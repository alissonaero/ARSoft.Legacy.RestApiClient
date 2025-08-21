using Newtonsoft.Json;
using Polly;
using Polly.Retry;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ARSoft.Legacy.RestApiClient
{
	public enum AuthType
	{
		None,
		Bearer,
		Basic,
		ApiKey  
	}

	public interface IApiClient
	{
		Task<ApiResponse<TResponse>> GetAsync<TResponse>(Uri url, string authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default);
		Task<ApiResponse<TResponse>> PostAsync<TRequest, TResponse>(Uri url, TRequest payload, string authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default);
		Task<ApiResponse<TResponse>> PutAsync<TRequest, TResponse>(Uri url, TRequest payload, string authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default);
		Task<ApiResponse<TResponse>> DeleteAsync<TResponse>(Uri url, string authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default);
		Task<ApiResponse<TResponse>> PatchAsync<TRequest, TResponse>(Uri url, TRequest payload, string authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default); 
		Task<ApiResponse<TResponse>> GetAsync<TResponse>(string url, string authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default);
		Task<ApiResponse<TResponse>> PostAsync<TRequest, TResponse>(string url, TRequest payload, string authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default);
	}

	public class ApiClient : IApiClient, IDisposable
	{
		private readonly HttpClient _httpClient;
		private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
		private readonly JsonSerializerSettings _jsonSettings;
		private readonly bool _disposeHttpClient;

		public ApiClient(HttpClient httpClient,
						 JsonSerializerSettings jsonSettings = null,
						 AsyncRetryPolicy<HttpResponseMessage> retryPolicy = null,
						 bool disposeHttpClient = false) 
		{
			_httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
			_jsonSettings = jsonSettings ?? DefaultJsonSettings();
			_retryPolicy = retryPolicy ?? CreateDefaultRetryPolicy();
			_disposeHttpClient = disposeHttpClient;
		}

		// Construtor de conveniência
		public ApiClient() : this(new HttpClient(), null, null, true) { }

		public async Task<ApiResponse<TResponse>> GetAsync<TResponse>(Uri url, string authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default)
		{
			return await SendAsync<object, TResponse>(HttpMethod.Get, url, null, authToken, authType, cancellationToken);
		}

		public async Task<ApiResponse<TResponse>> GetAsync<TResponse>(string url, string authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default)
		{
			return await GetAsync<TResponse>(new Uri(url), authToken, authType, cancellationToken);
		}

		public async Task<ApiResponse<TResponse>> PostAsync<TRequest, TResponse>(Uri url, TRequest payload, string authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default)
		{
			return await SendAsync<TRequest, TResponse>(HttpMethod.Post, url, payload, authToken, authType, cancellationToken);
		}

		public async Task<ApiResponse<TResponse>> PostAsync<TRequest, TResponse>(string url, TRequest payload, string authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default)
		{
			return await PostAsync<TRequest, TResponse>(new Uri(url), payload, authToken, authType, cancellationToken);
		}

		public async Task<ApiResponse<TResponse>> PutAsync<TRequest, TResponse>(Uri url, TRequest payload, string authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default)
		{
			return await SendAsync<TRequest, TResponse>(HttpMethod.Put, url, payload, authToken, authType, cancellationToken);
		}

		public async Task<ApiResponse<TResponse>> DeleteAsync<TResponse>(Uri url, string authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default)
		{
			return await SendAsync<object, TResponse>(HttpMethod.Delete, url, null, authToken, authType, cancellationToken);
		}

		public async Task<ApiResponse<TResponse>> PatchAsync<TRequest, TResponse>(Uri url, TRequest payload, string authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default)
		{
			return await SendAsync<TRequest, TResponse>(new HttpMethod("PATCH"), url, payload, authToken, authType, cancellationToken);
		}

		private async Task<ApiResponse<TResponse>> SendAsync<TRequest, TResponse>(HttpMethod method, Uri url, TRequest payload, string authToken, AuthType authType, CancellationToken cancellationToken)
		{
			var result = new ApiResponse<TResponse>();
			string responseJson = null;
			HttpResponseMessage httpResponse = null;

			try
			{
				httpResponse = await _retryPolicy.ExecuteAsync(async () =>
				{
					using (var request = new HttpRequestMessage(method, url))
					{
						SetHeaders(request, authToken, authType);
						SetContent(request, payload, method);

						return await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
					}
				});

				responseJson = await ReadResponseContent(httpResponse);
				result.StatusCode = httpResponse.StatusCode;

				if (!httpResponse.IsSuccessStatusCode)
				{
					result.Success = false;
					result.ErrorMessage = GetErrorMessage(httpResponse.StatusCode);
					result.ErrorData = responseJson;
					return result;
				}

				if (typeof(TResponse) == typeof(string))
				{					
					result.Data = (TResponse)(object)responseJson;
				}
				else if (!string.IsNullOrWhiteSpace(responseJson))
				{
					result.Data = JsonConvert.DeserializeObject<TResponse>(responseJson, _jsonSettings);
				}
				 

				result.Success = true;
			}
			catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
			{
				result.Success = false;
				result.ErrorMessage = "Request timeout";
				result.ErrorData = ex.Message;
			}
			catch (TaskCanceledException ex) when (cancellationToken.IsCancellationRequested)
			{
				result.Success = false;
				result.ErrorMessage = "Request was cancelled";
				result.ErrorData = ex.Message;
			}
			catch (HttpRequestException ex)
			{
				result.Success = false;
				result.ErrorMessage = $"Network error: {ex.Message}";
				result.ErrorData = responseJson;
			}
			catch (JsonException ex) // Catches both JsonSerializationException quanto JsonReaderException
			{
				result.Success = false;
				result.ErrorMessage = $"JSON processing error: {ex.Message}";
				result.ErrorData = responseJson;
			}
			catch (Exception ex)
			{
				result.Success = false;
				result.ErrorMessage = $"Unexpected error: {ex.Message}";
				result.ErrorData = responseJson;
			}
			finally
			{
				httpResponse?.Dispose();
			}

			return result;
		}

		private void SetHeaders(HttpRequestMessage request, string authToken, AuthType authType)
		{
			request.Headers.Add("Accept", "application/json");

			if (!string.IsNullOrWhiteSpace(authToken) && authType != AuthType.None)
			{
				var hasAuthorizationHeader = _httpClient.DefaultRequestHeaders.Authorization != null;

				if (!hasAuthorizationHeader)
				{
					switch (authType)
					{
						case AuthType.Bearer:
							request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
							break;
						case AuthType.Basic:
							request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authToken);
							break;
						case AuthType.ApiKey:
							request.Headers.Add("X-API-Key", authToken);
							break;
					}
				}
			}
		}

		private void SetContent<TRequest>(HttpRequestMessage request, TRequest payload, HttpMethod method)
		{
			if (payload != null && (method == HttpMethod.Post || method == HttpMethod.Put || method.Method == "PATCH"))
			{
				var json = JsonConvert.SerializeObject(payload, _jsonSettings);
				request.Content = new StringContent(json, Encoding.UTF8, "application/json");
			}
		}

		private async Task<string> ReadResponseContent(HttpResponseMessage response)
		{
			if (response.Content == null) return null;
			return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
		}

		private string GetErrorMessage(HttpStatusCode statusCode)
		{
			switch (statusCode)
			{
				case HttpStatusCode.BadRequest:
					return "Bad Request (400)";
				case HttpStatusCode.Unauthorized:
					return "Unauthorized (401)";
				case HttpStatusCode.Forbidden:
					return "Forbidden (403)";
				case HttpStatusCode.NotFound:
					return "Not Found (404)";
				case HttpStatusCode.InternalServerError:
					return "Internal Server Error (500)";
				case HttpStatusCode.BadGateway:
					return "Bad Gateway (502)";
				case HttpStatusCode.ServiceUnavailable:
					return "Service Unavailable (503)";
				default:
					return $"HTTP {(int)statusCode}";
			}
		}

		private static AsyncRetryPolicy<HttpResponseMessage> CreateDefaultRetryPolicy()
		{
			return Policy
				.Handle<HttpRequestException>()
				.OrResult<HttpResponseMessage>(r =>
					!r.IsSuccessStatusCode &&
					((int)r.StatusCode == 429 || // Too Many Requests
					 r.StatusCode == HttpStatusCode.ServiceUnavailable ||
					 r.StatusCode == HttpStatusCode.RequestTimeout))
				.WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
		}

		private static JsonSerializerSettings DefaultJsonSettings()
		{
			return new JsonSerializerSettings
			{
				ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver
				{
					NamingStrategy = new Newtonsoft.Json.Serialization.CamelCaseNamingStrategy(),
				},
				DateFormatString = "yyyy-MM-ddTHH:mm:ss.fffZ",
				MissingMemberHandling = MissingMemberHandling.Ignore,  
				NullValueHandling = NullValueHandling.Ignore,
				TypeNameHandling = TypeNameHandling.None,
				DateTimeZoneHandling = DateTimeZoneHandling.Utc
			};
		}

		public void Dispose()
		{
			if (_disposeHttpClient)
			{
				_httpClient?.Dispose();
			}
		}
	}

	public class ApiResponse<T>
	{
		public bool Success { get; set; }
		public T Data { get; set; }
		public string ErrorMessage { get; set; }
		public string ErrorData { get; set; }
		public HttpStatusCode? StatusCode { get; set; } //for debugging porposes

		public bool IsClientError => StatusCode.HasValue && (int)StatusCode.Value >= 400 && (int)StatusCode.Value < 500;
		public bool IsServerError => StatusCode.HasValue && (int)StatusCode.Value >= 500;
	}

 
	public static class ApiClientExtensions
	{
		public static async Task<ApiResponse<T>> GetAsync<T>(this IApiClient client, string url)
		{
			return await client.GetAsync<T>(new Uri(url));
		}

		public static async Task<ApiResponse<T>> PostAsJsonAsync<T>(this IApiClient client, string url, object payload)
		{
			return await client.PostAsync<object, T>(new Uri(url), payload);
		}
	}
}