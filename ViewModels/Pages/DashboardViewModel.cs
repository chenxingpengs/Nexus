using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Nexus.Services;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace Nexus.ViewModels.Pages
{
    public partial class DashboardViewModel : ViewModelBase
    {
        private readonly SocketIOService? _socketIOService;
        private readonly PowerControlService _powerControlService;

        private string _deviceStatus = "在线运行中";
        public string DeviceStatus
        {
            get => _deviceStatus;
            set => SetProperty(ref _deviceStatus, value);
        }

        private string _lastPowerControl = "无";
        public string LastPowerControl
        {
            get => _lastPowerControl;
            set => SetProperty(ref _lastPowerControl, value);
        }

        private bool _isProcessingPowerControl;
        public bool IsProcessingPowerControl
        {
            get => _isProcessingPowerControl;
            set => SetProperty(ref _isProcessingPowerControl, value);
        }

        public event EventHandler<string>? ShowNotification;

        public DashboardViewModel() : this(null)
        {
        }

        public DashboardViewModel(SocketIOService? socketIOService)
        {
            _socketIOService = socketIOService;
            _powerControlService = new PowerControlService();
            _powerControlService.PowerControlExecuted += OnPowerControlExecuted;

            if (_socketIOService != null)
            {
                _socketIOService.MessageReceived += OnSocketMessageReceived;
            }
        }

        private void OnSocketMessageReceived(object? sender, JsonElement message)
        {
            try
            {
                var type = message.TryGetProperty("type", out var typeElement)
                    ? typeElement.GetString()
                    : "";

                if (type == "power_control")
                {
                    var action = message.TryGetProperty("action", out var actionElement)
                        ? actionElement.GetString()
                        : "";

                    HandlePowerControlMessage(action);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DashboardViewModel] 解析消息失败: {ex.Message}");
            }
        }

        private void HandlePowerControlMessage(string? action)
        {
            var powerAction = PowerControlService.ParseActionFromString(action);
            if (powerAction == null)
            {
                System.Diagnostics.Debug.WriteLine($"[DashboardViewModel] 无效的电源操作: {action}");
                return;
            }

            Dispatcher.UIThread.Post(async () =>
            {
                IsProcessingPowerControl = true;

                var actionText = powerAction == PowerAction.Shutdown ? "关机" : "重启";
                ShowNotification?.Invoke(this, $"收到远程{actionText}指令，正在执行...");

                await Task.Run(() => _powerControlService.ExecutePowerControl(powerAction.Value));

                IsProcessingPowerControl = false;
            });
        }

        private void OnPowerControlExecuted(object? sender, PowerControlResult result)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var actionText = result.Action == PowerAction.Shutdown ? "关机" : "重启";
                LastPowerControl = $"{actionText} - {DateTime.Now:HH:mm:ss}";

                if (result.Success)
                {
                    ShowNotification?.Invoke(this, result.Message);
                }
                else
                {
                    ShowNotification?.Invoke(this, $"操作失败: {result.Message}");
                }
            });
        }

        [RelayCommand]
        private void RefreshStatus()
        {
            DeviceStatus = "在线运行中";
            ShowNotification?.Invoke(this, "状态已刷新");
        }

        public void Cleanup()
        {
            if (_socketIOService != null)
            {
                _socketIOService.MessageReceived -= OnSocketMessageReceived;
            }
            _powerControlService.PowerControlExecuted -= OnPowerControlExecuted;
        }
    }
}
