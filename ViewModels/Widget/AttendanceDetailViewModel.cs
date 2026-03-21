using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexus.Services;
using Nexus.Services.Attendance;

namespace Nexus.ViewModels.Widget
{
    public class StudentAttendanceItem : ObservableObject
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = "";
        
        private string _status = "";
        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }
        
        private string _reason = "";
        public string Reason
        {
            get => _reason;
            set => SetProperty(ref _reason, value);
        }
    }

    public partial class AttendanceDetailViewModel : ObservableObject
    {
        private readonly ConfigService _configService;
        private readonly int _scheduleId;
        private readonly HttpClient _httpClient;

        [ObservableProperty]
        private string _className = "";

        [ObservableProperty]
        private string _timeSlotName = "";

        [ObservableProperty]
        private string _attendanceDate = "";

        [ObservableProperty]
        private int _shouldAttend;

        [ObservableProperty]
        private int _actualAttend;

        [ObservableProperty]
        private int _leaveCount;

        [ObservableProperty]
        private int _absentCount;

        [ObservableProperty]
        private string _notes = "";

        [ObservableProperty]
        private bool _isLoading;

        public ObservableCollection<StudentAttendanceItem> LeaveStudents { get; } = new();
        public ObservableCollection<StudentAttendanceItem> AbsentStudents { get; } = new();

        public bool HasLeaveStudents => LeaveStudents.Count > 0;
        public bool HasAbsentStudents => AbsentStudents.Count > 0;

        public event EventHandler? Saved;
        public event EventHandler? Cancelled;

        public AttendanceDetailViewModel(ConfigService configService, int scheduleId)
        {
            _configService = configService;
            _scheduleId = scheduleId;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        }

        public async Task LoadDataAsync()
        {
            IsLoading = true;
            try
            {
                var baseUrl = _configService.GetServerUrl();
                var token = _configService.GetAccessToken();

                if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(token))
                {
                    Debug.WriteLine("[AttendanceDetailViewModel] 缺少服务器URL或Token");
                    return;
                }

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                var response = await _httpClient.GetAsync($"{baseUrl}/desktop/attendance/detail?schedule_id={_scheduleId}");
                var json = await response.Content.ReadAsStringAsync();

                Debug.WriteLine($"[AttendanceDetailViewModel] 详情响应: {json}");

                var apiResponse = JsonSerializer.Deserialize<JsonElement>(json);
                
                if (apiResponse.TryGetProperty("code", out var codeEl) && codeEl.GetInt32() == 200)
                {
                    if (apiResponse.TryGetProperty("data", out var data))
                    {
                        ClassName = data.TryGetProperty("className", out var cn) ? cn.GetString() ?? "" : "";
                        TimeSlotName = data.TryGetProperty("timeSlotName", out var tsn) ? tsn.GetString() ?? "" : "";
                        AttendanceDate = data.TryGetProperty("attendanceDate", out var ad) ? ad.GetString() ?? "" : "";
                        ShouldAttend = data.TryGetProperty("shouldAttend", out var sa) ? sa.GetInt32() : 0;
                        ActualAttend = data.TryGetProperty("actualAttend", out var aa) ? aa.GetInt32() : 0;
                        LeaveCount = data.TryGetProperty("leaveCount", out var lc) ? lc.GetInt32() : 0;
                        AbsentCount = data.TryGetProperty("absentCount", out var ac) ? ac.GetInt32() : 0;
                        Notes = data.TryGetProperty("notes", out var n) ? n.GetString() ?? "" : "";

                        LeaveStudents.Clear();
                        AbsentStudents.Clear();

                        if (data.TryGetProperty("leaveStudents", out var ls) && ls.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in ls.EnumerateArray())
                            {
                                LeaveStudents.Add(new StudentAttendanceItem
                                {
                                    StudentId = item.TryGetProperty("studentId", out var sid) ? sid.GetInt32() : 0,
                                    StudentName = item.TryGetProperty("studentName", out var sn) ? (sn.GetString() ?? "") : "",
                                    Reason = item.TryGetProperty("reason", out var r) ? (r.GetString() ?? "") : "",
                                    Status = "leave"
                                });
                            }
                        }

                        if (data.TryGetProperty("absentStudents", out var abs) && abs.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in abs.EnumerateArray())
                            {
                                AbsentStudents.Add(new StudentAttendanceItem
                                {
                                    StudentId = item.TryGetProperty("studentId", out var sid) ? sid.GetInt32() : 0,
                                    StudentName = item.TryGetProperty("studentName", out var sn) ? (sn.GetString() ?? "") : "",
                                    Status = "absent"
                                });
                            }
                        }

                        OnPropertyChanged(nameof(HasLeaveStudents));
                        OnPropertyChanged(nameof(HasAbsentStudents));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AttendanceDetailViewModel] 加载数据失败: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            Cancelled?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private async Task Save()
        {
            IsLoading = true;
            try
            {
                var baseUrl = _configService.GetServerUrl();
                var token = _configService.GetAccessToken();

                if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(token))
                {
                    Debug.WriteLine("[AttendanceDetailViewModel] 缺少服务器URL或Token");
                    return;
                }

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                var requestData = new
                {
                    scheduleId = _scheduleId,
                    attendanceDate = AttendanceDate,
                    shouldAttend = ShouldAttend,
                    notes = Notes,
                    studentDetails = new System.Collections.Generic.List<object>()
                };

                foreach (var student in LeaveStudents)
                {
                    requestData.studentDetails.Add(new
                    {
                        studentId = student.StudentId,
                        studentName = student.StudentName,
                        status = "leave",
                        leaveReason = student.Reason
                    });
                }

                foreach (var student in AbsentStudents)
                {
                    requestData.studentDetails.Add(new
                    {
                        studentId = student.StudentId,
                        studentName = student.StudentName,
                        status = "absent",
                        leaveReason = ""
                    });
                }

                var json = JsonSerializer.Serialize(requestData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{baseUrl}/desktop/attendance/record", content);
                var responseJson = await response.Content.ReadAsStringAsync();

                Debug.WriteLine($"[AttendanceDetailViewModel] 保存响应: {responseJson}");

                var apiResponse = JsonSerializer.Deserialize<JsonElement>(responseJson);
                if (apiResponse.TryGetProperty("code", out var codeEl) && codeEl.GetInt32() == 200)
                {
                    Saved?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AttendanceDetailViewModel] 保存失败: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
