using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexus.Models.Widget;
using Nexus.Models.Attendance;
using Nexus.Services.Attendance;

namespace Nexus.ViewModels.Widget.Cards
{
    public partial class AttendanceCardViewModel : ObservableObject
    {
        private readonly AttendanceCardModel _model;
        private readonly AttendanceService? _attendanceService;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _selectedTimeSlotName = "";

        [ObservableProperty]
        private int _selectedTimeSlotId;

        [ObservableProperty]
        private bool _showTimeSlotSelector;

        private TimeSlot? _selectedTimeSlot;
        public TimeSlot? SelectedTimeSlot
        {
            get => _selectedTimeSlot;
            set
            {
                if (SetProperty(ref _selectedTimeSlot, value) && value != null)
                {
                    _ = SelectTimeSlotAsync(value.Id);
                }
            }
        }

        public AttendanceCardViewModel(AttendanceCardModel model, AttendanceService? attendanceService = null)
        {
            _model = model;
            _attendanceService = attendanceService;

            if (_attendanceService != null)
            {
                _attendanceService.AttendanceDataUpdated += OnAttendanceDataUpdated;
                _attendanceService.TimeSlotChanged += OnTimeSlotChanged;
                _attendanceService.LeaveAttendanceTime += OnLeaveAttendanceTime;
            }

            UpdateSelectedTimeSlot();
        }

        public int TotalCount => _model.TotalCount;
        public int PresentCount => _model.PresentCount;
        public int AbsentCount => _model.AbsentCount;
        public int LeaveCount => _model.LeaveCount;
        public double AttendanceRate => _model.AttendanceRate;
        public string AttendanceRateDisplay => $"{_model.AttendanceRate:F2}%";
        public double ProgressWidth => _model.ProgressWidth;
        public bool IsAttendanceTime => _model.IsAttendanceTime;
        public bool Completed => _model.Completed;
        public string? SpecialStatus => _model.SpecialStatus;
        public string CancelReason => _model.CancelReason;
        public string Message => _model.Message;
        public string ClassName => _model.ClassName;
        public string Grade => _model.Grade;
        public int ScheduleId => _model.ScheduleId;
        public int TimeSlotId => _model.TimeSlotId;
        public string TimeSlotName => _model.TimeSlotName;
        public string StatusDisplay => _model.StatusDisplay;
        public string StatusColor => _model.StatusColor;
        public ObservableCollection<TimeSlot> TimeSlots { get; } = new();

        public bool ShowAttendanceData => _model.ScheduleId > 0 && string.IsNullOrEmpty(_model.SpecialStatus);
        public bool ShowNonAttendanceMessage => !_model.IsAttendanceTime && _model.ScheduleId == 0;
        public bool ShowNoScheduleMessage => _model.IsAttendanceTime && _model.ScheduleId == 0 && string.IsNullOrEmpty(_model.SpecialStatus);
        public bool ShowCancelledMessage => _model.SpecialStatus == "cancelled";
        public bool ShowReplacedMessage => _model.SpecialStatus == "replaced";
        public bool ShowMakeupMessage => _model.SpecialStatus == "makeup";
        public bool ShowDetailButton => _model.ScheduleId > 0;
        public bool HasMultipleTimeSlots => TimeSlots.Count > 1;

        private void UpdateSelectedTimeSlot()
        {
            SelectedTimeSlotName = _model.TimeSlotName;
            SelectedTimeSlotId = _model.TimeSlotId;

            TimeSlots.Clear();
            foreach (var slot in _model.TimeSlots)
            {
                TimeSlots.Add(slot);
            }

            if (_model.TimeSlotId > 0)
            {
                _selectedTimeSlot = _model.TimeSlots.FirstOrDefault(t => t.Id == _model.TimeSlotId);
                OnPropertyChanged(nameof(SelectedTimeSlot));
            }

            OnPropertyChanged(nameof(HasMultipleTimeSlots));
        }

        private void OnAttendanceDataUpdated(object? sender, AttendanceData data)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                UpdateFromAttendanceData(data);
            });
        }

        private void OnTimeSlotChanged(object? sender, TimeSlot slot)
        {
            Debug.WriteLine($"[AttendanceCardViewModel] 时段变化: {slot.Name}");
            _ = RefreshDataAsync();
        }

        private void OnLeaveAttendanceTime(object? sender, EventArgs e)
        {
            Debug.WriteLine("[AttendanceCardViewModel] 离开考勤时段");
        }

        public void UpdateFromAttendanceData(AttendanceData data)
        {
            if (data == null) return;

            _model.UpdateFromAttendanceData(data);
            UpdateSelectedTimeSlot();
            RefreshAllProperties();
        }

        private void RefreshAllProperties()
        {
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(PresentCount));
            OnPropertyChanged(nameof(AbsentCount));
            OnPropertyChanged(nameof(LeaveCount));
            OnPropertyChanged(nameof(AttendanceRate));
            OnPropertyChanged(nameof(AttendanceRateDisplay));
            OnPropertyChanged(nameof(ProgressWidth));
            OnPropertyChanged(nameof(IsAttendanceTime));
            OnPropertyChanged(nameof(Completed));
            OnPropertyChanged(nameof(SpecialStatus));
            OnPropertyChanged(nameof(CancelReason));
            OnPropertyChanged(nameof(Message));
            OnPropertyChanged(nameof(ClassName));
            OnPropertyChanged(nameof(Grade));
            OnPropertyChanged(nameof(TimeSlotName));
            OnPropertyChanged(nameof(StatusDisplay));
            OnPropertyChanged(nameof(StatusColor));
            OnPropertyChanged(nameof(ShowAttendanceData));
            OnPropertyChanged(nameof(ShowNonAttendanceMessage));
            OnPropertyChanged(nameof(ShowNoScheduleMessage));
            OnPropertyChanged(nameof(ShowCancelledMessage));
            OnPropertyChanged(nameof(ShowReplacedMessage));
            OnPropertyChanged(nameof(ShowMakeupMessage));
            OnPropertyChanged(nameof(ShowDetailButton));
        }

        public void UpdateModel(AttendanceCardModel model)
        {
            _model.TotalCount = model.TotalCount;
            _model.PresentCount = model.PresentCount;
            _model.AbsentCount = model.AbsentCount;
            _model.LeaveCount = model.LeaveCount;
            _model.AttendanceRate = model.AttendanceRate;
            _model.StatisticsDate = model.StatisticsDate;

            RefreshAllProperties();
        }

        [RelayCommand]
        private async Task RefreshDataAsync()
        {
            if (_attendanceService == null || IsLoading) return;

            IsLoading = true;
            try
            {
                await _attendanceService.RefreshAsync();
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task SelectTimeSlotAsync(int timeSlotId)
        {
            if (_attendanceService == null || timeSlotId == SelectedTimeSlotId) return;

            IsLoading = true;
            try
            {
                await _attendanceService.GetScheduleAsync(timeSlotId);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void OpenDetail()
        {
            if (ScheduleId <= 0) return;
            Debug.WriteLine($"[AttendanceCardViewModel] 打开考勤详情: ScheduleId={ScheduleId}");
        }

        public void Cleanup()
        {
            if (_attendanceService != null)
            {
                _attendanceService.AttendanceDataUpdated -= OnAttendanceDataUpdated;
                _attendanceService.TimeSlotChanged -= OnTimeSlotChanged;
                _attendanceService.LeaveAttendanceTime -= OnLeaveAttendanceTime;
            }
        }
    }
}
