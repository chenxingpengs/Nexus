using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Nexus.Models.Meeting;
using Nexus.Services.Meeting;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Nexus.ViewModels
{
    public class MeetingRoomViewModel : ViewModelBase, IDisposable
    {
        private readonly VideoFrameDecoder _frameDecoder;
        private readonly MeetingService? _meetingService;
        private WriteableBitmap? _videoFrame;
        private string _meetingTitle = "";
        string _meetingId = "";
        string _hostName = "";
        string _hostIp = "";
        int _broadcastPort;
        string _meetingKey = "";
        string _connectionStatus = "已连接";
        bool _isNoSignal = true;
        bool _disposed;
        int _frameCount;
        DateTime _meetingStartTime;
        List<ParticipantInfo> _participants = new();
        bool _isSidebarVisible = true;

        public WriteableBitmap? VideoFrame
        {
            get => _videoFrame;
            private set
            {
                if (SetProperty(ref _videoFrame, value))
                {
                    IsNoSignal = value == null;
                }
            }
        }

        public string MeetingTitle
        {
            get => _meetingTitle;
            set => SetProperty(ref _meetingTitle, value);
        }

        public string MeetingId
        {
            get => _meetingId;
            set => SetProperty(ref _meetingId, value);
        }

        public string HostName
        {
            get => _hostName;
            set => SetProperty(ref _hostName, value);
        }

        public string HostIp
        {
            get => _hostIp;
            set => SetProperty(ref _hostIp, value);
        }

        public int BroadcastPort
        {
            get => _broadcastPort;
            set => SetProperty(ref _broadcastPort, value);
        }

        public string MeetingKey
        {
            get => _meetingKey;
            set => SetProperty(ref _meetingKey, value);
        }

        public string ConnectionStatus
        {
            get => _connectionStatus;
            set => SetProperty(ref _connectionStatus, value);
        }

        public bool IsNoSignal
        {
            get => _isNoSignal;
            set => SetProperty(ref _isNoSignal, value);
        }

        public DateTime MeetingStartTime
        {
            get => _meetingStartTime;
            set => SetProperty(ref _meetingStartTime, value);
        }

        public string MeetingDurationFormatted
        {
            get
            {
                if (_meetingStartTime == default)
                    return "00:00:00";
                
                var duration = DateTime.Now - _meetingStartTime;
                return $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
            }
        }

        public List<ParticipantInfo> Participants
        {
            get => _participants;
            set => SetProperty(ref _participants, value);
        }

        public int AcceptedParticipantsCount => _participants.Count(p => p.Status == "accepted");
        public int TotalParticipantsCount => _participants.Count;

        public bool IsSidebarVisible
        {
            get => _isSidebarVisible;
            set => SetProperty(ref _isSidebarVisible, value);
        }

        public ICommand LeaveCommand { get; }
        public ICommand ToggleSidebarCommand { get; }
        public ICommand RefreshParticipantsCommand { get; }

        public event Action? LeaveRequested;

        public MeetingRoomViewModel()
        {
            _frameDecoder = new VideoFrameDecoder();
            LeaveCommand = new RelayCommand(OnLeave);
            ToggleSidebarCommand = new RelayCommand(OnToggleSidebar);
            RefreshParticipantsCommand = new RelayCommand(OnRefreshParticipants);
        }

        public MeetingRoomViewModel(MeetingService meetingService) : this()
        {
            _meetingService = meetingService;
        }

        public void InitializeMeeting(MeetingInfo meetingInfo)
        {
            MeetingTitle = meetingInfo.Title ?? "会议";
            MeetingId = meetingInfo.MeetingId;
            HostName = meetingInfo.HostName ?? "未知";
            HostIp = meetingInfo.HostIp ?? "-";
            BroadcastPort = meetingInfo.BroadcastPort;
            MeetingKey = meetingInfo.MeetingKey ?? "";
            MeetingStartTime = DateTime.Now;
            Participants = meetingInfo.Participants ?? new List<ParticipantInfo>();
            
            ConnectionStatus = "已连接";
        }

        public void UpdateFrame(byte[] frameData)
        {
            if (_disposed)
                return;

            var (bitmap, isNew) = _frameDecoder.DecodeFrame(frameData);

            if (bitmap != null)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_disposed)
                        return;
                        
                    try
                    {
                        _frameCount++;
                        
                        if (isNew)
                        {
                            VideoFrame = bitmap;
                        }
                        else
                        {
                            OnPropertyChanged(nameof(VideoFrame));
                        }
                        
                        ConnectionStatus = $"视频接收中 | 帧数: #{_frameCount} | 时长: {MeetingDurationFormatted}";
                        IsNoSignal = false;
                        OnPropertyChanged(nameof(MeetingDurationFormatted));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MeetingRoomViewModel] 更新帧失败: {ex.Message}");
                    }
                });
            }
        }

        private void OnLeave()
        {
            LeaveRequested?.Invoke();
        }

        private void OnToggleSidebar()
        {
            IsSidebarVisible = !IsSidebarVisible;
        }

        private async void OnRefreshParticipants()
        {
            if (_meetingService == null || string.IsNullOrEmpty(MeetingId))
                return;

            try
            {
                ConnectionStatus = "刷新参与者列表...";
                var response = await _meetingService.GetMeetingDetailAsync(MeetingId);
                
                if (response.Success && response.Data != null)
                {
                    Participants = response.Data.Participants ?? new List<ParticipantInfo>();
                    ConnectionStatus = $"已连接 - {AcceptedParticipantsCount}/{TotalParticipantsCount} 人在线";
                    OnPropertyChanged(nameof(AcceptedParticipantsCount));
                    OnPropertyChanged(nameof(TotalParticipantsCount));
                }
                else
                {
                    ConnectionStatus = "刷新失败";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MeetingRoomViewModel] 刷新参与者失败: {ex.Message}");
                ConnectionStatus = "刷新失败";
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _frameDecoder.Dispose();
                _disposed = true;
            }
        }
    }
}
