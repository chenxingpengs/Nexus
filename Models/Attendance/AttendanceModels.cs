using System;
using System.Text.Json.Serialization;

namespace Nexus.Models.Attendance
{
    public class TimeSlot
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("startTime")]
        public string StartTime { get; set; } = "";

        [JsonPropertyName("endTime")]
        public string EndTime { get; set; } = "";

        [JsonIgnore]
        public TimeSpan StartTimeSpan => TimeSpan.TryParse(StartTime, out var ts) ? ts : TimeSpan.Zero;

        [JsonIgnore]
        public TimeSpan EndTimeSpan => TimeSpan.TryParse(EndTime, out var ts) ? ts : TimeSpan.Zero;

        [JsonIgnore]
        public string DisplayName => $"{Name} ({StartTime[..5]}-{EndTime[..5]})";

        public bool IsCurrentTimeSlot()
        {
            var now = DateTime.Now.TimeOfDay;
            return StartTimeSpan <= now && EndTimeSpan >= now;
        }
    }

    public class CurrentTimeSlot : TimeSlot
    {
        [JsonPropertyName("isAttendanceTime")]
        public bool IsAttendanceTime { get; set; }
    }

    public class ScheduleInfo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

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

        [JsonPropertyName("shouldAttend")]
        public int ShouldAttend { get; set; }

        [JsonPropertyName("actualAttend")]
        public int ActualAttend { get; set; }

        [JsonPropertyName("leaveCount")]
        public int LeaveCount { get; set; }

        [JsonPropertyName("absentCount")]
        public int AbsentCount { get; set; }

        [JsonPropertyName("completed")]
        public bool Completed { get; set; }

        [JsonIgnore]
        public double AttendanceRate => ShouldAttend > 0 ? (double)ActualAttend / ShouldAttend * 100 : 0;
    }

    public class MakeupInfo
    {
        [JsonPropertyName("originalDate")]
        public string OriginalDate { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";
    }

    public class AttendanceData
    {
        [JsonPropertyName("currentTimeSlot")]
        public CurrentTimeSlot? CurrentTimeSlot { get; set; }

        [JsonPropertyName("timeSlots")]
        public System.Collections.Generic.List<TimeSlot> TimeSlots { get; set; } = new();

        [JsonPropertyName("isAttendanceTime")]
        public bool IsAttendanceTime { get; set; }

        [JsonPropertyName("schedule")]
        public ScheduleInfo? Schedule { get; set; }

        [JsonPropertyName("specialStatus")]
        public string? SpecialStatus { get; set; }

        [JsonPropertyName("cancelReason")]
        public string CancelReason { get; set; } = "";

        [JsonPropertyName("makeupInfo")]
        public MakeupInfo? MakeupInfo { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = "";
    }

    public class AttendanceApiResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("msg")]
        public string Msg { get; set; } = "";

        [JsonPropertyName("data")]
        public AttendanceData? Data { get; set; }
    }

    public class AttendanceUpdateNotification
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("data")]
        public AttendanceUpdateData? Data { get; set; }
    }

    public class AttendanceUpdateData
    {
        [JsonPropertyName("classId")]
        public int ClassId { get; set; }

        [JsonPropertyName("scheduleId")]
        public int ScheduleId { get; set; }

        [JsonPropertyName("timeSlotId")]
        public int TimeSlotId { get; set; }

        [JsonPropertyName("date")]
        public string Date { get; set; } = "";

        [JsonPropertyName("operation")]
        public string Operation { get; set; } = "";

        [JsonPropertyName("message")]
        public string Message { get; set; } = "";
    }
}
