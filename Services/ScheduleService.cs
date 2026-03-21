using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Nexus.Models.Schedule;

namespace Nexus.Services
{
    public class ScheduleService
    {
        private readonly HttpClient _httpClient;
        private readonly ConfigService _configService;

        public event Action<string>? StatusChanged;
        public event Action<bool>? LoadingStateChanged;

        public ScheduleService(ConfigService configService)
        {
            _configService = configService;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60)
            };
        }

        private void SetAuthHeader()
        {
            var token = _configService.Config.AccessToken;
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Bearer", token);
            }
        }

        public async Task<ScheduleCompletenessModel?> CheckCompletenessAsync(int classId)
        {
            var config = _configService.Config;

            System.Diagnostics.Debug.WriteLine($"[ScheduleService] CheckCompletenessAsync 开始, classId={classId}");

            try
            {
                LoadingStateChanged?.Invoke(true);
                StatusChanged?.Invoke("正在检查排班配置...");

                SetAuthHeader();

                var url = $"{config.ServerUrl}/desktop/schedule/completeness?class_id={classId}";
                System.Diagnostics.Debug.WriteLine($"[ScheduleService] 请求URL: {url}");

                var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[ScheduleService] 响应内容: {content}");

                if (!response.IsSuccessStatusCode)
                {
                    StatusChanged?.Invoke($"服务器错误: {response.StatusCode}");
                    return null;
                }

                var result = JsonSerializer.Deserialize<ScheduleApiResponse<ScheduleCompletenessModel>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result == null || result.Code != 200)
                {
                    StatusChanged?.Invoke(result?.Msg ?? "检查失败");
                    return null;
                }

                StatusChanged?.Invoke(result.Data?.IsComplete == true ? "排班配置完整" : "排班配置不完整");
                return result.Data;
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ScheduleService] HTTP错误: {ex.Message}");
                StatusChanged?.Invoke($"网络错误: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ScheduleService] 异常: {ex.Message}");
                StatusChanged?.Invoke($"检查失败: {ex.Message}");
                return null;
            }
            finally
            {
                LoadingStateChanged?.Invoke(false);
            }
        }

        public async Task<List<PeriodicRuleModel>?> GetPeriodicRulesAsync(int classId)
        {
            var config = _configService.Config;

            try
            {
                LoadingStateChanged?.Invoke(true);
                SetAuthHeader();

                var url = $"{config.ServerUrl}/desktop/periodic-rules?class_id={classId}";
                var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var result = JsonSerializer.Deserialize<ScheduleApiResponse<List<PeriodicRuleModel>>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return result?.Data;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ScheduleService] GetPeriodicRulesAsync 异常: {ex.Message}");
                return null;
            }
            finally
            {
                LoadingStateChanged?.Invoke(false);
            }
        }

        public async Task<PeriodicRuleModel?> AddPeriodicRuleAsync(AddPeriodicRuleRequest request)
        {
            var config = _configService.Config;

            try
            {
                LoadingStateChanged?.Invoke(true);
                SetAuthHeader();

                var url = $"{config.ServerUrl}/desktop/periodic-rules";
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"[ScheduleService] 添加规则失败: {responseContent}");
                    return null;
                }

                var result = JsonSerializer.Deserialize<ScheduleApiResponse<PeriodicRuleModel>>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result?.Code != 200)
                {
                    StatusChanged?.Invoke(result?.Msg ?? "添加失败");
                    return null;
                }

                StatusChanged?.Invoke("添加成功");
                return result.Data;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ScheduleService] AddPeriodicRuleAsync 异常: {ex.Message}");
                return null;
            }
            finally
            {
                LoadingStateChanged?.Invoke(false);
            }
        }

        public async Task<BatchAddResult?> BatchAddPeriodicRulesAsync(int classId, List<AddPeriodicRuleRequest> rules)
        {
            var config = _configService.Config;

            try
            {
                LoadingStateChanged?.Invoke(true);
                StatusChanged?.Invoke("正在批量保存排班规则...");
                SetAuthHeader();

                var url = $"{config.ServerUrl}/desktop/periodic-rules/batch";
                var request = new BatchAddPeriodicRulesRequest
                {
                    ClassId = classId,
                    Rules = rules
                };
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    StatusChanged?.Invoke($"服务器错误: {response.StatusCode}");
                    return null;
                }

                var result = JsonSerializer.Deserialize<ScheduleApiResponse<BatchAddResult>>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result?.Code != 200)
                {
                    StatusChanged?.Invoke(result?.Msg ?? "批量添加失败");
                    return null;
                }

                StatusChanged?.Invoke(result.Msg);
                return result.Data;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ScheduleService] BatchAddPeriodicRulesAsync 异常: {ex.Message}");
                StatusChanged?.Invoke($"批量添加失败: {ex.Message}");
                return null;
            }
            finally
            {
                LoadingStateChanged?.Invoke(false);
            }
        }

        public async Task<bool> UpdatePeriodicRuleAsync(int ruleId, UpdatePeriodicRuleRequest request)
        {
            var config = _configService.Config;

            try
            {
                LoadingStateChanged?.Invoke(true);
                SetAuthHeader();

                var url = $"{config.ServerUrl}/desktop/periodic-rules/{ruleId}";
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return false;
                }

                var result = JsonSerializer.Deserialize<ScheduleApiResponse<object>>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result?.Code != 200)
                {
                    StatusChanged?.Invoke(result?.Msg ?? "更新失败");
                    return false;
                }

                StatusChanged?.Invoke("更新成功");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ScheduleService] UpdatePeriodicRuleAsync 异常: {ex.Message}");
                return false;
            }
            finally
            {
                LoadingStateChanged?.Invoke(false);
            }
        }

        public async Task<bool> DeletePeriodicRuleAsync(int ruleId)
        {
            var config = _configService.Config;

            try
            {
                LoadingStateChanged?.Invoke(true);
                SetAuthHeader();

                var url = $"{config.ServerUrl}/desktop/periodic-rules/{ruleId}";
                var response = await _httpClient.DeleteAsync(url);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return false;
                }

                var result = JsonSerializer.Deserialize<ScheduleApiResponse<object>>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result?.Code != 200)
                {
                    StatusChanged?.Invoke(result?.Msg ?? "删除失败");
                    return false;
                }

                StatusChanged?.Invoke("删除成功");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ScheduleService] DeletePeriodicRuleAsync 异常: {ex.Message}");
                return false;
            }
            finally
            {
                LoadingStateChanged?.Invoke(false);
            }
        }

        public async Task<List<TimeSlotModel>?> GetFixedTimeSlotsAsync()
        {
            var config = _configService.Config;

            try
            {
                SetAuthHeader();

                var url = $"{config.ServerUrl}/desktop/time-slots/fixed";
                var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var result = JsonSerializer.Deserialize<ScheduleApiResponse<List<TimeSlotModel>>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return result?.Data;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ScheduleService] GetFixedTimeSlotsAsync 异常: {ex.Message}");
                return null;
            }
        }

        public async Task<List<string>?> GetServiceTypesAsync()
        {
            var config = _configService.Config;

            try
            {
                var url = $"{config.ServerUrl}/desktop/service-types";
                var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var result = JsonSerializer.Deserialize<ScheduleApiResponse<List<string>>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return result?.Data;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ScheduleService] GetServiceTypesAsync 异常: {ex.Message}");
                return null;
            }
        }

        public async Task<List<TeacherModel>?> GetTeachersAsync(string? search = null)
        {
            var config = _configService.Config;

            try
            {
                SetAuthHeader();
                
                var url = $"{config.ServerUrl}/desktop/teachers";
                if (!string.IsNullOrEmpty(search))
                {
                    url += $"?search={Uri.EscapeDataString(search)}";
                }
                
                var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"[ScheduleService] GetTeachersAsync 失败: {response.StatusCode}");
                    return null;
                }

                var result = JsonSerializer.Deserialize<ScheduleApiResponse<List<TeacherModel>>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return result?.Data;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ScheduleService] GetTeachersAsync 异常: {ex.Message}");
                return null;
            }
        }
    }

    #region Response Models

    public class ScheduleApiResponse<T>
    {
        public int Code { get; set; }
        public string Msg { get; set; } = string.Empty;
        public T? Data { get; set; }
    }

    public class BatchAddResult
    {
        public int AddedCount { get; set; }
        public int SkippedCount { get; set; }
        public List<PeriodicRuleModel>? AddedRules { get; set; }
        public List<object>? SkippedRules { get; set; }
    }

    #endregion
}
