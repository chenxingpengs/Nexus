using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Nexus.Models.Meeting;

namespace Nexus.Services.Meeting
{
    public class MeetingService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public MeetingService(string baseUrl)
        {
            _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        public void SetAuthToken(string token)
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        public async Task<ApiResponse<JoinMeetingResponse>> JoinMeetingAsync(string meetingId, string deviceId, int classId)
        {
            try
            {
                var request = new JoinMeetingRequest
                {
                    MeetingId = meetingId,
                    DeviceId = deviceId,
                    ClassId = classId
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/api/meeting/join", content);
                var responseJson = await response.Content.ReadAsStringAsync();

                var result = JsonSerializer.Deserialize<ApiResponse<JoinMeetingResponse>>(responseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return result ?? new ApiResponse<JoinMeetingResponse> { Code = 500, Msg = "解析响应失败" };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MeetingService] 加入会议失败: {ex.Message}");
                return new ApiResponse<JoinMeetingResponse> { Code = 500, Msg = $"加入会议失败: {ex.Message}" };
            }
        }

        public async Task<ApiResponse<object>> AcceptInvitationAsync(string meetingId, string deviceId, int classId)
        {
            try
            {
                var request = new
                {
                    meeting_id = meetingId,
                    device_id = deviceId,
                    class_id = classId
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/api/meeting/accept", content);
                var responseJson = await response.Content.ReadAsStringAsync();

                var result = JsonSerializer.Deserialize<ApiResponse<object>>(responseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return result ?? new ApiResponse<object> { Code = 500, Msg = "解析响应失败" };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MeetingService] 接受邀请失败: {ex.Message}");
                return new ApiResponse<object> { Code = 500, Msg = $"接受邀请失败: {ex.Message}" };
            }
        }

        public async Task<ApiResponse<object>> RejectInvitationAsync(string meetingId, int classId)
        {
            try
            {
                var request = new
                {
                    meeting_id = meetingId,
                    class_id = classId
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/api/meeting/reject", content);
                var responseJson = await response.Content.ReadAsStringAsync();

                var result = JsonSerializer.Deserialize<ApiResponse<object>>(responseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return result ?? new ApiResponse<object> { Code = 500, Msg = "解析响应失败" };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MeetingService] 拒绝邀请失败: {ex.Message}");
                return new ApiResponse<object> { Code = 500, Msg = $"拒绝邀请失败: {ex.Message}" };
            }
        }

        public async Task<ApiResponse<object>> LeaveMeetingAsync(string meetingId, string deviceId)
        {
            try
            {
                var request = new
                {
                    meeting_id = meetingId,
                    device_id = deviceId
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/api/meeting/leave", content);
                var responseJson = await response.Content.ReadAsStringAsync();

                var result = JsonSerializer.Deserialize<ApiResponse<object>>(responseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return result ?? new ApiResponse<object> { Code = 500, Msg = "解析响应失败" };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MeetingService] 离开会议失败: {ex.Message}");
                return new ApiResponse<object> { Code = 500, Msg = $"离开会议失败: {ex.Message}" };
            }
        }

        public async Task<ApiResponse<MeetingInfo>> GetMeetingDetailAsync(string meetingId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/meeting/{meetingId}");
                var responseJson = await response.Content.ReadAsStringAsync();

                var result = JsonSerializer.Deserialize<ApiResponse<MeetingInfo>>(responseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return result ?? new ApiResponse<MeetingInfo> { Code = 500, Msg = "解析响应失败" };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MeetingService] 获取会议详情失败: {ex.Message}");
                return new ApiResponse<MeetingInfo> { Code = 500, Msg = $"获取会议详情失败: {ex.Message}" };
            }
        }

        public async Task<ApiResponse<MeetingHistoryList>> GetMeetingHistoryAsync(int classId, int page = 1, int size = 10)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/meeting/history/{classId}?page={page}&size={size}");
                var responseJson = await response.Content.ReadAsStringAsync();

                var result = JsonSerializer.Deserialize<ApiResponse<MeetingHistoryList>>(responseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return result ?? new ApiResponse<MeetingHistoryList> { Code = 500, Msg = "解析响应失败" };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MeetingService] 获取会议历史失败: {ex.Message}");
                return new ApiResponse<MeetingHistoryList> { Code = 500, Msg = $"获取会议历史失败: {ex.Message}" };
            }
        }
    }

    public class ApiResponse<T>
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("msg")]
        public string Msg { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public T? Data { get; set; }

        public bool Success => Code == 200;
    }

    public class MeetingHistoryList
    {
        [JsonPropertyName("list")]
        public List<MeetingHistory> List { get; set; } = new();

        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("page")]
        public int Page { get; set; }

        [JsonPropertyName("size")]
        public int Size { get; set; }
    }
}
