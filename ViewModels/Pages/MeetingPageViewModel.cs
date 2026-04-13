using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Nexus.Models.Meeting;
using Nexus.Services;
using Nexus.Services.Meeting;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Nexus.ViewModels.Pages
{
    public class MeetingPageViewModel : ViewModelBase, IDisposable
    {
        private readonly ConfigService _configService;
        private readonly MeetingService _meetingService;
        private readonly ToastService _toastService;
        private VideoReceiver? _videoReceiver;
        private SocketIOService? _socketIOService;

        private string _inputMeetingId = "";
        public string InputMeetingId
        {
            get => _inputMeetingId;
            set => SetProperty(ref _inputMeetingId, value);
        }

        private string _currentMeetingTitle = "";
        public string CurrentMeetingTitle
        {
            get => _currentMeetingTitle;
            set => SetProperty(ref _currentMeetingTitle, value);
        }

        private bool _isInMeeting;
        public bool IsInMeeting
        {
            get => _isInMeeting;
            set => SetProperty(ref _isInMeeting, value);
        }

        private string _meetingStatus = "无进行中的会议";
        public string MeetingStatus
        {
            get => _meetingStatus;
            set => SetProperty(ref _meetingStatus, value);
        }

        public ObservableCollection<MeetingHistory> MeetingHistory { get; }

        public ICommand JoinMeetingCommand { get; }
        public ICommand LeaveMeetingCommand { get; }
        public ICommand RefreshHistoryCommand { get; }

        public event Action? RequestShowMeetingRoom;

        public MeetingPageViewModel(ConfigService configService, ToastService toastService)
        {
            _configService = configService;
            _toastService = toastService;
            
            var serverUrl = configService.Config.ServerUrl;
            _meetingService = new MeetingService(serverUrl);
            
            if (!string.IsNullOrEmpty(configService.Config.AccessToken))
            {
                _meetingService.SetAuthToken(configService.Config.AccessToken);
            }

            MeetingHistory = new ObservableCollection<MeetingHistory>();

            JoinMeetingCommand = new AsyncRelayCommand(JoinMeetingAsync);
            LeaveMeetingCommand = new AsyncRelayCommand(LeaveMeetingAsync);
            RefreshHistoryCommand = new AsyncRelayCommand(LoadHistoryAsync);

            _ = LoadHistoryAsync();
        }

        public void SetSocketIOService(SocketIOService socketIOService)
        {
            _socketIOService = socketIOService;
        }

        private async Task JoinMeetingAsync()
        {
            if (string.IsNullOrWhiteSpace(InputMeetingId))
            {
                _toastService.ShowWarning("请输入会议ID");
                return;
            }

            if (IsInMeeting)
            {
                _toastService.ShowWarning("您已在会议中，请先离开当前会议");
                return;
            }

            try
            {
                var deviceId = _configService.Config.DeviceId;
                var classId = _configService.Config.BindInfo?.ClassId ?? 0;

                if (classId == 0)
                {
                    _toastService.ShowError("设备未绑定班级，无法加入会议");
                    return;
                }

                MeetingStatus = "正在加入会议...";
                
                var result = await _meetingService.JoinMeetingAsync(InputMeetingId, deviceId, classId);

                if (result.Success && result.Data != null)
                {
                    CurrentMeetingTitle = result.Data.Title;
                    IsInMeeting = true;
                    MeetingStatus = $"已加入: {result.Data.Title}";

                    _toastService.ShowSuccess($"已加入会议: {result.Data.Title}");

                    await StartReceivingVideoAsync(result.Data);
                    
                    RequestShowMeetingRoom?.Invoke();
                }
                else
                {
                    MeetingStatus = "加入失败";
                    _toastService.ShowError(result.Msg ?? "加入会议失败");
                }
            }
            catch (Exception ex)
            {
                MeetingStatus = "加入失败";
                _toastService.ShowError($"加入会议异常: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[MeetingPageViewModel] 加入会议异常: {ex.Message}");
            }
        }

        private async Task StartReceivingVideoAsync(JoinMeetingResponse meetingInfo)
        {
            try
            {
                _videoReceiver = new VideoReceiver();
                _videoReceiver.FrameReceived += OnFrameReceived;
                _videoReceiver.ErrorOccurred += OnVideoError;
                _videoReceiver.Started += OnVideoStarted;
                _videoReceiver.Stopped += OnVideoStopped;

                await _videoReceiver.StartReceivingAsync(
                    meetingInfo.BroadcastPort,
                    meetingInfo.MeetingKey
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MeetingPageViewModel] 启动视频接收失败: {ex.Message}");
                _toastService.ShowError($"启动视频接收失败: {ex.Message}");
            }
        }

        private void OnFrameReceived(object? sender, byte[] frame)
        {
            Dispatcher.UIThread.Post(() =>
            {
                // TODO: 将帧传递给视频播放控件
            });
        }

        private void OnVideoError(object? sender, string error)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _toastService.ShowError(error);
            });
        }

        private void OnVideoStarted(object? sender, EventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                System.Diagnostics.Debug.WriteLine("[MeetingPageViewModel] 视频接收已启动");
            });
        }

        private void OnVideoStopped(object? sender, EventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                System.Diagnostics.Debug.WriteLine("[MeetingPageViewModel] 视频接收已停止");
            });
        }

        private async Task LeaveMeetingAsync()
        {
            if (!IsInMeeting)
                return;

            try
            {
                var deviceId = _configService.Config.DeviceId;
                
                _videoReceiver?.StopReceiving();
                _videoReceiver?.Dispose();
                _videoReceiver = null;

                var result = await _meetingService.LeaveMeetingAsync(InputMeetingId, deviceId);

                IsInMeeting = false;
                CurrentMeetingTitle = "";
                MeetingStatus = "无进行中的会议";
                InputMeetingId = "";

                if (result.Success)
                {
                    _toastService.ShowSuccess("已离开会议");
                }
                else
                {
                    _toastService.ShowWarning(result.Msg ?? "离开会议失败");
                }

                await LoadHistoryAsync();
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"离开会议异常: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[MeetingPageViewModel] 离开会议异常: {ex.Message}");
            }
        }

        private async Task LoadHistoryAsync()
        {
            try
            {
                var classId = _configService.Config.BindInfo?.ClassId ?? 0;
                if (classId == 0)
                    return;

                var result = await _meetingService.GetMeetingHistoryAsync(classId);

                if (result.Success && result.Data != null)
                {
                    MeetingHistory.Clear();
                    foreach (var history in result.Data.List)
                    {
                        MeetingHistory.Add(history);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MeetingPageViewModel] 加载会议历史失败: {ex.Message}");
            }
        }

        public async Task AcceptInvitationAsync(MeetingInvitation invitation)
        {
            if (IsInMeeting)
            {
                _toastService.ShowWarning("您已在会议中，请先离开当前会议");
                return;
            }

            try
            {
                var deviceId = _configService.Config.DeviceId;
                var classId = _configService.Config.BindInfo?.ClassId ?? 0;

                var result = await _meetingService.AcceptInvitationAsync(
                    invitation.MeetingId,
                    deviceId,
                    classId
                );

                if (result.Success)
                {
                    InputMeetingId = invitation.MeetingId;
                    CurrentMeetingTitle = invitation.Title;
                    IsInMeeting = true;
                    MeetingStatus = $"已加入: {invitation.Title}";

                    _toastService.ShowSuccess($"已加入会议: {invitation.Title}");

                    var joinResult = await _meetingService.JoinMeetingAsync(
                        invitation.MeetingId,
                        deviceId,
                        classId
                    );

                    if (joinResult.Success && joinResult.Data != null)
                    {
                        await StartReceivingVideoAsync(joinResult.Data);
                        RequestShowMeetingRoom?.Invoke();
                    }
                }
                else
                {
                    _toastService.ShowError(result.Msg ?? "接受邀请失败");
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"接受邀请异常: {ex.Message}");
            }
        }

        public async Task RejectInvitationAsync(MeetingInvitation invitation)
        {
            try
            {
                var classId = _configService.Config.BindInfo?.ClassId ?? 0;
                await _meetingService.RejectInvitationAsync(invitation.MeetingId, classId);
                _toastService.ShowInfo("已拒绝会议邀请");
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"拒绝邀请异常: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _videoReceiver?.StopReceiving();
            _videoReceiver?.Dispose();
            _videoReceiver = null;
        }
    }
}
