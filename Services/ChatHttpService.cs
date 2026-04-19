using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Nexus.Models.Chat;

namespace Nexus.Services
{
    public class ChatHttpService : IDisposable
    {
        private readonly ConfigService _configService;
        private readonly HttpClient _httpClient;
        private readonly ToastService? _toastService;
        private bool _disposed;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public ChatHttpService(ConfigService configService, ToastService? toastService = null)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _toastService = toastService;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<ConversationListResponse?> GetConversationsAsync(int page = 1, int size = 20)
        {
            var url = BuildUrl($"/api/chat/conversations?page={page}&size={size}");
            return await SendRequestAsync<ConversationListResponse>(HttpMethod.Get, url);
        }

        public async Task<Conversation?> GetConversationDetailAsync(string conversationId)
        {
            var url = BuildUrl($"/api/chat/conversations/{conversationId}");
            var response = await SendRequestAsync<Conversation>(HttpMethod.Get, url);
            return response;
        }

        public async Task<MessageListResponse?> GetMessagesAsync(
            string conversationId,
            int page = 1,
            int size = 30,
            int? beforeId = null,
            int? afterId = null)
        {
            var url = BuildUrl($"/api/chat/conversations/{conversationId}/messages?page={page}&size={size}");
            
            if (beforeId.HasValue)
            {
                url += $"&before_id={beforeId.Value}";
            }
            
            if (afterId.HasValue)
            {
                url += $"&after_id={afterId.Value}";
            }

            return await SendRequestAsync<MessageListResponse>(HttpMethod.Get, url);
        }

        public async Task<ChatMessage?> SendMessageAsync(
            string conversationId,
            string content,
            string type = "text",
            int? replyToId = null,
            Dictionary<string, object>? extraData = null)
        {
            var url = BuildUrl($"/api/chat/conversations/{conversationId}/messages");

            var body = new Dictionary<string, object>
            {
                { "content", content },
                { "type", type }
            };

            if (replyToId.HasValue)
            {
                body["reply_to_id"] = replyToId.Value;
            }

            if (extraData != null)
            {
                body["extra_data"] = extraData;
            }

            return await SendRequestAsync<ChatMessage>(HttpMethod.Post, url, body);
        }

        public async Task<bool> MarkAsReadAsync(string conversationId, int? lastMessageId = null)
        {
            var url = BuildUrl($"/api/chat/conversations/{conversationId}/read");

            var body = new Dictionary<string, object>();
            if (lastMessageId.HasValue)
            {
                body["last_message_id"] = lastMessageId.Value;
            }

            var response = await SendRequestAsync<object>(HttpMethod.Put, url, body);
            return response != null;
        }

        private string BuildUrl(string endpoint)
        {
            var baseUrl = _configService.Config.ServerUrl;
            
            if (string.IsNullOrEmpty(baseUrl))
            {
                throw new InvalidOperationException("服务器地址未配置");
            }

            return $"{baseUrl.TrimEnd('/')}/{endpoint.TrimStart('/')}";
        }

        private async Task<T?> SendRequestAsync<T>(HttpMethod method, string url, object? body = null)
        {
            try
            {
                using var request = new HttpRequestMessage(method, url);
                
                request.Headers.UserAgent.ParseAdd("Nexus/1.0.0");
                
                var token = _configService.Config.AccessToken;
                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                var deviceId = _configService.Config.DeviceId;
                if (!string.IsNullOrEmpty(deviceId))
                {
                    request.Headers.Add("X-Device-ID", deviceId);
                }

                if (body != null)
                {
                    var json = JsonSerializer.Serialize(body, JsonOptions);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }

                Debug.WriteLine($"[ChatHttp] {method} {url}");

                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30));
                var response = await _httpClient.SendAsync(request, cts.Token);
                var content = await response.Content.ReadAsStringAsync(cts.Token);

                Debug.WriteLine($"[ChatHttp] Response: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorMsg = TryExtractMessage(content) ?? $"请求失败 ({(int)response.StatusCode})";
                    Debug.WriteLine($"[ChatHttp] Error: {errorMsg}");
                    _toastService?.ShowError(errorMsg);
                    return default;
                }

                if (string.IsNullOrWhiteSpace(content))
                {
                    return default;
                }

                var apiResponse = JsonSerializer.Deserialize<ApiResponse<T>>(content, JsonOptions);
                
                if (apiResponse == null)
                {
                    Debug.WriteLine("[ChatHttp] Failed to parse response");
                    return default;
                }

                if (!apiResponse.Success)
                {
                    Debug.WriteLine($"[ChatHttp] API Error: {apiResponse.Msg}");
                    _toastService?.ShowError(apiResponse.Msg);
                    return default;
                }

                return apiResponse.Data;
            }
            catch (TaskCanceledException)
            {
                Debug.WriteLine("[ChatHttp] Request timeout");
                _toastService?.ShowError("请求超时");
                return default;
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"[ChatHttp] Network error: {ex.Message}");
                _toastService?.ShowError("网络连接失败");
                return default;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatHttp] Error: {ex.Message}");
                _toastService?.ShowError($"操作失败: {ex.Message}");
                return default;
            }
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

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _httpClient.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
