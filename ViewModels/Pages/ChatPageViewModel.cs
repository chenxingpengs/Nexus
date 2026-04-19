using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Nexus.Models.Chat;
using Nexus.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Nexus.ViewModels.Pages
{
    public class MentionItem
    {
        public int UserId { get; set; }
        public string Name { get; set; } = "";
    }

    public class ChatPageViewModel : ViewModelBase, IDisposable
    {
        private readonly ConfigService _configService;
        private readonly ToastService _toastService;
        private readonly ProfanityCheckService _profanityCheckService;
        private ChatSocketIOService? _chatSocketService;
        private ChatHttpService? _chatHttpService;
        private ChatCacheService? _chatCacheService;

        private Conversation? _selectedConversation;
        public Conversation? SelectedConversation
        {
            get => _selectedConversation;
            set
            {
                if (SetProperty(ref _selectedConversation, value))
                {
                    OnConversationSelected(value);
                }
            }
        }

        private string _messageInput = "";
        public string MessageInput
        {
            get => _messageInput;
            set 
            { 
                if (SetProperty(ref _messageInput, value))
                {
                    (SendMessageCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
                }
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private bool _isSending;
        public bool IsSending
        {
            get => _isSending;
            set => SetProperty(ref _isSending, value);
        }

        private bool _isTyping;
        public bool IsTyping
        {
            get => _isTyping;
            set => SetProperty(ref _isTyping, value);
        }

        private string _typingUserName = "";
        public string TypingUserName
        {
            get => _typingUserName;
            set => SetProperty(ref _typingUserName, value);
        }

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set => SetProperty(ref _isConnected, value);
        }

        private string _connectionStatus = "未连接";
        public string ConnectionStatus
        {
            get => _connectionStatus;
            set => SetProperty(ref _connectionStatus, value);
        }

        private int? _lastReadMessageId;
        private int? _dividerMessageId;
        private bool _isWindowActive = true;
        private bool _isWindowMinimized = false;

        public bool HasNewMessageDivider => _dividerMessageId != null;

        public ObservableCollection<Conversation> Conversations { get; } = new();
        public ObservableCollection<ChatMessage> Messages { get; } = new();

        public ICommand SendMessageCommand { get; }
        public ICommand RefreshConversationsCommand { get; }
        public ICommand ShowMentionPanelCommand { get; }
        public ICommand SelectMentionMemberCommand { get; }
        public ICommand CloseMentionPanelCommand { get; }

        private bool _isMentionPanelVisible;
        public bool IsMentionPanelVisible
        {
            get => _isMentionPanelVisible;
            set => SetProperty(ref _isMentionPanelVisible, value);
        }

        private ObservableCollection<Participant> _mentionMembers = new();
        public ObservableCollection<Participant> MentionMembers
        {
            get => _mentionMembers;
            set => SetProperty(ref _mentionMembers, value);
        }

        private string _mentionSearchText = "";
        public string MentionSearchText
        {
            get => _mentionSearchText;
            set
            {
                if (SetProperty(ref _mentionSearchText, value))
                {
                    FilterMentionMembers();
                }
            }
        }

        private List<MentionItem> _mentions = new();
        public List<MentionItem> Mentions => _mentions;

        public event EventHandler<ChatMessage>? NewMessageReceived;
        public event EventHandler? MessagesRead;

        public ChatPageViewModel(ConfigService configService, ToastService toastService)
        {
            _configService = configService;
            _toastService = toastService;
            _profanityCheckService = new ProfanityCheckService(toastService);

            SendMessageCommand = new AsyncRelayCommand(SendMessageAsync, CanSendMessage);
            RefreshConversationsCommand = new AsyncRelayCommand(LoadConversationsAsync);
            ShowMentionPanelCommand = new RelayCommand(ShowMentionPanel);
            SelectMentionMemberCommand = new RelayCommand<Participant>(SelectMentionMember);
            CloseMentionPanelCommand = new RelayCommand(() => IsMentionPanelVisible = false);
        }

        public void SetChatServices(ChatSocketIOService chatSocketService, ChatHttpService chatHttpService, ChatCacheService? chatCacheService = null)
        {
            _chatSocketService = chatSocketService;
            _chatHttpService = chatHttpService;
            _chatCacheService = chatCacheService;
            
            SetupSocketEventHandlers();
            
            _ = LoadConversationsAsync();
        }

        private void SetupSocketEventHandlers()
        {
            if (_chatSocketService == null) return;

            _chatSocketService.MessageReceived += (sender, message) =>
            {
                Dispatcher.UIThread.Post(() => OnMessageReceived(message));
            };

            _chatSocketService.TypingStarted += (sender, data) =>
            {
                Dispatcher.UIThread.Post(() => OnTypingStarted(data.ConversationId, data.SenderName ?? "对方"));
            };

            _chatSocketService.TypingStopped += (sender, data) =>
            {
                Dispatcher.UIThread.Post(() => OnTypingStopped(data.ConversationId));
            };

            _chatSocketService.Connected += (sender, e) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    IsConnected = true;
                    ConnectionStatus = "已连接";
                });
            };

            _chatSocketService.Disconnected += (sender, e) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    IsConnected = false;
                    ConnectionStatus = "已断开";
                });
            };

            _chatSocketService.ConnectionStateChanged += (sender, info) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    ConnectionStatus = info.StatusText;
                    IsConnected = info.IsConnected;
                });
            };

            _chatSocketService.ErrorOccurred += (sender, errorMessage) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    HandleSocketError(errorMessage);
                });
            };

            IsConnected = _chatSocketService.IsConnected;
            ConnectionStatus = _chatSocketService.IsConnected ? "已连接" : "未连接";
        }

        private void HandleSocketError(string errorMessage)
        {
            try
            {
                var errorObj = System.Text.Json.JsonDocument.Parse(errorMessage);
                if (errorObj.RootElement.TryGetProperty("message", out var msgElement))
                {
                    var message = msgElement.GetString();
                    if (!string.IsNullOrEmpty(message))
                    {
                        _toastService.ShowError(message);
                        Debug.WriteLine($"[ChatPageViewModel] Socket错误: {message}");
                        return;
                    }
                }
            }
            catch
            {
            }
            
            _toastService.ShowError(errorMessage);
            Debug.WriteLine($"[ChatPageViewModel] Socket错误: {errorMessage}");
        }

        public async Task LoadConversationsAsync()
        {
            if (_chatHttpService == null)
            {
                _toastService.ShowError("聊天服务未初始化");
                return;
            }

            var loadedFromCache = false;

            if (_chatCacheService != null)
            {
                var cachedConversations = _chatCacheService.GetCachedConversations();
                if (cachedConversations != null && cachedConversations.Count > 0)
                {
                    Conversations.Clear();
                    foreach (var conv in cachedConversations)
                    {
                        Conversations.Add(conv);
                    }
                    loadedFromCache = true;
                    Debug.WriteLine("[ChatPageViewModel] 从缓存加载了会话列表");
                }
            }

            try
            {
                IsLoading = true;

                var response = await _chatHttpService.GetConversationsAsync();
                
                if (response != null && response.List != null)
                {
                    Conversations.Clear();
                    foreach (var conv in response.List)
                    {
                        Conversations.Add(conv);
                    }

                    if (_chatCacheService != null)
                    {
                        _chatCacheService.CacheConversations(response.List);
                    }

                    if (Conversations.Count == 0)
                    {
                        Debug.WriteLine("[ChatPageViewModel] 暂无会话");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatPageViewModel] 加载会话列表异常: {ex.Message}");
                if (!loadedFromCache)
                {
                    _toastService.ShowError($"加载会话列表异常: {ex.Message}");
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async void OnConversationSelected(Conversation? conversation)
        {
            if (conversation == null) return;

            var conversationId = conversation.Id;
            var loadedFromCache = false;

            _dividerMessageId = null;
            _lastReadMessageId = null;

            if (_chatCacheService != null)
            {
                var cachedMessages = _chatCacheService.GetCachedMessages(conversationId);
                if (cachedMessages != null && cachedMessages.Count > 0)
                {
                    Messages.Clear();
                    foreach (var msg in cachedMessages)
                    {
                        if (!msg.IsNewMessageDivider)
                        {
                            Messages.Add(msg);
                        }
                    }
                    loadedFromCache = true;
                    Debug.WriteLine($"[ChatPageViewModel] 从缓存加载了 {cachedMessages.Count} 条消息");
                }

                _lastReadMessageId = _chatCacheService.GetLastReadMessageId(conversationId);
            }

            try
            {
                IsLoading = true;

                if (!loadedFromCache)
                {
                    Messages.Clear();
                }

                if (_chatSocketService != null && _chatSocketService.IsConnected)
                {
                    var joinResult = await _chatSocketService.JoinConversationAsync(conversationId);
                    if (!joinResult.Success)
                    {
                        _toastService.ShowError($"加入会话失败: {joinResult.ErrorMessage}");
                    }
                }

                if (_chatHttpService != null)
                {
                    var response = await _chatHttpService.GetMessagesAsync(conversationId);
                    
                    if (response != null && response.List != null)
                    {
                        if (!loadedFromCache)
                        {
                            Messages.Clear();
                        }

                        var existingIds = new System.Collections.Generic.HashSet<int>();
                        foreach (var msg in Messages)
                        {
                            existingIds.Add(msg.Id);
                        }

                        foreach (var msg in response.List)
                        {
                            if (!existingIds.Contains(msg.Id))
                            {
                                Messages.Insert(0, msg);
                                existingIds.Add(msg.Id);
                            }
                        }

                        if (_chatCacheService != null)
                        {
                            _chatCacheService.CacheMessages(conversationId, new System.Collections.Generic.List<ChatMessage>(Messages));
                        }
                    }
                }

                InsertNewMessageDivider();

                Debug.WriteLine($"[ChatPageViewModel] 会话 {conversationId} 已加载，最后已读消息ID: {_lastReadMessageId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatPageViewModel] 加载消息失败: {ex.Message}");
                _toastService.ShowError($"加载消息失败: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void InsertNewMessageDivider()
        {
            if (_lastReadMessageId == null || Messages.Count == 0) return;

            RemoveNewMessageDivider();

            int insertIndex = -1;
            for (int i = 0; i < Messages.Count; i++)
            {
                if (Messages[i].Id == _lastReadMessageId.Value)
                {
                    insertIndex = i + 1;
                    break;
                }
            }

            if (insertIndex > 0 && insertIndex < Messages.Count)
            {
                var divider = new ChatMessage
                {
                    Id = int.MinValue,
                    IsNewMessageDivider = true,
                    Content = "-----以下为新消息----"
                };
                Messages.Insert(insertIndex, divider);
                _dividerMessageId = divider.Id;
                OnPropertyChanged(nameof(HasNewMessageDivider));
                Debug.WriteLine($"[ChatPageViewModel] 插入分隔符在位置 {insertIndex}");
            }
        }

        private void RemoveNewMessageDivider()
        {
            if (_dividerMessageId == null) return;

            for (int i = Messages.Count - 1; i >= 0; i--)
            {
                if (Messages[i].IsNewMessageDivider)
                {
                    Messages.RemoveAt(i);
                    break;
                }
            }
            _dividerMessageId = null;
            OnPropertyChanged(nameof(HasNewMessageDivider));
        }

        public void MarkMessagesAsRead()
        {
            if (SelectedConversation == null || Messages.Count == 0) return;

            var lastMessage = Messages.LastOrDefault(m => !m.IsNewMessageDivider);
            if (lastMessage == null) return;

            _lastReadMessageId = lastMessage.Id;

            if (_chatCacheService != null)
            {
                _chatCacheService.SetLastReadMessageId(SelectedConversation.Id, lastMessage.Id);
            }

            RemoveNewMessageDivider();

            SelectedConversation.UnreadCount = 0;

            if (_chatCacheService != null)
            {
                _chatCacheService.MarkConversationAsRead(SelectedConversation.Id);
                _chatCacheService.UpdateConversationUnreadCount(SelectedConversation.Id, 0);
            }

            if (_chatHttpService != null)
            {
                _ = _chatHttpService.MarkAsReadAsync(SelectedConversation.Id, lastMessage.Id);
            }

            MessagesRead?.Invoke(this, EventArgs.Empty);

            Debug.WriteLine($"[ChatPageViewModel] 会话 {SelectedConversation.Id} 已标记为已读，最后消息ID: {lastMessage.Id}");
        }

        public void UpdateWindowState(bool isActive, bool isMinimized)
        {
            _isWindowActive = isActive;
            _isWindowMinimized = isMinimized;
        }

        private bool CanSendMessage()
        {
            return SelectedConversation != null && 
                   !string.IsNullOrWhiteSpace(MessageInput) && 
                   !IsSending &&
                   (_chatHttpService != null || (_chatSocketService != null && _chatSocketService.IsConnected));
        }

        private async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(MessageInput) || SelectedConversation == null)
                return;

            try
            {
                IsSending = true;
                var content = MessageInput;
                
                var checkResult = await _profanityCheckService.CheckTextAsync(content);
                if (checkResult != null && checkResult.HasForbiddenWords)
                {
                    var forbiddenWordsStr = string.Join("、", checkResult.ForbiddenWords);
                    _toastService.ShowWarning($"消息包含违禁词: {forbiddenWordsStr}");
                    Debug.WriteLine($"[ChatPageViewModel] 消息包含违禁词，已阻止发送: {forbiddenWordsStr}");
                    return;
                }
                
                MessageInput = "";

                var extraData = new Dictionary<string, object>();
                if (_mentions.Count > 0)
                {
                    extraData["mentions"] = _mentions.Select(m => new { id = m.UserId, name = m.Name }).ToList();
                    Debug.WriteLine($"[ChatPageViewModel] 发送消息包含 {_mentions.Count} 个@");
                    _mentions.Clear();
                }

                var tempMessage = new ChatMessage
                {
                    Id = -(int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % int.MaxValue),
                    ConversationId = SelectedConversation.Id,
                    SenderDeviceId = _configService.Config.DeviceId,
                    SenderName = _configService.Config.BindInfo?.ClassName ?? "我",
                    Content = content,
                    SentAt = DateTime.Now,
                    IsMine = true,
                    Status = MessageStatus.Sending
                };

                Messages.Add(tempMessage);

                ChatMessage? sentMessage = null;

                if (_chatHttpService != null)
                {
                    sentMessage = await _chatHttpService.SendMessageAsync(
                        SelectedConversation.Id,
                        content,
                        "text",
                        null,
                        extraData.Count > 0 ? extraData : null
                    );
                }
                else if (_chatSocketService != null && _chatSocketService.IsConnected)
                {
                    var result = await _chatSocketService.SendMessageAsync(
                        SelectedConversation.Id,
                        content,
                        "text"
                    );
                    
                    if (!result.Success)
                    {
                        tempMessage.Status = MessageStatus.Failed;
                        _toastService.ShowError(result.ErrorMessage ?? "发送失败");
                    }
                }

                if (sentMessage != null)
                {
                    sentMessage.IsMine = true;
                    
                    var index = Messages.IndexOf(tempMessage);
                    if (index >= 0)
                    {
                        Messages[index] = sentMessage;
                    }

                    if (_chatCacheService != null)
                    {
                        _chatCacheService.AddMessage(SelectedConversation.Id, sentMessage);
                    }
                }
                else if (_chatCacheService != null)
                {
                    _chatCacheService.AddMessage(SelectedConversation.Id, tempMessage);
                }

                SelectedConversation.LastMessage = content;
                SelectedConversation.LastMessageTime = DateTime.Now;

                if (_chatCacheService != null)
                {
                    _chatCacheService.UpdateConversationLastMessage(
                        SelectedConversation.Id, 
                        content, 
                        DateTime.Now
                    );
                }

                MarkMessagesAsRead();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatPageViewModel] 发送消息异常: {ex.Message}");
                _toastService.ShowError($"发送消息异常: {ex.Message}");
            }
            finally
            {
                IsSending = false;
                (SendMessageCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
            }
        }

        private void OnMessageReceived(ChatMessage message)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var currentDeviceId = _configService.Config.DeviceId;
                var isMyMessage = !string.IsNullOrEmpty(message.SenderDeviceId) && 
                                  message.SenderDeviceId == currentDeviceId;

                Debug.WriteLine($"[ChatPageViewModel] 收到消息: Id={message.Id}, SenderDeviceId={message.SenderDeviceId}, CurrentDeviceId={currentDeviceId}, IsMine={isMyMessage}");

                if (SelectedConversation != null && 
                    message.ConversationId == SelectedConversation.Id)
                {
                    var existingIndex = -1;
                    for (var i = 0; i < Messages.Count; i++)
                    {
                        if (Messages[i].Id == message.Id)
                        {
                            existingIndex = i;
                            break;
                        }
                    }

                    if (existingIndex >= 0)
                    {
                        message.IsMine = isMyMessage;
                        Messages[existingIndex] = message;
                        
                        if (_chatCacheService != null)
                        {
                            _chatCacheService.AddMessage(message.ConversationId, message);
                        }
                        return;
                    }

                    if (isMyMessage)
                    {
                        for (var i = 0; i < Messages.Count; i++)
                        {
                            var msg = Messages[i];
                            if (msg.Id < 0 && 
                                msg.Content == message.Content &&
                                msg.IsMine &&
                                Math.Abs((msg.SentAt - message.SentAt).TotalSeconds) < 30)
                            {
                                message.IsMine = true;
                                Messages[i] = message;
                                
                                if (_chatCacheService != null)
                                {
                                    _chatCacheService.AddMessage(message.ConversationId, message);
                                }
                                return;
                            }
                        }
                    }

                    message.IsMine = isMyMessage;
                    Messages.Add(message);

                    if (_chatCacheService != null)
                    {
                        _chatCacheService.AddMessage(message.ConversationId, message);
                    }

                    if (!isMyMessage && _isWindowActive && !_isWindowMinimized)
                    {
                        if (_dividerMessageId == null && _lastReadMessageId != null)
                        {
                            InsertNewMessageDivider();
                        }
                    }
                }
                else
                {
                    UpdateConversationUnreadCount(message.ConversationId);

                    if (_chatCacheService != null)
                    {
                        _chatCacheService.AddMessage(message.ConversationId, message);
                        _chatCacheService.UpdateConversationLastMessage(
                            message.ConversationId,
                            message.Content,
                            message.SentAt
                        );
                    }
                    
                    if (!isMyMessage)
                    {
                        NewMessageReceived?.Invoke(this, message);
                    }
                }
            });
        }

        private void OnTypingStarted(string conversationId, string senderName)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (SelectedConversation != null && conversationId == SelectedConversation.Id)
                {
                    TypingUserName = senderName;
                    IsTyping = true;
                }
            });
        }

        private void OnTypingStopped(string conversationId)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (SelectedConversation != null && conversationId == SelectedConversation.Id)
                {
                    IsTyping = false;
                }
            });
        }

        private void UpdateConversationUnreadCount(string conversationId)
        {
            foreach (var conv in Conversations)
            {
                if (conv.Id == conversationId)
                {
                    conv.UnreadCount++;

                    if (_chatCacheService != null)
                    {
                        _chatCacheService.UpdateConversationUnreadCount(conversationId, conv.UnreadCount);
                    }
                    break;
                }
            }
        }

        private void ShowMentionPanel()
        {
            if (SelectedConversation == null) return;

            IsMentionPanelVisible = true;
            MentionSearchText = "";
            _ = LoadMentionMembersAsync();
        }

        private async Task LoadMentionMembersAsync()
        {
            if (_chatHttpService == null || SelectedConversation == null) return;

            try
            {
                var conversationDetail = await _chatHttpService.GetConversationDetailAsync(SelectedConversation.Id);

                if (conversationDetail?.Participants != null)
                {
                    var currentDeviceId = _configService.Config.DeviceId;
                    var filteredMembers = conversationDetail.Participants
                        .Where(p => p.DeviceId != currentDeviceId)
                        .ToList();

                    MentionMembers.Clear();
                    foreach (var member in filteredMembers)
                    {
                        MentionMembers.Add(member);
                    }

                    Debug.WriteLine($"[ChatPageViewModel] 加载了 {MentionMembers.Count} 个可@成员");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatPageViewModel] 加载成员列表失败: {ex.Message}");
                _toastService.ShowError("加载成员列表失败");
            }
        }

        private void FilterMentionMembers()
        {
            if (_chatHttpService == null || SelectedConversation == null) return;

            if (string.IsNullOrWhiteSpace(MentionSearchText))
            {
                _ = LoadMentionMembersAsync();
                return;
            }

            try
            {
                var filtered = MentionMembers
                    .Where(m => m.Name?.Contains(MentionSearchText, StringComparison.OrdinalIgnoreCase) == true)
                    .ToList();

                MentionMembers.Clear();
                foreach (var member in filtered)
                {
                    MentionMembers.Add(member);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatPageViewModel] 过滤成员列表失败: {ex.Message}");
            }
        }

        private void SelectMentionMember(Participant? member)
        {
            if (member == null) return;

            var mentionItem = new MentionItem
            {
                UserId = member.UserId ?? 0,
                Name = member.Name ?? "未知用户"
            };

            if (!_mentions.Any(m => m.UserId == mentionItem.UserId))
            {
                _mentions.Add(mentionItem);
            }

            MessageInput += $"@{mentionItem.Name} ";
            IsMentionPanelVisible = false;

            Debug.WriteLine($"[ChatPageViewModel] 选择@成员: {mentionItem.Name}, 当前@数量: {_mentions.Count}");
        }

        public void Dispose()
        {
            if (_chatSocketService != null && SelectedConversation != null)
            {
                _ = _chatSocketService.LeaveConversationAsync(SelectedConversation.Id);
            }
        }
    }
}
