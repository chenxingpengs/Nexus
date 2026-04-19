using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexus.Services;

namespace Nexus.ViewModels.Widget
{
    public partial class AttendanceDetailViewModel : ObservableObject
    {
        private readonly ConfigService _configService;
        private readonly HttpClient _httpClient;
        private int _scheduleId;
        private int _classId;
        private string? _editingStudentId;

        [ObservableProperty]
        private string _className = "";

        [ObservableProperty]
        private string _grade = "";

        [ObservableProperty]
        private string _timeSlotName = "";

        [ObservableProperty]
        private string _attendanceDate = "";

        [ObservableProperty]
        private int _shouldAttend;

        [ObservableProperty]
        private string _shouldAttendInput = "";

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

        [ObservableProperty]
        private bool _showAddStudentDialog;

        [ObservableProperty]
        private string _dialogTitle = "添加学生";

        [ObservableProperty]
        private string _dialogConfirmText = "确认添加";

        [ObservableProperty]
        private string _dialogStatus = "leave";

        [ObservableProperty]
        private string _dialogStudentName = "";

        [ObservableProperty]
        private string _dialogLeaveReason = "";

        [ObservableProperty]
        private string _errorMessage = "";

        [ObservableProperty]
        private bool _hasError;

        public ObservableCollection<StudentItem> StudentList { get; } = new();

        public ObservableCollection<TeacherItem> Teachers { get; } = new();

        [ObservableProperty]
        private TeacherItem? _selectedTeacher;

        public bool HasStudents => StudentList.Count > 0;

        public bool IsLeaveStatus => DialogStatus == "leave";

        public string StatusDescription => DialogStatus == "leave"
            ? "学生已请假且不在校"
            : "学生未请假且不在校内";

        public IBrush LeaveSelectedBackground => DialogStatus == "leave"
            ? new SolidColorBrush(Color.Parse("#E3F2FD"))
            : new SolidColorBrush(Colors.Transparent);

        public IBrush AbsentSelectedBackground => DialogStatus == "absent"
            ? new SolidColorBrush(Color.Parse("#FFEBEE"))
            : new SolidColorBrush(Colors.Transparent);
        public IBrush LeaveRadioBorder => DialogStatus == "leave"
            ? new SolidColorBrush(Color.Parse("#1976D2"))
            : new SolidColorBrush(Color.Parse("#BDBDBD"));
        public IBrush AbsentRadioBorder => DialogStatus == "absent"
            ? new SolidColorBrush(Color.Parse("#F44336"))
            : new SolidColorBrush(Color.Parse("#BDBDBD"));
        public IBrush LeaveRadioFill => DialogStatus == "leave"
            ? new SolidColorBrush(Color.Parse("#1976D2"))
            : new SolidColorBrush(Colors.Transparent);
        public IBrush AbsentRadioFill => DialogStatus == "absent"
            ? new SolidColorBrush(Color.Parse("#F44336"))
            : new SolidColorBrush(Colors.Transparent);

        public bool CanSubmit => !IsLoading && !string.IsNullOrWhiteSpace(ShouldAttendInput) && SelectedTeacher != null;

        public event EventHandler? Saved;
        public event EventHandler? Cancelled;

        public AttendanceDetailViewModel(ConfigService configService, int scheduleId)
        {
            _configService = configService;
            _scheduleId = scheduleId;
            _httpClient = new HttpClient();
        }

        public void SetDialogStatus(string status)
        {
            DialogStatus = status;
            OnPropertyChanged(nameof(IsLeaveStatus));
            OnPropertyChanged(nameof(StatusDescription));
            OnPropertyChanged(nameof(LeaveSelectedBackground));
            OnPropertyChanged(nameof(AbsentSelectedBackground));
            OnPropertyChanged(nameof(LeaveRadioBorder));
            OnPropertyChanged(nameof(AbsentRadioBorder));
            OnPropertyChanged(nameof(LeaveRadioFill));
            OnPropertyChanged(nameof(AbsentRadioFill));
        }
        partial void OnShouldAttendInputChanged(string value)
        {
            if (int.TryParse(value, out int shouldAttend))
            {
                ShouldAttend = shouldAttend;
            }
            UpdateActualAttend();
            OnPropertyChanged(nameof(CanSubmit));
        }
        private void UpdateActualAttend()
        {
            ActualAttend = Math.Max(0, ShouldAttend - LeaveCount - AbsentCount);
        }
        public async Task LoadDataAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            HasError = false;
            ErrorMessage = "";
            try
            {
                var baseUrl = _configService.GetServerUrl();
                var token = _configService.GetAccessToken();
                if (string.IsNullOrEmpty(token))
                {
                    HasError = true;
                    ErrorMessage = "未配置设备Token";
                    return;
                }
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                var detailUrl = $"{baseUrl}/desktop/detail?schedule_id={_scheduleId}";
                var detailResponse = await _httpClient.GetAsync(detailUrl);
                var detailContent = await detailResponse.Content.ReadAsStringAsync();
                Debug.WriteLine($"[AttendanceDetail] 响应: {detailContent}");
                if (!detailResponse.IsSuccessStatusCode)
                {
                    HasError = true;
                    ErrorMessage = $"获取考勤详情失败: {detailResponse.StatusCode}";
                    return;
                }
                var detailResult = JsonSerializer.Deserialize<AttendanceDetailResponse>(detailContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (detailResult?.Code != 200 || detailResult.Data == null)
                {
                    HasError = true;
                    ErrorMessage = detailResult?.Msg ?? "获取考勤详情失败";
                    return;
                }
                var data = detailResult.Data;
                _classId = data.ClassId;
                ClassName = data.ClassName;
                Grade = data.Grade;
                TimeSlotName = data.TimeSlotName;
                AttendanceDate = data.AttendanceDate;
                ShouldAttend = data.ShouldAttend;
                ShouldAttendInput = data.ShouldAttend.ToString();
                ActualAttend = data.ActualAttend;
                LeaveCount = data.LeaveCount;
                AbsentCount = data.AbsentCount;
                Notes = data.Notes ?? "";
                StudentList.Clear();
                if (data.LeaveStudents != null)
                {
                    foreach (var student in data.LeaveStudents)
                    {
                        StudentList.Add(new StudentItem
                        {
                            Id = Guid.NewGuid().ToString(),
                            StudentName = student.StudentName,
                            Status = "leave",
                            Reason = student.Reason ?? ""
                        });
                    }
                }
                if (data.AbsentStudents != null)
                {
                    foreach (var student in data.AbsentStudents)
                    {
                        StudentList.Add(new StudentItem
                        {
                            Id = Guid.NewGuid().ToString(),
                            StudentName = student.StudentName,
                            Status = "absent",
                            Reason = student.Reason ?? ""
                        });
                    }
                }
                OnPropertyChanged(nameof(HasStudents));
                var teachersUrl = $"{baseUrl}/desktop/teachers?class_id={_classId}";
                var teachersResponse = await _httpClient.GetAsync(teachersUrl);
                var teachersContent = await teachersResponse.Content.ReadAsStringAsync();
                Debug.WriteLine($"[AttendanceDetail] 教师响应: {teachersContent}");
                
                if (teachersResponse.IsSuccessStatusCode)
                {
                    var teachersResult = JsonSerializer.Deserialize<TeachersResponse>(teachersContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    Teachers.Clear();
                    if (teachersResult?.Code == 200 && teachersResult.Data != null)
                    {
                        foreach (var teacher in teachersResult.Data)
                        {
                            if (teacher.Id > 0 && !string.IsNullOrWhiteSpace(teacher.Name))
                            {
                                Debug.WriteLine($"[AttendanceDetail] 教师: Id={teacher.Id}, Name={teacher.Name}, IsHeadTeacher={teacher.IsHeadTeacher}");
                                Teachers.Add(new TeacherItem
                                {
                                    Id = teacher.Id,
                                    Name = teacher.Name,
                                    IsHeadTeacher = teacher.IsHeadTeacher
                                });
                            }
                        }
                        var defaultTeacher = Teachers.FirstOrDefault(t => t.Id == data.TeacherId);
                        if (defaultTeacher == null && data.TeacherId > 0)
                        {
                            defaultTeacher = new TeacherItem
                            {
                                Id = data.TeacherId,
                                Name = data.TeacherName,
                                IsHeadTeacher = false
                            };
                            Teachers.Add(defaultTeacher);
                        }
                        SelectedTeacher = defaultTeacher ?? Teachers.FirstOrDefault();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AttendanceDetail] 加载失败: {ex.Message}");
                HasError = true;
                ErrorMessage = $"加载失败: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(CanSubmit));
            }
        }
        [RelayCommand]
        private void AddStudent()
        {
            _editingStudentId = null;
            DialogTitle = "添加学生";
            DialogConfirmText = "确认添加";
            DialogStatus = "leave";
            DialogStudentName = "";
            DialogLeaveReason = "";
            ShowAddStudentDialog = true;
            SetDialogStatus("leave");
        }
        [RelayCommand]
        private void EditStudent(StudentItem? student)
        {
            if (student == null) return;
            _editingStudentId = student.Id;
            DialogTitle = "编辑学生";
            DialogConfirmText = "确认修改";
            DialogStatus = student.Status;
            DialogStudentName = student.StudentName;
            DialogLeaveReason = student.Reason;
            ShowAddStudentDialog = true;
            SetDialogStatus(student.Status);
        }
        [RelayCommand]
        private void DeleteStudent(StudentItem? student)
        {
            if (student == null) return;
            StudentList.Remove(student);
            UpdateCounts();
            OnPropertyChanged(nameof(HasStudents));
        }

        [RelayCommand]
        private void SelectLeaveStatus()
        {
            SetDialogStatus("leave");
        }

        [RelayCommand]
        private void SelectAbsentStatus()
        {
            SetDialogStatus("absent");
        }

        [RelayCommand]
        private void CancelDialog()
        {
            ShowAddStudentDialog = false;
            _editingStudentId = null;
        }
        [RelayCommand]
        private void ConfirmDialog()
        {
            if (string.IsNullOrWhiteSpace(DialogStudentName))
            {
                return;
            }
            if (DialogStatus == "leave" && string.IsNullOrWhiteSpace(DialogLeaveReason))
            {
                return;
            }
            if (!string.IsNullOrEmpty(_editingStudentId))
            {
                var existing = StudentList.FirstOrDefault(s => s.Id == _editingStudentId);
                if (existing != null)
                {
                    existing.StudentName = DialogStudentName.Trim();
                    existing.Status = DialogStatus;
                    existing.Reason = DialogLeaveReason.Trim();
                    existing.OnStatusChanged();
                }
            }
            else
            {
                StudentList.Add(new StudentItem
                {
                    Id = Guid.NewGuid().ToString(),
                    StudentName = DialogStudentName.Trim(),
                    Status = DialogStatus,
                    Reason = DialogLeaveReason.Trim()
                });
            }
            UpdateCounts();
            OnPropertyChanged(nameof(HasStudents));
            ShowAddStudentDialog = false;
            _editingStudentId = null;
        }
        private void UpdateCounts()
        {
            LeaveCount = StudentList.Count(s => s.Status == "leave");
            AbsentCount = StudentList.Count(s => s.Status == "absent");
            UpdateActualAttend();
        }
        [RelayCommand]
        private void Cancel()
        {
            Cancelled?.Invoke(this, EventArgs.Empty);
        }
        [RelayCommand]
        private async Task Save()
        {
            if (IsLoading) return;
            if (SelectedTeacher == null)
            {
                HasError = true;
                ErrorMessage = "请选择考勤教师";
                return;
            }
            if (!int.TryParse(ShouldAttendInput, out int shouldAttend) || shouldAttend < 0)
            {
                HasError = true;
                ErrorMessage = "请输入有效的应到人数";
                return;
            }
            IsLoading = true;
            HasError = false;
            ErrorMessage = "";
            try
            {
                var baseUrl = _configService.GetServerUrl();
                var token = _configService.GetAccessToken();
                if (string.IsNullOrEmpty(token))
                {
                    HasError = true;
                    ErrorMessage = "未配置设备Token";
                    return;
                }
                var requestData = new SubmitAttendanceRequest
                {
                    ScheduleId = _scheduleId.ToString(),
                    AttendanceDate = AttendanceDate,
                    ShouldAttend = shouldAttend,
                    Notes = Notes,
                    TeacherId = SelectedTeacher.Id,
                    StudentDetails = StudentList.Select(s => new StudentDetail
                    {
                        StudentName = s.StudentName,
                        Status = s.Status,
                        LeaveReason = s.Reason
                    }).ToList()
                };
                var json = JsonSerializer.Serialize(requestData, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                var response = await _httpClient.PostAsync($"{baseUrl}/desktop/record", content);
                var responseContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[AttendanceDetail] 提交响应: {responseContent}");
                var result = JsonSerializer.Deserialize<BaseResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (result?.Code != 200)
                {
                    HasError = true;
                    ErrorMessage = result?.Msg ?? "提交失败";
                    return;
                }
                Saved?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AttendanceDetail] 提交失败: {ex.Message}");
                HasError = true;
                ErrorMessage = $"提交失败: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
    public partial class StudentItem : ObservableObject
    {
        public string Id { get; set; } = "";
        [ObservableProperty]
        private string _studentName = "";

        [ObservableProperty]
        private string _status = "";
        [ObservableProperty]
        private string _reason = "";
        public string StatusText => Status == "leave" ? "请假" : "缺勤";
        public string StatusBackground => Status == "leave" ? "#FF9800" : "#F44336";
        public string ReasonDisplay => string.IsNullOrWhiteSpace(Reason) ? "" : $"({Reason})";
        public void OnStatusChanged()
        {
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusBackground));
            OnPropertyChanged(nameof(ReasonDisplay));
        }
    }
    public partial class TeacherItem : ObservableObject
    {
        public int Id { get; set; }
        [ObservableProperty]
        private string _name = "";
        [ObservableProperty]
        private bool _isHeadTeacher;
    }
    public class AttendanceDetailResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }
        [JsonPropertyName("msg")]
        public string Msg { get; set; } = "";
        [JsonPropertyName("data")]
        public AttendanceDetailData? Data { get; set; }
    }
    public class AttendanceDetailData
    {
        [JsonPropertyName("scheduleId")]
        public string ScheduleId { get; set; } = "";
        [JsonPropertyName("classId")]
        public int ClassId { get; set; }
        [JsonPropertyName("className")]
        public string ClassName { get; set; } = "";
        [JsonPropertyName("grade")]
        public string Grade { get; set; } = "";
        [JsonPropertyName("timeSlotId")]
        public int TimeSlotId { get; set; }
        [JsonPropertyName("timeSlotName")]
        public string TimeSlotName { get; set; } = "";
        [JsonPropertyName("attendanceDate")]
        public string AttendanceDate { get; set; } = "";
        [JsonPropertyName("shouldAttend")]
        public int ShouldAttend { get; set; }
        [JsonPropertyName("actualAttend")]
        public int ActualAttend { get; set; }
        [JsonPropertyName("leaveCount")]
        public int LeaveCount { get; set; }
        [JsonPropertyName("absentCount")]
        public int AbsentCount { get; set; }
        [JsonPropertyName("notes")]
        public string Notes { get; set; } = "";
        [JsonPropertyName("teacherId")]
        public int TeacherId { get; set; }
        [JsonPropertyName("teacherName")]
        public string TeacherName { get; set; } = "";
        [JsonPropertyName("leaveStudents")]
        public List<StudentException>? LeaveStudents { get; set; }
        [JsonPropertyName("absentStudents")]
        public List<StudentException>? AbsentStudents { get; set; }
        [JsonPropertyName("completed")]
        public bool Completed { get; set; }
    }
    public class StudentException
    {
        [JsonPropertyName("studentId")]
        public string StudentId { get; set; } = "";
        [JsonPropertyName("studentName")]
        public string StudentName { get; set; } = "";
        [JsonPropertyName("reason")]
        public string Reason { get; set; } = "";
    }
    public class TeachersResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }
        [JsonPropertyName("msg")]
        public string Msg { get; set; } = "";
        [JsonPropertyName("data")]
        public List<TeacherData>? Data { get; set; }
    }
    public class TeacherData
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonPropertyName("isHeadTeacher")]
        public bool IsHeadTeacher { get; set; }
    }
    public class SubmitAttendanceRequest
    {
        public string ScheduleId { get; set; } = "";
        public string AttendanceDate { get; set; } = "";
        public int ShouldAttend { get; set; }
        public string Notes { get; set; } = "";
        public int TeacherId { get; set; }
        public List<StudentDetail> StudentDetails { get; set; } = new();
    }
    public class StudentDetail
    {
        public string StudentName { get; set; } = "";
        public string Status { get; set; } = "";
        public string LeaveReason { get; set; } = "";
    }
    public class BaseResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }
        [JsonPropertyName("msg")]
        public string Msg { get; set; } = "";
    }
}
