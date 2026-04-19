using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nexus.Models.Attendance;
using Nexus.Services.Http;

namespace Nexus.Services.Attendance;

public class AttendanceService : HttpService, IDisposable
{
    private List<TimeSlot> _timeSlots = new();
    private int _currentClassId;
    private int _currentTimeSlotId;
    private Timer? _monitorTimer;
    private bool _isMonitoring;
    private bool _disposed;

    public event EventHandler<AttendanceData>? AttendanceDataUpdated;
#pragma warning disable CS0067
    public event EventHandler<TimeSlot>? TimeSlotChanged;
    public event EventHandler? LeaveAttendanceTime;
#pragma warning restore CS0067
    public event EventHandler<string>? ErrorOccurred;

    public List<TimeSlot> TimeSlots => _timeSlots;
    public int CurrentTimeSlotId => _currentTimeSlotId;
    public int CurrentClassId => _currentClassId;
    public bool IsMonitoring => _isMonitoring;

    public AttendanceService(ConfigService configService, ToastService? toastService = null) 
        : base(configService, toastService)
    {
    }

    public void SetClassId(int classId)
    {
        if (_currentClassId != classId)
        {
            _currentClassId = classId;
        }
    }

    public async Task<List<TimeSlot>> GetTimeSlotsAsync()
    {
        var response = await GetAsync<List<TimeSlot>>(
            "/desktop/time-slots",
            new RequestOptions { OperationName = "获取时段列表" });

        if (response?.IsSuccess == true && response.Data != null)
        {
            _timeSlots = response.Data;
            return _timeSlots;
        }

        return new List<TimeSlot>();
    }

    public async Task<AttendanceData?> GetCurrentAttendanceAsync()
    {
        if (_currentClassId == 0)
        {
            return null;
        }

        var response = await GetAsync<AttendanceData>(
            $"/desktop/current?class_id={_currentClassId}",
            new RequestOptions { OperationName = "获取当前考勤", SuppressLogging = true });

        if (response?.IsSuccess == true && response.Data != null)
        {
            var data = response.Data;

            if (data.TimeSlots != null && data.TimeSlots.Count > 0)
            {
                _timeSlots = data.TimeSlots;
            }

            if (data.CurrentTimeSlot != null)
            {
                _currentTimeSlotId = data.CurrentTimeSlot.Id;
            }
            else
            {
                _currentTimeSlotId = 0;
            }

            AttendanceDataUpdated?.Invoke(this, data);
            return data;
        }

        return null;
    }

    public async Task<AttendanceData?> GetScheduleAsync(int timeSlotId, string? date = null)
    {
        var endpoint = $"/desktop/schedule?class_id={_currentClassId}&time_slot_id={timeSlotId}";
        if (!string.IsNullOrEmpty(date))
        {
            endpoint += $"&date={date}";
        }

        var response = await GetAsync<AttendanceData>(
            endpoint,
            new RequestOptions { OperationName = "获取排班信息" });

        if (response?.IsSuccess == true && response.Data != null)
        {
            _currentTimeSlotId = timeSlotId;
            AttendanceDataUpdated?.Invoke(this, response.Data);
            return response.Data;
        }

        return null;
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
    }

    public void StopMonitoring()
    {
        _isMonitoring = false;
        _monitorTimer?.Dispose();
        _monitorTimer = null;
    }

    private void CheckTimeSlot(object? state)
    {
        _ = CheckTimeSlotAsync();
    }

    private async Task CheckTimeSlotAsync()
    {
        try
        {
            await GetCurrentAttendanceAsync();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"时段检查失败: {ex.Message}");
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

    public new void Dispose()
    {
        if (_disposed) return;

        StopMonitoring();
        base.Dispose();
        _disposed = true;

        GC.SuppressFinalize(this);
    }
}
