[readme_file.md](https://github.com/user-attachments/files/21907434/readme_file.md)
# ApiClient Library for .NET Framework

A lightweight, resilient HTTP API client library designed specifically for .NET Framework 4.8.1 and legacy applications, providing modern async HTTP capabilities similar to those available in .NET 5+.

## 🎯 Motivation

While modern .NET versions (5+) include convenient extension methods like `PostAsJsonAsync`, `ReadFromJsonAsync`, and `GetFromJsonAsync`, .NET Framework projects are limited to basic `HttpClient` functionality. This library bridges that gap by providing:

- **Modern async patterns** for HTTP operations
- **Built-in retry policies** with exponential backoff
- **Automatic JSON serialization/deserialization**
- **Multiple authentication methods** (Bearer, Basic, API Key)
- **Comprehensive error handling** with detailed response information
- **Clean, testable interface** following SOLID principles

Perfect for Windows Forms applications, legacy web applications, and any .NET Framework project that needs robust HTTP client capabilities.

## 🚀 Quick Start

### Installation

1. Add the source files to your project
2. Install required NuGet packages:
   ```xml
   <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
   <PackageReference Include="Polly" Version="7.2.4" />
   ```

### Basic Usage

```csharp
using ApiClientLibrary;

// Simple GET request
using (var client = new ApiClient())
{
    var response = await client.GetAsync<User>("https://api.example.com/users/1");
    
    if (response.Success)
    {
        Console.WriteLine($"User: {response.Data.Name}");
    }
    else
    {
        Console.WriteLine($"Error: {response.ErrorMessage}");
    }
}
```

## 📖 Detailed Examples

### 1. GET Request with Authentication

```csharp
public async Task<List<Product>> GetProductsAsync(string bearerToken)
{
    using (var client = new ApiClient())
    {
        var response = await client.GetAsync<List<Product>>(
            "https://api.shop.com/products", 
            bearerToken, 
            AuthType.Bearer);

        if (response.Success)
        {
            return response.Data;
        }
        
        // Handle error
        throw new HttpRequestException($"Failed to get products: {response.ErrorMessage}");
    }
}
```

### 2. POST Request with JSON Payload

```csharp
public async Task<User> CreateUserAsync(CreateUserRequest request)
{
    using (var client = new ApiClient())
    {
        var response = await client.PostAsync<CreateUserRequest, User>(
            new Uri("https://api.example.com/users"),
            request);

        if (!response.Success)
        {
            throw new ApiException($"User creation failed: {response.ErrorMessage}");
        }

        return response.Data;
    }
}

public class CreateUserRequest
{
    public string Name { get; set; }
    public string Email { get; set; }
    public DateTime BirthDate { get; set; }
}
```

### 3. Custom Configuration

```csharp
// Custom JSON settings
var jsonSettings = new JsonSerializerSettings
{
    DateFormatString = "dd/MM/yyyy",
    NullValueHandling = NullValueHandling.Ignore
};

// Custom retry policy
var retryPolicy = Policy
    .Handle<HttpRequestException>()
    .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode && (int)r.StatusCode >= 500)
    .WaitAndRetryAsync(5, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));

// Custom HttpClient with timeout
var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

using (var client = new ApiClient(httpClient, jsonSettings, retryPolicy))
{
    // Your API calls here
}
```

## 🖥️ Windows Forms Integration Examples

### Example 1: Product Management Form

```csharp
public partial class ProductManagementForm : Form
{
    private readonly IApiClient _apiClient;

    public ProductManagementForm()
    {
        InitializeComponent();
        _apiClient = new ApiClient();
    }

    private async void LoadProductsButton_Click(object sender, EventArgs e)
    {
        try
        {
            LoadingLabel.Visible = true;
            LoadProductsButton.Enabled = false;

            var response = await _apiClient.GetAsync<List<Product>>(
                "https://api.inventory.com/products",
                GetAuthToken(),
                AuthType.Bearer);

            if (response.Success)
            {
                ProductsDataGridView.DataSource = response.Data;
                StatusLabel.Text = $"Loaded {response.Data.Count} products";
            }
            else
            {
                MessageBox.Show($"Failed to load products: {response.ErrorMessage}", 
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unexpected error: {ex.Message}", 
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            LoadingLabel.Visible = false;
            LoadProductsButton.Enabled = true;
        }
    }

    private async void SaveProductButton_Click(object sender, EventArgs e)
    {
        if (!ValidateForm()) return;

        var product = new Product
        {
            Name = NameTextBox.Text,
            Price = decimal.Parse(PriceTextBox.Text),
            Category = CategoryComboBox.SelectedItem.ToString()
        };

        try
        {
            SaveProductButton.Enabled = false;

            var response = await _apiClient.PostAsync<Product, Product>(
                "https://api.inventory.com/products",
                product,
                GetAuthToken(),
                AuthType.Bearer);

            if (response.Success)
            {
                MessageBox.Show("Product saved successfully!", "Success", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                ClearForm();
                // Refresh the products list
                LoadProductsButton_Click(sender, e);
            }
            else
            {
                MessageBox.Show($"Failed to save product: {response.ErrorMessage}", 
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        finally
        {
            SaveProductButton.Enabled = true;
        }
    }

    private string GetAuthToken()
    {
        // Retrieve from secure storage, config, or login form
        return Properties.Settings.Default.ApiToken;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _apiClient?.Dispose();
            components?.Dispose();
        }
        base.Dispose(disposing);
    }
}
```

### Example 2: Login Form with API Authentication

```csharp
public partial class LoginForm : Form
{
    private readonly IApiClient _apiClient;

    public string AuthToken { get; private set; }
    public User CurrentUser { get; private set; }

    public LoginForm()
    {
        InitializeComponent();
        _apiClient = new ApiClient();
    }

    private async void LoginButton_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(UsernameTextBox.Text) || 
            string.IsNullOrWhiteSpace(PasswordTextBox.Text))
        {
            MessageBox.Show("Please enter username and password", "Validation Error");
            return;
        }

        try
        {
            LoginButton.Enabled = false;
            LoginButton.Text = "Logging in...";

            var loginRequest = new LoginRequest
            {
                Username = UsernameTextBox.Text,
                Password = PasswordTextBox.Text
            };

            var response = await _apiClient.PostAsync<LoginRequest, LoginResponse>(
                "https://api.myapp.com/auth/login",
                loginRequest);

            if (response.Success && !string.IsNullOrEmpty(response.Data.Token))
            {
                AuthToken = response.Data.Token;
                CurrentUser = response.Data.User;
                
                // Save token securely
                Properties.Settings.Default.ApiToken = AuthToken;
                Properties.Settings.Default.Save();

                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                MessageBox.Show(response.ErrorMessage ?? "Invalid credentials", 
                    "Login Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                PasswordTextBox.Clear();
                PasswordTextBox.Focus();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Login error: {ex.Message}", "Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            LoginButton.Enabled = true;
            LoginButton.Text = "Login";
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _apiClient?.Dispose();
            components?.Dispose();
        }
        base.Dispose(disposing);
    }
}

public class LoginRequest
{
    public string Username { get; set; }
    public string Password { get; set; }
}

public class LoginResponse
{
    public string Token { get; set; }
    public User User { get; set; }
    public DateTime ExpiresAt { get; set; }
}
```

### Example 3: Background Data Sync with Progress

```csharp
public partial class DataSyncForm : Form
{
    private readonly IApiClient _apiClient;
    private CancellationTokenSource _cancellationTokenSource;

    public DataSyncForm()
    {
        InitializeComponent();
        _apiClient = new ApiClient();
    }

    private async void StartSyncButton_Click(object sender, EventArgs e)
    {
        _cancellationTokenSource = new CancellationTokenSource();
        StartSyncButton.Enabled = false;
        CancelSyncButton.Enabled = true;
        ProgressBar.Value = 0;

        try
        {
            await SyncAllDataAsync(_cancellationTokenSource.Token);
            MessageBox.Show("Sync completed successfully!", "Success");
        }
        catch (OperationCanceledException)
        {
            MessageBox.Show("Sync was cancelled", "Cancelled");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Sync failed: {ex.Message}", "Error");
        }
        finally
        {
            StartSyncButton.Enabled = true;
            CancelSyncButton.Enabled = false;
            ProgressBar.Value = 0;
        }
    }

    private async Task SyncAllDataAsync(CancellationToken cancellationToken)
    {
        var syncTasks = new[]
        {
            ("Customers", () => SyncCustomersAsync(cancellationToken)),
            ("Products", () => SyncProductsAsync(cancellationToken)),
            ("Orders", () => SyncOrdersAsync(cancellationToken))
        };

        for (int i = 0; i < syncTasks.Length; i++)
        {
            var (name, task) = syncTasks[i];
            
            StatusLabel.Text = $"Syncing {name}...";
            await task();
            
            ProgressBar.Value = (i + 1) * 100 / syncTasks.Length;
            Application.DoEvents(); // Allow UI updates
        }

        StatusLabel.Text = "Sync completed";
    }

    private async Task SyncCustomersAsync(CancellationToken cancellationToken)
    {
        var response = await _apiClient.GetAsync<List<Customer>>(
            "https://api.myapp.com/customers",
            GetAuthToken(),
            AuthType.Bearer,
            cancellationToken);

        if (response.Success)
        {
            // Save to local database
            await SaveCustomersToLocalDbAsync(response.Data);
        }
        else
        {
            throw new Exception($"Failed to sync customers: {response.ErrorMessage}");
        }
    }

    private void CancelSyncButton_Click(object sender, EventArgs e)
    {
        _cancellationTokenSource?.Cancel();
    }
}
```

## 🆚 Comparison with Modern .NET APIs

| Feature | This Library (.NET Framework) | Modern .NET 5+ |
|---------|-------------------------------|-----------------|
| **JSON GET** | `await client.GetAsync<T>(url)` | `await httpClient.GetFromJsonAsync<T>(url)` |
| **JSON POST** | `await client.PostAsync<TReq, TRes>(url, data)` | `await httpClient.PostAsJsonAsync(url, data)` |
| **Error Handling** | Built-in `ApiResponse<T>` with detailed errors | Manual exception handling |
| **Retry Policy** | Built-in with Polly integration | Manual Policy configuration |
| **Authentication** | Multiple auth types support | Manual header management |
| **Async/Await** | ✅ Full support | ✅ Full support |
| **Cancellation** | ✅ CancellationToken support | ✅ CancellationToken support |

## 🔧 Configuration Options

### Authentication Types

```csharp
// Bearer Token
await client.GetAsync<User>(url, "your-jwt-token", AuthType.Bearer);

// Basic Authentication (base64 encoded)
var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
await client.GetAsync<User>(url, credentials, AuthType.Basic);

// API Key
await client.GetAsync<User>(url, "your-api-key", AuthType.ApiKey);
```

### Custom JSON Settings

```csharp
var settings = new JsonSerializerSettings
{
    DateFormatString = "yyyy-MM-dd",
    NullValueHandling = NullValueHandling.Ignore,
    ContractResolver = new DefaultContractResolver
    {
        NamingStrategy = new SnakeCaseNamingStrategy()
    }
};

var client = new ApiClient(httpClient, settings);
```

## 🛡️ Error Handling

The library provides comprehensive error information through the `ApiResponse<T>` class:

```csharp
var response = await client.GetAsync<User>("https://api.example.com/user/123");

if (!response.Success)
{
    // Check the type of error
    if (response.StatusCode == HttpStatusCode.NotFound)
    {
        // Handle 404 specifically
    }
    else if (response.StatusCode >= HttpStatusCode.InternalServerError)
    {
        // Handle server errors
    }
    
    // Access detailed error information
    Console.WriteLine($"Error: {response.ErrorMessage}");
    Console.WriteLine($"Raw response: {response.ErrorData}");
}
```

## 🤝 Contributing

Contributions are welcome! Please feel free to submit issues, feature requests, or pull requests.

## 📝 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🏗️ Requirements

- .NET Framework 4.8.1
- Newtonsoft.Json 13.0.3+
- Polly 7.2.4+

## 🔄 Version History

### v1.0.0
- Initial release
- Support for GET, POST, PUT, DELETE, PATCH operations
- Built-in retry policies with exponential backoff
- Multiple authentication methods
- Comprehensive error handling
- Full async/await support
