using System;
using System.Text.Json.Serialization;

namespace Nexus.Models
{
    public enum PageCallStatus
    {
        [JsonPropertyName("pending")]
        Pending,
        
        [JsonPropertyName("confirmed")]
        Confirmed,
        
        [JsonPropertyName("cancelled")]
        Cancelled
    }

    public class PageCall
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
        
        [JsonPropertyName("type")]
        public string Type { get; set; } = "page_call";
        
        [JsonPropertyName("student_name")]
        public string StudentName { get; set; } = string.Empty;
        
        [JsonPropertyName("class_id")]
        public int? ClassId { get; set; }
        
        [JsonPropertyName("class_name")]
        public string? ClassName { get; set; }
        
        [JsonPropertyName("device_id")]
        public string? DeviceId { get; set; }
        
        [JsonPropertyName("reason")]
        public string? Reason { get; set; }
        
        [JsonPropertyName("status")]
        public string Status { get; set; } = "pending";
        
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }
        
        [JsonPropertyName("confirmed_at")]
        public DateTime? ConfirmedAt { get; set; }
        
        [JsonPropertyName("confirmed_by")]
        public string? ConfirmedBy { get; set; }
        
        [JsonPropertyName("cancelled_at")]
        public DateTime? CancelledAt { get; set; }
        
        public PageCallStatus PageCallStatus => Status?.ToLower() switch
        {
            "pending" => PageCallStatus.Pending,
            "confirmed" => PageCallStatus.Confirmed,
            "cancelled" => PageCallStatus.Cancelled,
            _ => PageCallStatus.Pending
        };
        
        public string DisplayTitle => $"寻人通知";
        
        public string DisplayContent => string.IsNullOrEmpty(Reason) 
            ? $"请通知 {StudentName} 同学到办公室一趟"
            : $"请通知 {StudentName} 同学到办公室一趟\n原因：{Reason}";
        
        public string DisplayClass => ClassName ?? "未知班级";
        
        public string DisplayTime => CreatedAt.ToString("HH:mm:ss");
    }
}
