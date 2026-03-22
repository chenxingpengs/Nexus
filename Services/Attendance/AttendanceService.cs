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
    public event EventHandler<TimeSlot>? TimeSlotChanged;
    public event EventHandler? LeaveAttendanceTime;
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
            Debug.WriteLine($"[AttendanceService] 设置班级ID: {classId}");
        }
    }

    public async Task<List<TimeSlot>> GetTimeSlotsAsync()
    {
        Debug.WriteLine($"[AttendanceService] GetTimeSlotsAsync 开始");

        var response = await GetAsync<List<TimeSlot>>(
            "/desktop/time-slots",
            new RequestOptions { OperationName = "获取时段列表" });

        if (response?.IsSuccess == true && response.Data != null)
        {
            _timeSlots = response.Data;
            Debug.WriteLine($"[AttendanceService] 获取到 {_timeSlots.Count} 个时段");
            foreach (var slot in _timeSlots)
            {
                Debug.WriteLine($"[AttendanceService] 时段: ID={slot.Id}, 名称={slot.Name}, 开始={slot.StartTime}, 结束={slot.EndTime}");
            }
            return _timeSlots;
        }

        Debug.WriteLine($"[AttendanceService] 获取时段列表失败");
        return new List<TimeSlot>();
    }

    public async Task<AttendanceData?> GetCurrentAttendanceAsync()
    {
        Debug.WriteLine($"[AttendanceService] GetCurrentAttendanceAsync 开始");

        if (_currentClassId == 0)
        {
            Debug.WriteLine("[AttendanceService] 未设置班级ID");
            return null;
        }

        var response = await GetAsync<AttendanceData>(
            $"/desktop/current?class_id={_currentClassId}",
            new RequestOptions { OperationName = "获取当前考勤" });

        if (response?.IsSuccess == true && response.Data != null)
        {
            var data = response.Data;
            Debug.WriteLine($"[AttendanceService] GetCurrentAttendanceAsync 解析成功:");
            Debug.WriteLine($"[AttendanceService]   - IsAttendanceTime: {data.IsAttendanceTime}");
            Debug.WriteLine($"[AttendanceService]   - CurrentTimeSlot: {(data.CurrentTimeSlot != null ? $"ID={data.CurrentTimeSlot.Id}, Name={data.CurrentTimeSlot.Name}" : "null")}");
            Debug.WriteLine($"[AttendanceService]   - Schedule: {(data.Schedule != null ? $"ID={data.Schedule.Id}, ClassName={data.Schedule.ClassName}" : "null")}");
            Debug.WriteLine($"[AttendanceService]   - Message: {data.Message}");
            Debug.WriteLine($"[AttendanceService]   - TimeSlots.Count: {data.TimeSlots?.Count ?? 0}");

            if (data.TimeSlots != null && data.TimeSlots.Count > 0)
            {
                _timeSlots = data.TimeSlots;
                Debug.WriteLine($"[AttendanceService]   - 更新时段缓存: {_timeSlots.Count} 个时段");
            }

            if (data.CurrentTimeSlot != null)
            {
                _currentTimeSlotId = data.CurrentTimeSlot.Id;
                Debug.WriteLine($"[AttendanceService]   - 更新 _currentTimeSlotId={_currentTimeSlotId}");
            }
            else
            {
                _currentTimeSlotId = 0;
            }

            AttendanceDataUpdated?.Invoke(this, data);
            return data;
        }

        Debug.WriteLine($"[AttendanceService] GetCurrentAttendanceAsync 失败");
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
        Debug.WriteLine($"[AttendanceService] 当前时间: {DateTime.Now:HH:mm:ss}, TimeOfDay={now}");
        Debug.WriteLine($"[AttendanceService] 时段列表数量: {_timeSlots.Count}");
        foreach (var slot in _timeSlots)
        {
            Debug.WriteLine($"[AttendanceService] 检查时段: {slot.Name}, 开始={slot.StartTimeSpan}, 结束={slot.EndTimeSpan}, 当前={now}, 条件={slot.StartTimeSpan <= now && slot.EndTimeSpan >= now}");
            if (slot.StartTimeSpan <= now && slot.EndTimeSpan >= now)
            {
                Debug.WriteLine($"[AttendanceService] 匹配到当前时段: {slot.Name}");
                return slot;
            }
        }
        Debug.WriteLine($"[AttendanceService] 未匹配到当前时段");
        return null;
    }

    public void StartMonitoring()
    {
        if (_isMonitoring) return;

        _isMonitoring = true;
        _monitorTimer = new Timer(CheckTimeSlot, null, 0, 3600000);
        Debug.WriteLine("[AttendanceService] 时段监控已启动，每小时检查一次");
    }

    public void StopMonitoring()
    {
        _isMonitoring = false;
        _monitorTimer?.Dispose();
        _monitorTimer = null;
        Debug.WriteLine("[AttendanceService] 时段监控已停止");
    }

    private async void CheckTimeSlot(object? state)
    {
        try
        {
            Debug.WriteLine("[AttendanceService] 定时检查：调用后端获取最新考勤状态");
            await GetCurrentAttendanceAsync();
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
        Debug.WriteLine($"[AttendanceService] ========== InitializeAsync 开始, classId={classId} ==========");
        SetClassId(classId);

        await GetTimeSlotsAsync();
        Debug.WriteLine($"[AttendanceService] 时段列表获取完成, _timeSlots.Count={_timeSlots.Count}");

        var currentSlot = GetCurrentTimeSlot();
        Debug.WriteLine($"[AttendanceService] 当前时段判断结果: {(currentSlot != null ? $"ID={currentSlot.Id}, Name={currentSlot.Name}" : "null")}");
        if (currentSlot != null)
        {
            _currentTimeSlotId = currentSlot.Id;
            Debug.WriteLine($"[AttendanceService] 设置 _currentTimeSlotId={_currentTimeSlotId}");
        }

        Debug.WriteLine($"[AttendanceService] 开始调用 GetCurrentAttendanceAsync, classId={_currentClassId}");
        await GetCurrentAttendanceAsync();

        StartMonitoring();
        Debug.WriteLine($"[AttendanceService] ========== InitializeAsync 完成 ==========");
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
