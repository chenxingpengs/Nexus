using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nexus.ViewModels;

namespace Nexus.Models.Chat
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ConversationType
    {
        [JsonPropertyName("private")]
        Private,
        [JsonPropertyName("group")]
        Group,
        [JsonPropertyName("broadcast")]
        Broadcast
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MessageType
    {
        [JsonPropertyName("text")]
        Text,
        [JsonPropertyName("image")]
        Image,
        [JsonPropertyName("file")]
        File,
        [JsonPropertyName("system")]
        System
    }

    public enum MessageStatus
    {
        Sending,
        Sent,
        Delivered,
        Read,
        Failed
    }

    public class Conversation : ViewModelBase
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
        
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        
        [JsonPropertyName("type")]
        public string? TypeStr { get; set; }
        
        [JsonIgnore]
        public ConversationType Type
        {
            get => TypeStr?.ToLower() switch
            {
                "private" => ConversationType.Private,
                "group" => ConversationType.Group,
                "broadcast" => ConversationType.Broadcast,
                _ => ConversationType.Group
            };
        }
        
        [JsonPropertyName("avatar")]
        public string? Avatar { get; set; }
        
        [JsonPropertyName("last_message")]
        public string? LastMessage { get; set; }
        
        [JsonPropertyName("last_message_time")]
        public string? LastMessageTimeStr { get; set; }
        
        [JsonIgnore]
        public DateTime? LastMessageTime
        {
            get
            {
                if (string.IsNullOrEmpty(LastMessageTimeStr)) return null;
                if (DateTime.TryParse(LastMessageTimeStr, null, DateTimeStyles.RoundtripKind, out var dt))
                    return dt.ToLocalTime();
                if (DateTime.TryParse(LastMessageTimeStr, out var dtLocal))
                    return dtLocal;
                return null;
            }
            set
            {
                LastMessageTimeStr = value?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
            }
        }

        private int _unreadCount;
        
        [JsonPropertyName("unread_count")]
        public int UnreadCount
        {
            get => _unreadCount;
            set => SetProperty(ref _unreadCount, value);
        }
        
        [JsonPropertyName("is_pinned")]
        public bool IsPinned { get; set; }
        
        [JsonPropertyName("is_muted")]
        public bool IsMuted { get; set; }
        
        [JsonPropertyName("participants")]
        public List<Participant> Participants { get; set; } = new();
    }

    public class Participant
    {
        [JsonPropertyName("user_id")]
        public int? UserId { get; set; }
        
        [JsonPropertyName("device_id")]
        public string? DeviceId { get; set; }
        
        [JsonPropertyName("class_id")]
        public int? ClassId { get; set; }
        
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        
        [JsonPropertyName("avatar")]
        public string? Avatar { get; set; }
        
        [JsonPropertyName("role")]
        public string? Role { get; set; }
    }

    public class ChatMessage
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        
        [JsonPropertyName("conversation_id")]
        public string? ConversationId { get; set; }
        
        [JsonPropertyName("sender_id")]
        public int? SenderId { get; set; }
        
        [JsonPropertyName("sender_device_id")]
        public string? SenderDeviceId { get; set; }
        
        [JsonPropertyName("sender_class_id")]
        public int? SenderClassId { get; set; }
        
        [JsonPropertyName("sender_name")]
        public string? SenderName { get; set; }
        
        [JsonPropertyName("sender_avatar")]
        public string? SenderAvatar { get; set; }
        
        [JsonPropertyName("content")]
        public string? Content { get; set; }
        
        [JsonPropertyName("type")]
        public string? TypeStr { get; set; } = "text";
        
        [JsonIgnore]
        public MessageType Type
        {
            get => TypeStr?.ToLower() switch
            {
                "text" => MessageType.Text,
                "image" => MessageType.Image,
                "file" => MessageType.File,
                "system" => MessageType.System,
                _ => MessageType.Text
            };
            set => TypeStr = value.ToString().ToLower();
        }
        
        [JsonPropertyName("sent_at")]
        public string? SentAtStr { get; set; }
        
        [JsonIgnore]
        public DateTime SentAt
        {
            get
            {
                if (string.IsNullOrEmpty(SentAtStr)) return DateTime.Now;
                if (DateTime.TryParse(SentAtStr, null, DateTimeStyles.RoundtripKind, out var dt))
                    return dt.ToLocalTime();
                if (DateTime.TryParse(SentAtStr, out var dtLocal))
                    return dtLocal;
                return DateTime.Now;
            }
            set
            {
                SentAtStr = value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
            }
        }
        
        [JsonPropertyName("reply_to_id")]
        public int? ReplyToId { get; set; }
        
        [JsonPropertyName("reply_to")]
        public ChatMessage? ReplyTo { get; set; }
        
        [JsonPropertyName("is_mine")]
        public bool IsMine { get; set; }
        
        [JsonIgnore]
        public MessageStatus Status { get; set; } = MessageStatus.Sent;
        
        [JsonIgnore]
        public bool IsNewMessageDivider { get; set; } = false;

        [JsonPropertyName("extra_data")]
        [JsonConverter(typeof(ExtraDataJsonConverter))]
        public object? ExtraDataRaw { get; set; }

        [JsonIgnore]
        public Dictionary<string, object>? ExtraData
        {
            get
            {
                if (ExtraDataRaw == null) return null;

                if (ExtraDataRaw is Dictionary<string, object> dict)
                {
                    return dict;
                }

                if (ExtraDataRaw is string jsonString)
                {
                    try
                    {
                        return JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString);
                    }
                    catch (Exception)
                    {
                        return new Dictionary<string, object>
                        {
                            { "raw", jsonString }
                        };
                    }
                }

                if (ExtraDataRaw is JsonElement jsonElement)
                {
                    try
                    {
                        return JsonSerializer.Deserialize<Dictionary<string, object>>(jsonElement.GetRawText());
                    }
                    catch (Exception)
                    {
                        return new Dictionary<string, object>
                        {
                            { "raw", jsonElement.ToString() }
                        };
                    }
                }

                return null;
            }
            set => ExtraDataRaw = value;
        }
    }

    public class ConversationListResponse
    {
        [JsonPropertyName("list")]
        public List<Conversation>? List { get; set; }
        
        [JsonPropertyName("total")]
        public int Total { get; set; }
        
        [JsonPropertyName("page")]
        public int Page { get; set; }
        
        [JsonPropertyName("size")]
        public int Size { get; set; }
    }

    public class MessageListResponse
    {
        [JsonPropertyName("list")]
        public List<ChatMessage>? List { get; set; }
        
        [JsonPropertyName("total")]
        public int Total { get; set; }
        
        [JsonPropertyName("has_more")]
        public bool HasMore { get; set; }
    }

    public class ApiResponse<T>
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }
        
        [JsonPropertyName("msg")]
        public string? Msg { get; set; }
        
        [JsonPropertyName("data")]
        public T? Data { get; set; }
        
        [JsonIgnore]
        public bool Success => Code == 200;
    }
}
