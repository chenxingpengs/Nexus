using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Nexus.Models.Attendance;

namespace Nexus.Models.Widget
{
    public class AttendanceCardModel : WidgetCard
    {
        private int _totalCount;
        private int _presentCount;
        private int _absentCount;
        private int _leaveCount;
        private double _attendanceRate;
        private DateTime _statisticsDate;
        private int _scheduleId;
        private int _classId;
        private string _className = "";
        private string _grade = "";
        private int _timeSlotId;
        private string _timeSlotName = "";
        private bool _isAttendanceTime;
        private bool _completed;
        private string? _specialStatus;
        private string _cancelReason = "";
        private string _message = "";
        private List<TimeSlot> _timeSlots = new();
        private MakeupInfo? _makeupInfo;

        public AttendanceCardModel()
        {
            Type = CardType.Attendance;
            Title = "今日考勤";
        }

        public int TotalCount
        {
            get => _totalCount;
            set => SetProperty(ref _totalCount, value);
        }

        public int PresentCount
        {
            get => _presentCount;
            set => SetProperty(ref _presentCount, value);
        }

        public int AbsentCount
        {
            get => _absentCount;
            set => SetProperty(ref _absentCount, value);
        }

        public int LeaveCount
        {
            get => _leaveCount;
            set => SetProperty(ref _leaveCount, value);
        }

        public double AttendanceRate
        {
            get => _attendanceRate;
            set => SetProperty(ref _attendanceRate, value);
        }

        public DateTime StatisticsDate
        {
            get => _statisticsDate;
            set => SetProperty(ref _statisticsDate, value);
        }

        public int ScheduleId
        {
            get => _scheduleId;
            set => SetProperty(ref _scheduleId, value);
        }

        public int ClassId
        {
            get => _classId;
            set => SetProperty(ref _classId, value);
        }

        public string ClassName
        {
            get => _className;
            set => SetProperty(ref _className, value);
        }

        public string Grade
        {
            get => _grade;
            set => SetProperty(ref _grade, value);
        }

        public int TimeSlotId
        {
            get => _timeSlotId;
            set => SetProperty(ref _timeSlotId, value);
        }

        public string TimeSlotName
        {
            get => _timeSlotName;
            set => SetProperty(ref _timeSlotName, value);
        }

        public bool IsAttendanceTime
        {
            get => _isAttendanceTime;
            set => SetProperty(ref _isAttendanceTime, value);
        }

        public bool Completed
        {
            get => _completed;
            set => SetProperty(ref _completed, value);
        }

        public string? SpecialStatus
        {
            get => _specialStatus;
            set => SetProperty(ref _specialStatus, value);
        }

        public string CancelReason
        {
            get => _cancelReason;
            set => SetProperty(ref _cancelReason, value);
        }

        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        public List<TimeSlot> TimeSlots
        {
            get => _timeSlots;
            set => SetProperty(ref _timeSlots, value);
        }

        public MakeupInfo? MakeupInfo
        {
            get => _makeupInfo;
            set => SetProperty(ref _makeupInfo, value);
        }

        public double ProgressWidth => AttendanceRate * 2;

        public string StatusDisplay
        {
            get
            {
                if (!string.IsNullOrEmpty(SpecialStatus))
                {
                    return SpecialStatus switch
                    {
                        "cancelled" => "已停课",
                        "replaced" => "已调课",
                        "makeup" => "补课",
                        _ => SpecialStatus
                    };
                }
                return Completed ? "已完成" : "待考勤";
            }
        }

        public string StatusColor
        {
            get
            {
                if (!string.IsNullOrEmpty(SpecialStatus))
                {
                    return SpecialStatus switch
                    {
                        "cancelled" => "#F44336",
                        "replaced" => "#FF9800",
                        "makeup" => "#2196F3",
                        _ => "#757575"
                    };
                }
                return Completed ? "#4CAF50" : "#FF9800";
            }
        }

        public string AttendanceRateDisplay => $"{AttendanceRate:F2}%";

        public bool ShowNonAttendanceMessage => !IsAttendanceTime && ScheduleId == 0;

        public bool ShowNoScheduleMessage => IsAttendanceTime && ScheduleId == 0 && string.IsNullOrEmpty(SpecialStatus);

        public bool ShowCancelledMessage => SpecialStatus == "cancelled";

        public bool ShowReplacedMessage => SpecialStatus == "replaced";

        public bool ShowMakeupMessage => SpecialStatus == "makeup";

        public bool ShowAttendanceData => ScheduleId > 0 && string.IsNullOrEmpty(SpecialStatus);

        public void UpdateFromAttendanceData(AttendanceData data)
        {
            if (data == null) return;

            IsAttendanceTime = data.IsAttendanceTime;
            TimeSlots = data.TimeSlots ?? new List<TimeSlot>();
            SpecialStatus = data.SpecialStatus;
            CancelReason = data.CancelReason ?? "";
            Message = data.Message ?? "";
            MakeupInfo = data.MakeupInfo;

            if (data.CurrentTimeSlot != null)
            {
                TimeSlotId = data.CurrentTimeSlot.Id;
                TimeSlotName = data.CurrentTimeSlot.Name;
            }

            if (data.Schedule != null)
            {
                ScheduleId = data.Schedule.Id;
                ClassId = data.Schedule.ClassId;
                ClassName = data.Schedule.ClassName;
                Grade = data.Schedule.Grade;
                TimeSlotId = data.Schedule.TimeSlotId;
                TimeSlotName = data.Schedule.TimeSlotName;
                TotalCount = data.Schedule.ShouldAttend;
                PresentCount = data.Schedule.ActualAttend;
                LeaveCount = data.Schedule.LeaveCount;
                AbsentCount = data.Schedule.AbsentCount;
                Completed = data.Schedule.Completed;
                AttendanceRate = data.Schedule.AttendanceRate;
            }
            else
            {
                ScheduleId = 0;
                TotalCount = 0;
                PresentCount = 0;
                LeaveCount = 0;
                AbsentCount = 0;
                Completed = false;
                AttendanceRate = 0;
            }

            OnPropertyChanged(nameof(StatusDisplay));
            OnPropertyChanged(nameof(StatusColor));
            OnPropertyChanged(nameof(ProgressWidth));
            OnPropertyChanged(nameof(AttendanceRateDisplay));
            OnPropertyChanged(nameof(ShowNonAttendanceMessage));
            OnPropertyChanged(nameof(ShowNoScheduleMessage));
            OnPropertyChanged(nameof(ShowCancelledMessage));
            OnPropertyChanged(nameof(ShowReplacedMessage));
            OnPropertyChanged(nameof(ShowMakeupMessage));
            OnPropertyChanged(nameof(ShowAttendanceData));
        }
    }
}
