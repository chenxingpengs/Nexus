using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Nexus.Models.Schedule
{
    public class TeacherModel
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        [JsonPropertyName("nickname")]
        public string Nickname { get; set; } = string.Empty;

        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("permission")]
        public int Permission { get; set; }

        public string DisplayName => string.IsNullOrEmpty(Nickname) ? Username : Nickname;
    }

    public class TimeSlotModel
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("start_time")]
        public string StartTime { get; set; } = string.Empty;

        [JsonPropertyName("end_time")]
        public string EndTime { get; set; } = string.Empty;

        [JsonPropertyName("is_fixed")]
        public int IsFixed { get; set; }
    }

    public class PeriodicRuleModel
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("class_id")]
        public int ClassId { get; set; }

        [JsonPropertyName("teacher_id")]
        public int? TeacherId { get; set; }

        [JsonPropertyName("teacher_name")]
        public string? TeacherName { get; set; }

        [JsonPropertyName("time_slot_id")]
        public int TimeSlotId { get; set; }

        [JsonPropertyName("time_slot_name")]
        public string? TimeSlotName { get; set; }

        [JsonPropertyName("weekday")]
        public int Weekday { get; set; }

        [JsonPropertyName("weekday_name")]
        public string? WeekdayName { get; set; }

        [JsonPropertyName("service_type")]
        public string? ServiceType { get; set; }

        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("start_time")]
        public string? StartTime { get; set; }

        [JsonPropertyName("end_time")]
        public string? EndTime { get; set; }

        [JsonPropertyName("class_name")]
        public string? ClassName { get; set; }
    }

    public class MissingSlotModel
    {
        [JsonPropertyName("time_slot_id")]
        public int TimeSlotId { get; set; }

        [JsonPropertyName("time_slot_name")]
        public string TimeSlotName { get; set; } = string.Empty;

        [JsonPropertyName("weekday")]
        public int Weekday { get; set; }

        [JsonPropertyName("weekday_name")]
        public string? WeekdayName { get; set; }
    }

    public class ScheduleCompletenessModel
    {
        [JsonPropertyName("is_complete")]
        public bool IsComplete { get; set; }

        [JsonPropertyName("fixed_time_slots")]
        public List<TimeSlotModel> FixedTimeSlots { get; set; } = new();

        [JsonPropertyName("existing_rules")]
        public List<PeriodicRuleModel> ExistingRules { get; set; } = new();

        [JsonPropertyName("missing_slots")]
        public List<MissingSlotModel> MissingSlots { get; set; } = new();

        [JsonPropertyName("weekday_names")]
        public Dictionary<string, string>? WeekdayNames { get; set; }
    }

    public class AddPeriodicRuleRequest
    {
        [JsonPropertyName("class_id")]
        public int ClassId { get; set; }

        [JsonPropertyName("time_slot_id")]
        public int TimeSlotId { get; set; }

        [JsonPropertyName("weekday")]
        public int Weekday { get; set; }

        [JsonPropertyName("service_type")]
        public string ServiceType { get; set; } = "课后服务";

        [JsonPropertyName("teacher_id")]
        public int? TeacherId { get; set; }
    }

    public class BatchAddPeriodicRulesRequest
    {
        [JsonPropertyName("class_id")]
        public int ClassId { get; set; }

        [JsonPropertyName("rules")]
        public List<AddPeriodicRuleRequest> Rules { get; set; } = new();
    }

    public class UpdatePeriodicRuleRequest
    {
        [JsonPropertyName("service_type")]
        public string? ServiceType { get; set; }

        [JsonPropertyName("teacher_id")]
        public int? TeacherId { get; set; }

        [JsonPropertyName("weekday")]
        public int? Weekday { get; set; }

        [JsonPropertyName("time_slot_id")]
        public int? TimeSlotId { get; set; }
    }

    public class TimeSlotScheduleItem : ObservableObject
    {
        public TimeSlotModel TimeSlot { get; set; } = new();
        public ObservableCollection<WeekdayScheduleItem> WeekdayItems { get; set; } = new();
    }

    public partial class WeekdayScheduleItem : ObservableObject
    {
        public int Weekday { get; set; }
        public string WeekdayName { get; set; } = string.Empty;
        public bool IsConfigured { get; set; }
        public PeriodicRuleModel? ExistingRule { get; set; }
        
        [ObservableProperty]
        private int? _selectedTeacherId;
        
        public bool IsMissing { get; set; }
        
        public ObservableCollection<TeacherModel>? AvailableTeachers { get; set; }
        public ObservableCollection<TeacherModel>? FilteredTeachers { get; set; }
    }

    public static class WeekdayHelper
    {
        public static readonly Dictionary<int, string> WeekdayNames = new()
        {
            { 1, "周一" },
            { 2, "周二" },
            { 3, "周三" },
            { 4, "周四" },
            { 5, "周五" },
            { 6, "周六" },
            { 7, "周日" }
        };

        public static string GetWeekdayName(int weekday)
        {
            return WeekdayNames.TryGetValue(weekday, out var name) ? name : "";
        }
    }
}
