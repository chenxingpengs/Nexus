using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Nexus.Models.Meeting
{
    public enum MeetingStatus
    {
        Pending,
        Active,
        Ended
    }

    public enum ParticipantStatus
    {
        Invited,
        Accepted,
        Rejected,
        Left
    }

    public class MeetingInfo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("meeting_id")]
        public string MeetingId { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("host_id")]
        public int HostId { get; set; }

        [JsonPropertyName("host_name")]
        public string? HostName { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "pending";

        [JsonPropertyName("broadcast_port")]
        public int BroadcastPort { get; set; }

        [JsonPropertyName("host_ip")]
        public string? HostIp { get; set; }

        [JsonPropertyName("meeting_key")]
        public string? MeetingKey { get; set; }

        [JsonPropertyName("participants")]
        public List<ParticipantInfo>? Participants { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; set; }

        [JsonPropertyName("started_at")]
        public DateTime? StartedAt { get; set; }

        [JsonPropertyName("ended_at")]
        public DateTime? EndedAt { get; set; }
    }

    public class ParticipantInfo
    {
        [JsonPropertyName("class_id")]
        public int ClassId { get; set; }

        [JsonPropertyName("class_name")]
        public string? ClassName { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "invited";

        [JsonPropertyName("device_id")]
        public string? DeviceId { get; set; }

        [JsonPropertyName("invited_at")]
        public DateTime? InvitedAt { get; set; }

        [JsonPropertyName("joined_at")]
        public DateTime? JoinedAt { get; set; }

        [JsonPropertyName("left_at")]
        public DateTime? LeftAt { get; set; }
    }

    public class MeetingInvitation
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "meeting_invited";

        [JsonPropertyName("meeting_id")]
        public string MeetingId { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("host_name")]
        public string? HostName { get; set; }

        [JsonPropertyName("broadcast_port")]
        public int BroadcastPort { get; set; }

        [JsonPropertyName("host_ip")]
        public string? HostIp { get; set; }

        [JsonPropertyName("invited_at")]
        public DateTime? InvitedAt { get; set; }
    }

    public class MeetingHistory
    {
        [JsonPropertyName("meeting_id")]
        public string MeetingId { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("host_name")]
        public string? HostName { get; set; }

        [JsonPropertyName("started_at")]
        public DateTime? StartedAt { get; set; }

        [JsonPropertyName("ended_at")]
        public DateTime? EndedAt { get; set; }

        [JsonPropertyName("duration")]
        public int? Duration { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
    }

    public class JoinMeetingRequest
    {
        [JsonPropertyName("meeting_id")]
        public string MeetingId { get; set; } = string.Empty;

        [JsonPropertyName("device_id")]
        public string DeviceId { get; set; } = string.Empty;

        [JsonPropertyName("class_id")]
        public int ClassId { get; set; }
    }

    public class JoinMeetingResponse
    {
        [JsonPropertyName("meeting_id")]
        public string MeetingId { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("meeting_key")]
        public string MeetingKey { get; set; } = string.Empty;

        [JsonPropertyName("broadcast_port")]
        public int BroadcastPort { get; set; }

        [JsonPropertyName("host_ip")]
        public string? HostIp { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
    }
}
