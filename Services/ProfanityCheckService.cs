using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Nexus.Models;

namespace Nexus.Services;

public class ProfanityCheckService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ToastService? _toastService;
    private bool _disposed;
    
    private const string API_URL = "https://uapis.cn/api/v1/text/profanitycheck";
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ProfanityCheckService(ToastService? toastService = null)
    {
        _toastService = toastService;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Nexus/1.0.0");
    }

    public async Task<ProfanityCheckResult?> CheckTextAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            Debug.WriteLine("[ProfanityCheck] 文本为空，跳过检查");
            return null;
        }
        
        try
        {
            var body = new { text };
            var json = JsonSerializer.Serialize(body, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            Debug.WriteLine($"[ProfanityCheck] 检测文本: {text.Substring(0, Math.Min(text.Length, 50))}...");
            
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
            var response = await _httpClient.PostAsync(API_URL, content, cts.Token);
            var responseContent = await response.Content.ReadAsStringAsync(cts.Token);
            
            Debug.WriteLine($"[ProfanityCheck] 响应状态: {response.StatusCode}");
            
            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"[ProfanityCheck] API错误: {responseContent}");
                _toastService?.ShowError("违禁词检测服务暂时不可用");
                return null;
            }
            
            if (string.IsNullOrWhiteSpace(responseContent))
            {
                Debug.WriteLine("[ProfanityCheck] 响应为空");
                return null;
            }
            
            var result = JsonSerializer.Deserialize<ProfanityCheckResult>(responseContent, JsonOptions);
            
            if (result != null && result.HasForbiddenWords)
            {
                Debug.WriteLine($"[ProfanityCheck] 发现违禁词: {string.Join(", ", result.ForbiddenWords)}");
            }
            else
            {
                Debug.WriteLine("[ProfanityCheck] 未发现违禁词");
            }
            
            return result;
        }
        catch (TaskCanceledException)
        {
            Debug.WriteLine("[ProfanityCheck] 请求超时");
            _toastService?.ShowError("违禁词检测超时");
            return null;
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[ProfanityCheck] 网络错误: {ex.Message}");
            _toastService?.ShowError("网络连接失败，无法进行违禁词检测");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProfanityCheck] 错误: {ex.Message}");
            _toastService?.ShowError($"违禁词检测失败: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> ContainsForbiddenWordsAsync(string text)
    {
        var result = await CheckTextAsync(text);
        return result?.HasForbiddenWords ?? false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
