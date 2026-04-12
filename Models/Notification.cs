using System;
using System.Text.Json.Serialization;

namespace Nexus.Models
{
    public enum NotificationType
    {
        [JsonPropertyName("banner")]
        Banner,
        
        [JsonPropertyName("alert")]
        Alert,
        
        [JsonPropertyName("system")]
        System,
        
        [JsonPropertyName("emergency")]
        Emergency,
        
        [JsonPropertyName("fire_alarm")]
        FireAlarm,
        
        [JsonPropertyName("air_raid_alert")]
        AirRaidAlert,
        
        [JsonPropertyName("earthquake_warning")]
        EarthquakeWarning
    }

    public enum NotificationPriority
    {
        [JsonPropertyName("low")]
        Low,
        
        [JsonPropertyName("normal")]
        Normal,
        
        [JsonPropertyName("high")]
        High,
        
        [JsonPropertyName("urgent")]
        Urgent
    }

    public class DisplayConfig
    {
        [JsonPropertyName("duration")]
        public int Duration { get; set; } = 10;
        
        [JsonPropertyName("scroll_speed")]
        public int ScrollSpeed { get; set; } = 50;
        
        [JsonPropertyName("position")]
        public string Position { get; set; } = "top";
        
        [JsonPropertyName("style")]
        public string Style { get; set; } = "info";
        
        [JsonPropertyName("sound")]
        public bool? Sound { get; set; }
        
        [JsonPropertyName("speak")]
        public SpeakConfig? Speak { get; set; }
    }

    public class SpeakConfig
    {
        [JsonPropertyName("speak_enabled")]
        public bool SpeakEnabled { get; set; } = true;
        
        [JsonPropertyName("speak_voice")]
        public string SpeakVoice { get; set; } = "xiaoxiao";
        
        [JsonPropertyName("speak_rate")]
        public int SpeakRate { get; set; } = 0;
    }

    public class Notification
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
        
        [JsonPropertyName("type")]
        public string Type { get; set; } = "banner";
        
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;
        
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
        
        [JsonPropertyName("priority")]
        public string Priority { get; set; } = "normal";
        
        [JsonPropertyName("display")]
        public DisplayConfig? Display { get; set; }
        
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }
        
        [JsonPropertyName("expires_at")]
        public DateTime? ExpiresAt { get; set; }
        
        [JsonPropertyName("alert_subtype")]
        public string? AlertSubtype { get; set; }
        
        [JsonPropertyName("magnitude")]
        public string? Magnitude { get; set; }
        
        [JsonPropertyName("eta")]
        public string? Eta { get; set; }
        
        public NotificationType NotificationType => Type?.ToLower() switch
        {
            "banner" => NotificationType.Banner,
            "alert" => NotificationType.Alert,
            "system" => NotificationType.System,
            "emergency" => NotificationType.Emergency,
            "fire_alarm" => NotificationType.FireAlarm,
            "air_raid_alert" => NotificationType.AirRaidAlert,
            "earthquake_warning" => NotificationType.EarthquakeWarning,
            _ => NotificationType.Banner
        };
        
        public NotificationPriority NotificationPriority => Priority?.ToLower() switch
        {
            "low" => NotificationPriority.Low,
            "normal" => NotificationPriority.Normal,
            "high" => NotificationPriority.High,
            "urgent" => NotificationPriority.Urgent,
            _ => NotificationPriority.Normal
        };
        
        public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.Now;
        
        public string BackgroundColor => Display?.Style?.ToLower() switch
        {
            "warning" => "#E6A23C",
            "error" => "#F56C6C",
            "success" => "#67C23A",
            _ => "#409EFF"
        };
        
        public string FlashColor => Type?.ToLower() switch
        {
            "fire_alarm" => "#FF0000",
            "air_raid_alert" => "#FF8C00",
            "earthquake_warning" => "#FFD700",
            "emergency" => "#F56C6C",
            _ => "#F56C6C"
        };
    }
}
