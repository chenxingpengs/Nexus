using Nexus.Models.Chat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Diagnostics;

namespace Nexus.Services
{
    public class ChatCacheService : IDisposable
    {
        private static readonly string CacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Nexus",
            "ChatCache"
        );

        private const int MaxMessagesPerConversation = 100;
        private const int CacheExpirationDays = 7;

        private readonly Dictionary<string, List<ChatMessage>> _messageCache = new();
        private readonly Dictionary<string, Conversation> _conversationCache = new();
        private readonly HashSet<string> _readConversations = new();
        private readonly Dictionary<string, int> _lastReadMessageIds = new();
        private readonly object _lock = new();
        private bool _disposed;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        public ChatCacheService()
        {
            LoadCacheFromDisk();
        }

        public void CacheMessages(string conversationId, List<ChatMessage> messages)
        {
            if (string.IsNullOrEmpty(conversationId) || messages == null || messages.Count == 0)
                return;

            lock (_lock)
            {
                if (!_messageCache.ContainsKey(conversationId))
                {
                    _messageCache[conversationId] = new List<ChatMessage>();
                }

                var existingIds = _messageCache[conversationId].Select(m => m.Id).ToHashSet();
                
                foreach (var msg in messages)
                {
                    if (!existingIds.Contains(msg.Id))
                    {
                        _messageCache[conversationId].Add(msg);
                        existingIds.Add(msg.Id);
                    }
                }

                _messageCache[conversationId] = _messageCache[conversationId]
                    .OrderByDescending(m => m.SentAt)
                    .Take(MaxMessagesPerConversation)
                    .OrderBy(m => m.SentAt)
                    .ToList();

                SaveMessagesToDisk(conversationId);
            }

            Debug.WriteLine($"[ChatCache] 缓存了 {messages.Count} 条消息到会话 {conversationId}");
        }

        public List<ChatMessage>? GetCachedMessages(string conversationId)
        {
            if (string.IsNullOrEmpty(conversationId))
                return null;

            lock (_lock)
            {
                if (_messageCache.TryGetValue(conversationId, out var messages))
                {
                    return messages.OrderBy(m => m.SentAt).ToList();
                }
                return null;
            }
        }

        public void AddMessage(string conversationId, ChatMessage message)
        {
            if (string.IsNullOrEmpty(conversationId) || message == null)
                return;

            lock (_lock)
            {
                if (!_messageCache.ContainsKey(conversationId))
                {
                    _messageCache[conversationId] = new List<ChatMessage>();
                }

                var existingIds = _messageCache[conversationId].Select(m => m.Id).ToHashSet();
                if (!existingIds.Contains(message.Id))
                {
                    _messageCache[conversationId].Add(message);
                    
                    _messageCache[conversationId] = _messageCache[conversationId]
                        .OrderByDescending(m => m.SentAt)
                        .Take(MaxMessagesPerConversation)
                        .OrderBy(m => m.SentAt)
                        .ToList();

                    SaveMessagesToDisk(conversationId);
                    Debug.WriteLine($"[ChatCache] 添加新消息到缓存: {message.Id}");
                }
            }
        }

        public void CacheConversations(List<Conversation> conversations)
        {
            if (conversations == null)
                return;

            lock (_lock)
            {
                foreach (var conv in conversations)
                {
                    if (!string.IsNullOrEmpty(conv.Id))
                    {
                        _conversationCache[conv.Id] = conv;
                    }
                }

                SaveConversationsToDisk();
            }

            Debug.WriteLine($"[ChatCache] 缓存了 {conversations.Count} 个会话");
        }

        public List<Conversation>? GetCachedConversations()
        {
            lock (_lock)
            {
                if (_conversationCache.Count > 0)
                {
                    return _conversationCache.Values
                        .OrderByDescending(c => c.IsPinned)
                        .ThenByDescending(c => c.LastMessageTime)
                        .ToList();
                }
                return null;
            }
        }

        public void MarkConversationAsRead(string conversationId)
        {
            if (string.IsNullOrEmpty(conversationId))
                return;

            lock (_lock)
            {
                _readConversations.Add(conversationId);

                if (_conversationCache.TryGetValue(conversationId, out var conv))
                {
                    conv.UnreadCount = 0;
                    SaveConversationsToDisk();
                }

                SaveReadStatusToDisk();
            }

            Debug.WriteLine($"[ChatCache] 标记会话已读: {conversationId}");
        }

        public bool IsConversationRead(string conversationId)
        {
            lock (_lock)
            {
                return _readConversations.Contains(conversationId);
            }
        }

        public int? GetLastReadMessageId(string conversationId)
        {
            if (string.IsNullOrEmpty(conversationId))
                return null;

            lock (_lock)
            {
                if (_lastReadMessageIds.TryGetValue(conversationId, out var id))
                    return id;
                return null;
            }
        }

        public void SetLastReadMessageId(string conversationId, int messageId)
        {
            if (string.IsNullOrEmpty(conversationId))
                return;

            lock (_lock)
            {
                _lastReadMessageIds[conversationId] = messageId;
                SaveLastReadMessageIdsToDisk();
            }

            Debug.WriteLine($"[ChatCache] 设置会话 {conversationId} 最后已读消息ID: {messageId}");
        }

        public void UpdateConversationLastMessage(string conversationId, string? lastMessage, DateTime? lastMessageTime)
        {
            if (string.IsNullOrEmpty(conversationId))
                return;

            lock (_lock)
            {
                if (_conversationCache.TryGetValue(conversationId, out var conv))
                {
                    conv.LastMessage = lastMessage;
                    conv.LastMessageTime = lastMessageTime;
                    SaveConversationsToDisk();
                }
            }
        }

        public void UpdateConversationUnreadCount(string conversationId, int unreadCount)
        {
            if (string.IsNullOrEmpty(conversationId))
                return;

            lock (_lock)
            {
                if (_conversationCache.TryGetValue(conversationId, out var conv))
                {
                    conv.UnreadCount = unreadCount;
                    SaveConversationsToDisk();
                }
            }
        }

        public void ClearCache()
        {
            lock (_lock)
            {
                _messageCache.Clear();
                _conversationCache.Clear();
                _readConversations.Clear();

                try
                {
                    if (Directory.Exists(CacheDir))
                    {
                        Directory.Delete(CacheDir, true);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ChatCache] 清除缓存失败: {ex.Message}");
                }
            }

            Debug.WriteLine("[ChatCache] 缓存已清除");
        }

        private void LoadCacheFromDisk()
        {
            try
            {
                if (!Directory.Exists(CacheDir))
                {
                    Directory.CreateDirectory(CacheDir);
                    return;
                }

                LoadConversationsFromDisk();
                LoadReadStatusFromDisk();
                LoadLastReadMessageIdsFromDisk();
                LoadAllMessagesFromDisk();

                Debug.WriteLine("[ChatCache] 从磁盘加载缓存完成");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatCache] 加载缓存失败: {ex.Message}");
            }
        }

        private void LoadConversationsFromDisk()
        {
            var conversationsFile = Path.Combine(CacheDir, "conversations.json");
            if (File.Exists(conversationsFile))
            {
                var json = File.ReadAllText(conversationsFile);
                var conversations = JsonSerializer.Deserialize<List<Conversation>>(json, JsonOptions);
                
                if (conversations != null)
                {
                    lock (_lock)
                    {
                        foreach (var conv in conversations)
                        {
                            if (!string.IsNullOrEmpty(conv.Id))
                            {
                                _conversationCache[conv.Id] = conv;
                            }
                        }
                    }
                }
            }
        }

        private void LoadReadStatusFromDisk()
        {
            var readFile = Path.Combine(CacheDir, "read_status.json");
            if (File.Exists(readFile))
            {
                var json = File.ReadAllText(readFile);
                var readIds = JsonSerializer.Deserialize<List<string>>(json, JsonOptions);
                
                if (readIds != null)
                {
                    lock (_lock)
                    {
                        foreach (var id in readIds)
                        {
                            _readConversations.Add(id);
                        }
                    }
                }
            }
        }

        private void LoadAllMessagesFromDisk()
        {
            var messagesDir = Path.Combine(CacheDir, "messages");
            if (!Directory.Exists(messagesDir))
                return;

            foreach (var file in Directory.GetFiles(messagesDir, "*.json"))
            {
                try
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    var conversationId = fileName;
                    var json = File.ReadAllText(file);
                    var messages = JsonSerializer.Deserialize<List<ChatMessage>>(json, JsonOptions);

                    if (messages != null && !string.IsNullOrEmpty(conversationId))
                    {
                        lock (_lock)
                        {
                            _messageCache[conversationId] = messages;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ChatCache] 加载消息文件失败: {file}, 错误: {ex.Message}");
                }
            }
        }

        private void SaveConversationsToDisk()
        {
            try
            {
                if (!Directory.Exists(CacheDir))
                {
                    Directory.CreateDirectory(CacheDir);
                }

                var conversationsFile = Path.Combine(CacheDir, "conversations.json");
                var conversations = _conversationCache.Values.ToList();
                var json = JsonSerializer.Serialize(conversations, JsonOptions);
                File.WriteAllText(conversationsFile, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatCache] 保存会话缓存失败: {ex.Message}");
            }
        }

        private void SaveReadStatusToDisk()
        {
            try
            {
                if (!Directory.Exists(CacheDir))
                {
                    Directory.CreateDirectory(CacheDir);
                }

                var readFile = Path.Combine(CacheDir, "read_status.json");
                var readIds = _readConversations.ToList();
                var json = JsonSerializer.Serialize(readIds, JsonOptions);
                File.WriteAllText(readFile, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatCache] 保存已读状态失败: {ex.Message}");
            }
        }

        private void SaveLastReadMessageIdsToDisk()
        {
            try
            {
                if (!Directory.Exists(CacheDir))
                {
                    Directory.CreateDirectory(CacheDir);
                }

                var lastReadFile = Path.Combine(CacheDir, "last_read_message_ids.json");
                var json = JsonSerializer.Serialize(_lastReadMessageIds, JsonOptions);
                File.WriteAllText(lastReadFile, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatCache] 保存最后已读消息ID失败: {ex.Message}");
            }
        }

        private void LoadLastReadMessageIdsFromDisk()
        {
            var lastReadFile = Path.Combine(CacheDir, "last_read_message_ids.json");
            if (File.Exists(lastReadFile))
            {
                try
                {
                    var json = File.ReadAllText(lastReadFile);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, int>>(json, JsonOptions);
                    
                    if (dict != null)
                    {
                        lock (_lock)
                        {
                            foreach (var kvp in dict)
                            {
                                _lastReadMessageIds[kvp.Key] = kvp.Value;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ChatCache] 加载最后已读消息ID失败: {ex.Message}");
                }
            }
        }

        private void SaveMessagesToDisk(string conversationId)
        {
            try
            {
                var messagesDir = Path.Combine(CacheDir, "messages");
                if (!Directory.Exists(messagesDir))
                {
                    Directory.CreateDirectory(messagesDir);
                }

                var messageFile = Path.Combine(messagesDir, $"{conversationId}.json");
                var messages = _messageCache.GetValueOrDefault(conversationId);
                
                if (messages != null)
                {
                    var json = JsonSerializer.Serialize(messages, JsonOptions);
                    File.WriteAllText(messageFile, json);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatCache] 保存消息缓存失败: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            lock (_lock)
            {
                SaveConversationsToDisk();
                SaveReadStatusToDisk();
                SaveLastReadMessageIdsToDisk();
                
                foreach (var conversationId in _messageCache.Keys)
                {
                    SaveMessagesToDisk(conversationId);
                }
            }

            Debug.WriteLine("[ChatCache] 服务已释放");
            GC.SuppressFinalize(this);
        }
    }
}
