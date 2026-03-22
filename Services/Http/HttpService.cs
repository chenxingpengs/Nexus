using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Nexus.Models;

namespace Nexus.Services.Http;

public class HttpService : IDisposable
{
    private static readonly Lazy<HttpClient> _sharedClient = new(() =>
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    });

    protected HttpClient HttpClient => _sharedClient.Value;
    protected readonly ConfigService ConfigService;
    protected readonly ToastService? ToastService;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private bool _disposed;

    public HttpService(ConfigService configService, ToastService? toastService = null)
    {
        ConfigService = configService ?? throw new ArgumentNullException(nameof(configService));
        ToastService = toastService;
    }

    public async Task<ApiResponse<T>?> GetAsync<T>(string endpoint, RequestOptions? options = null)
    {
        options ??= RequestOptions.Default;
        var url = BuildUrl(endpoint);
        return await SendAsync<T>(HttpMethod.Get, url, null, options);
    }

    public async Task<ApiResponse<T>?> PostAsync<T>(string endpoint, object? body = null, RequestOptions? options = null)
    {
        options ??= RequestOptions.Default;
        var url = BuildUrl(endpoint);
        return await SendAsync<T>(HttpMethod.Post, url, body, options);
    }

    public async Task<ApiResponse<T>?> PutAsync<T>(string endpoint, object? body = null, RequestOptions? options = null)
    {
        options ??= RequestOptions.Default;
        var url = BuildUrl(endpoint);
        return await SendAsync<T>(HttpMethod.Put, url, body, options);
    }

    public async Task<ApiResponse<bool>?> DeleteAsync(string endpoint, RequestOptions? options = null)
    {
        options ??= RequestOptions.Default;
        var url = BuildUrl(endpoint);
        return await SendAsync<bool>(HttpMethod.Delete, url, null, options);
    }

    public async Task<string?> GetRawAsync(string endpoint, RequestOptions? options = null)
    {
        options ??= RequestOptions.Default;
        var url = BuildUrl(endpoint);
        
        var attempt = 0;
        Exception? lastException = null;

        while (attempt <= options.MaxRetries)
        {
            using var request = CreateRequest(HttpMethod.Get, url, null, options);
            
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds));
                var response = await HttpClient.SendAsync(request, cts.Token);
                
                if (!response.IsSuccessStatusCode && ShouldRetry(response.StatusCode) && attempt < options.MaxRetries)
                {
                    attempt++;
                    await Task.Delay(options.RetryDelayMs * attempt);
                    continue;
                }
                
                return await response.Content.ReadAsStringAsync();
            }
            catch (TaskCanceledException ex)
            {
                lastException = ex;
                if (attempt < options.MaxRetries)
                {
                    attempt++;
                    await Task.Delay(options.RetryDelayMs * attempt);
                    continue;
                }
                HandleException(ex, options);
                return null;
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
                if (attempt < options.MaxRetries)
                {
                    attempt++;
                await Task.Delay(options.RetryDelayMs * attempt);
                continue;
                }
                HandleException(ex, options);
                return null;
            }
            catch (Exception ex)
            {
                HandleException(ex, options);
                return null;
            }
        }

        return null;
    }

    private async Task<ApiResponse<T>?> SendAsync<T>(HttpMethod method, string url, object? body, RequestOptions options)
    {
        var attempt = 0;
        Exception? lastException = null;

        while (attempt <= options.MaxRetries)
        {
        using var request = CreateRequest(method, url, body, options);
        
        try
        {
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds));
            
            LogRequest(request, options, attempt);
            
            var response = await HttpClient.SendAsync(request, cts.Token);
            var content = await response.Content.ReadAsStringAsync(cts.Token);
            
            LogResponse(response, content, options);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorMsg = GetErrorMessage(response.StatusCode, content);
                
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    ToastService?.ShowError("登录已过期，请重新绑定设备");
                    return new ApiResponse<T> { Code = 401, Msg = errorMsg };
                }
                
                if (ShouldRetry(response.StatusCode) && attempt < options.MaxRetries)
                {
                    attempt++;
                    await Task.Delay(options.RetryDelayMs * attempt);
                    continue;
                }
                
                if (options.ShowErrorToast)
                {
                    ToastService?.ShowError(errorMsg);
                }
                
                return new ApiResponse<T> { Code = (int)response.StatusCode, Msg = errorMsg };
            }
            
            if (string.IsNullOrWhiteSpace(content))
            {
                return new ApiResponse<T> { Code = 200, Msg = "操作成功" };
            }
            
            var result = JsonSerializer.Deserialize<ApiResponse<T>>(content, JsonOptions);
            
            if (result == null)
            {
                if (options.ShowErrorToast)
                {
                    ToastService?.ShowError("数据格式错误");
                }
                return new ApiResponse<T> { Code = 0, Msg = "数据格式错误" };
            }
            
            if (!result.IsSuccess && options.ShowErrorToast)
            {
                ToastService?.ShowError(result.Msg);
            }
            
            if (result.IsSuccess && options.ShowSuccessToast)
            {
                ToastService?.ShowSuccess(result.Msg);
            }
            
            return result;
        }
        catch (TaskCanceledException ex)
        {
            lastException = ex;
            Debug.WriteLine($"[HttpService] 请求超时: {options.OperationName ?? url}");
            
            if (attempt < options.MaxRetries)
            {
                attempt++;
                await Task.Delay(options.RetryDelayMs * attempt);
                continue;
            }
            
            if (options.ShowErrorToast)
            {
                ToastService?.ShowError("请求超时，请稍后重试");
            }
            
            return new ApiResponse<T> { Code = 0, Msg = "请求超时" };
        }
        catch (HttpRequestException ex)
        {
            lastException = ex;
            Debug.WriteLine($"[HttpService] 网络错误: {ex.Message}");
            
        if (attempt < options.MaxRetries)
        {
            attempt++;
        await Task.Delay(options.RetryDelayMs * attempt);
        continue;
    }
    
    var errorMsg = GetNetworkErrorMessage(ex);
    if (options.ShowErrorToast)
    {
        ToastService?.ShowError(errorMsg);
    }
    
    return new ApiResponse<T> { Code = 0, Msg = errorMsg };
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"[HttpService] JSON解析错误: {ex.Message}");
            
            if (options.ShowErrorToast)
            {
                ToastService?.ShowError("数据格式错误");
            }
            
            return new ApiResponse<T> { Code = 0, Msg = "数据格式错误" };
        }
        catch (Exception ex)
        {
            lastException = ex;
            Debug.WriteLine($"[HttpService] 未知错误: {ex.Message}");
            
            if (options.ShowErrorToast)
        {
                ToastService?.ShowError($"操作失败: {ex.Message}");
            }
            
            return new ApiResponse<T> { Code = 0, Msg = ex.Message };
        }
    }

    return new ApiResponse<T> { Code = 0, Msg = lastException?.Message ?? "请求失败" };
}

 private HttpRequestMessage CreateRequest(HttpMethod method, string url, object? body, RequestOptions options)
    {
        var request = new HttpRequestMessage(method, url);
        
        if (body != null)
        {
            var json = JsonSerializer.Serialize(body, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }
        
        if (options.RequireAuth)
        {
            var token = ConfigService.Config.AccessToken;
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }
        
        return request;
    }

    private string BuildUrl(string endpoint)
    {
        var baseUrl = ConfigService.Config.ServerUrl;
        
        if (endpoint.StartsWith("http://") || endpoint.StartsWith("https://"))
        {
            return endpoint;
        }
        
        if (!baseUrl.EndsWith("/") && !endpoint.StartsWith("/"))
        {
            return $"{baseUrl}/{endpoint}";
        }
        
        return $"{baseUrl}{endpoint}";
    }

    private bool ShouldRetry(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
        HttpStatusCode.InternalServerError => true,
        HttpStatusCode.BadGateway => true,
        HttpStatusCode.ServiceUnavailable => true,
        HttpStatusCode.GatewayTimeout => true,
        HttpStatusCode.RequestTimeout => true,
        HttpStatusCode.TooManyRequests => true,
        _ => false
        };
    }

    private string GetErrorMessage(HttpStatusCode statusCode, string content)
    {
        return statusCode switch
        {
            HttpStatusCode.BadRequest => "请求参数错误",
            HttpStatusCode.Unauthorized => "登录已过期，请重新绑定",
            HttpStatusCode.Forbidden => "没有权限执行此操作",
            HttpStatusCode.NotFound => "请求的资源不存在",
            HttpStatusCode.InternalServerError => "服务器繁忙，请稍后重试",
            HttpStatusCode.BadGateway => "网关错误，请稍后重试",
            HttpStatusCode.ServiceUnavailable => "服务暂时不可用",
            HttpStatusCode.GatewayTimeout => "网关超时，请稍后重试",
            HttpStatusCode.RequestTimeout => "请求超时",
            HttpStatusCode.TooManyRequests => "请求过于频繁，请稍后重试",
            _ => TryExtractMessage(content) ?? $"请求失败 ({(int)statusCode}"
        };
    }

    private string GetNetworkErrorMessage(HttpRequestException ex)
    {
        if (ex.InnerException is System.Net.Sockets.SocketException socketEx)
        {
            return socketEx.SocketErrorCode switch
            {
                System.Net.Sockets.SocketError.HostNotFound => "无法连接到服务器",
                System.Net.Sockets.SocketError.ConnectionRefused => "服务器拒绝连接",
                System.Net.Sockets.SocketError.TimedOut => "连接超时",
                System.Net.Sockets.SocketError.NetworkUnreachable => "网络不可用",
                _ => "网络连接失败，请检查网络"
            };
        }
        
        return "网络连接失败,请检查网络";
    }

    private string? TryExtractMessage(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;
        
        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("msg", out var msgElement))
            {
                return msgElement.GetString();
            }
            
            if (root.TryGetProperty("message", out var messageElement))
            {
                return messageElement.GetString();
            }
        }
        catch
        {
        }
        
        return null;
    }

    private void LogRequest(HttpRequestMessage request, RequestOptions options, int attempt)
    {
        var operationName = options.OperationName ?? request.RequestUri?.ToString();
        Debug.WriteLine($"[HttpService] >>> {request.Method} {request.RequestUri} (Attempt {attempt + 1})");
        
        if (request.Content != null)
        {
        Debug.WriteLine($"[HttpService] >>> Body: {request.Content}");
        }
    }

    private void LogResponse(HttpResponseMessage response, string content, RequestOptions options)
    {
        Debug.WriteLine($"[HttpService] <<< {response.StatusCode} {response.RequestMessage?.RequestUri}");
        
        if (!string.IsNullOrEmpty(content) && content.Length < 500)
        {
            Debug.WriteLine($"[HttpService] <<< Content: {content}");
        }
        else if (!string.IsNullOrEmpty(content))
        {
        Debug.WriteLine($"[HttpService] <<< Content: {content.Substring(0, 500)}... (truncated)");
        }
    }

    private void HandleException(Exception ex, RequestOptions options)
    {
        Debug.WriteLine($"[HttpService] 异常: {ex.Message}");
        Debug.WriteLine($"[HttpService] 堆栈: {ex.StackTrace}");
        
        if (options.ShowErrorToast && ToastService != null)
        {
            var message = ex switch
            {
                TaskCanceledException => "请求超时，请稍后重试",
                HttpRequestException httpEx => GetNetworkErrorMessage(httpEx),
                JsonException => "数据格式错误",
                _ => $"操作失败: {ex.Message}"
            };
            
            ToastService.ShowError(message);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
