using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Nexus.Models.Attendance;
using Nexus.Services;

namespace Nexus.Services.Attendance
{
    public class AttendanceService : IDisposable
    {
        private readonly ConfigService _configService;
        private readonly HttpClient _httpClient;
        private List<TimeSlot> _timeSlots = new();
        private int _currentClassId;
        private int _currentTimeSlotId;
        private Timer? _monitorTimer;
        private bool _isMonitoring;
        private bool _disposed;

        public event EventHandler<AttendanceData>? AttendanceDataUpdated;
        public event EventHandler<TimeSlot>? TimeSlotChanged;
        public event EventHandler? LeaveAttendanceTime;
        public event EventHandler<string>? ErrorOccurred;

        public List<TimeSlot> TimeSlots => _timeSlots;
        public int CurrentTimeSlotId => _currentTimeSlotId;
        public int CurrentClassId => _currentClassId;
        public bool IsMonitoring => _isMonitoring;

        public AttendanceService(ConfigService configService)
        {
            _configService = configService;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
        }

        public void SetClassId(int classId)
        {
            if (_currentClassId != classId)
            {
                _currentClassId = classId;
                Debug.WriteLine($"[AttendanceService] 设置班级ID: {classId}");
            }
        }

        public async Task<List<TimeSlot>> GetTimeSlotsAsync()
        {
            try
            {
                var baseUrl = _configService.GetServerUrl();
                var token = _configService.GetAccessToken();

                if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(token))
                {
                    Debug.WriteLine("[AttendanceService] 缺少服务器URL或Token");
                    return new List<TimeSlot>();
                }

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                var response = await _httpClient.GetAsync($"{baseUrl}/desktop/attendance/time-slots");
                var json = await response.Content.ReadAsStringAsync();

                Debug.WriteLine($"[AttendanceService] 时段列表响应: {json}");

                var apiResponse = JsonSerializer.Deserialize<AttendanceApiResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Code == 200 && apiResponse.Data?.TimeSlots != null)
                {
                    _timeSlots = apiResponse.Data.TimeSlots;
                    Debug.WriteLine($"[AttendanceService] 获取到 {_timeSlots.Count} 个时段");
                    return _timeSlots;
                }

                return new List<TimeSlot>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AttendanceService] 获取时段列表失败: {ex.Message}");
                ErrorOccurred?.Invoke(this, $"获取时段列表失败: {ex.Message}");
                return new List<TimeSlot>();
            }
        }

        public async Task<AttendanceData?> GetCurrentAttendanceAsync()
        {
            try
            {
                var baseUrl = _configService.GetServerUrl();
                var token = _configService.GetAccessToken();

                if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(token))
                {
                    Debug.WriteLine("[AttendanceService] 缺少服务器URL或Token");
                    return null;
                }

                if (_currentClassId == 0)
                {
                    Debug.WriteLine("[AttendanceService] 未设置班级ID");
                    return null;
                }

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                var response = await _httpClient.GetAsync($"{baseUrl}/desktop/attendance/current?class_id={_currentClassId}");
                var json = await response.Content.ReadAsStringAsync();

                Debug.WriteLine($"[AttendanceService] 当前考勤响应: {json}");

                var apiResponse = JsonSerializer.Deserialize<AttendanceApiResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Code == 200)
                {
                    var data = apiResponse.Data;
                    if (data?.CurrentTimeSlot != null)
                    {
                        _currentTimeSlotId = data.CurrentTimeSlot.Id;
                    }
                    AttendanceDataUpdated?.Invoke(this, data!);
                    return data;
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AttendanceService] 获取当前考勤数据失败: {ex.Message}");
                ErrorOccurred?.Invoke(this, $"获取考勤数据失败: {ex.Message}");
                return null;
            }
        }

        public async Task<AttendanceData?> GetScheduleAsync(int timeSlotId, string? date = null)
        {
            try
            {
                var baseUrl = _configService.GetServerUrl();
                var token = _configService.GetAccessToken();

                if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(token))
                {
                    return null;
                }

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                var url = $"{baseUrl}/desktop/attendance/schedule?class_id={_currentClassId}&time_slot_id={timeSlotId}";
                if (!string.IsNullOrEmpty(date))
                {
                    url += $"&date={date}";
                }

                var response = await _httpClient.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();

                Debug.WriteLine($"[AttendanceService] 排班响应: {json}");

                var apiResponse = JsonSerializer.Deserialize<AttendanceApiResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Code == 200)
                {
                    var data = apiResponse.Data;
                    _currentTimeSlotId = timeSlotId;
                    AttendanceDataUpdated?.Invoke(this, data!);
                    return data;
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AttendanceService] 获取排班信息失败: {ex.Message}");
                ErrorOccurred?.Invoke(this, $"获取排班信息失败: {ex.Message}");
                return null;
            }
        }

        public TimeSlot? GetCurrentTimeSlot()
        {
            var now = DateTime.Now.TimeOfDay;
            foreach (var slot in _timeSlots)
            {
                if (slot.StartTimeSpan <= now && slot.EndTimeSpan >= now)
                {
                    return slot;
                }
            }
            return null;
        }

        public void StartMonitoring()
        {
            if (_isMonitoring) return;

            _isMonitoring = true;
            _monitorTimer = new Timer(CheckTimeSlot, null, 0, 60000);
            Debug.WriteLine("[AttendanceService] 时段监控已启动");
        }

        public void StopMonitoring()
        {
            _isMonitoring = false;
            _monitorTimer?.Dispose();
            _monitorTimer = null;
            Debug.WriteLine("[AttendanceService] 时段监控已停止");
        }

        private void CheckTimeSlot(object? state)
        {
            try
            {
                var currentSlot = GetCurrentTimeSlot();

                if (currentSlot != null && currentSlot.Id != _currentTimeSlotId)
                {
                    Debug.WriteLine($"[AttendanceService] 进入新时段: {currentSlot.Name}");
                    _currentTimeSlotId = currentSlot.Id;
                    TimeSlotChanged?.Invoke(this, currentSlot);
                }
                else if (currentSlot == null && _currentTimeSlotId != 0)
                {
                    Debug.WriteLine("[AttendanceService] 离开考勤时段");
                    _currentTimeSlotId = 0;
                    LeaveAttendanceTime?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AttendanceService] 时段检查失败: {ex.Message}");
            }
        }

        public async Task RefreshAsync()
        {
            if (_currentTimeSlotId > 0)
            {
                await GetScheduleAsync(_currentTimeSlotId);
            }
            else
            {
                await GetCurrentAttendanceAsync();
            }
        }

        public async Task InitializeAsync(int classId)
        {
            SetClassId(classId);
            
            await GetTimeSlotsAsync();
            
            var currentSlot = GetCurrentTimeSlot();
            if (currentSlot != null)
            {
                _currentTimeSlotId = currentSlot.Id;
            }
            
            await GetCurrentAttendanceAsync();
            
            StartMonitoring();
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            StopMonitoring();
            _httpClient.Dispose();
            _disposed = true;
            
            GC.SuppressFinalize(this);
        }
    }
}
