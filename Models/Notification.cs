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
        
        [JsonPropertyName("schedule")]
        Schedule,
        
        [JsonPropertyName("attendance")]
        Attendance,
        
        [JsonPropertyName("emergency")]
        Emergency
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
        
        public NotificationType NotificationType => Type?.ToLower() switch
        {
            "banner" => NotificationType.Banner,
            "alert" => NotificationType.Alert,
            "system" => NotificationType.System,
            "schedule" => NotificationType.Schedule,
            "attendance" => NotificationType.Attendance,
            "emergency" => NotificationType.Emergency,
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
    }
}
