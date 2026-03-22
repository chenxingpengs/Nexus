using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Nexus.Models.Widget;
using Nexus.Models.Attendance;
using Nexus.Services;
using Nexus.Services.Attendance;
using Nexus.Views.Widget;
using Nexus.ViewModels.Widget.Cards;

namespace Nexus.Services.Widget
{
    public class WidgetService
    {
        private readonly ConfigService _configService;
        private readonly WeatherService _weatherService;
        private readonly CitySearchService _citySearchService;
        private readonly ShortcutService _shortcutService;
        private readonly AttendanceService? _attendanceService;
        private readonly SocketIOService? _socketIOService;
        private DesktopWidgetWindow? _widgetWindow;
        private bool _userManuallyShownAttendance;
        
        public event EventHandler<WeatherCardModel>? WeatherUpdated;
        public event EventHandler<ShortcutCardModel>? ShortcutUpdated;
        public event EventHandler<AttendanceCardViewModel>? AttendanceUpdated;
        
        public WeatherCardModel? WeatherData { get; private set; }
        public AnnouncementCardModel? AnnouncementData { get; private set; }
        public AttendanceCardViewModel? AttendanceViewModel { get; private set; }
        public ShortcutCardModel? ShortcutData { get; private set; }

        public WidgetService(ConfigService configService, SocketIOService? socketIOService = null)
        {
            _configService = configService;
            _weatherService = new WeatherService(configService);
            _citySearchService = new CitySearchService();
            _shortcutService = new ShortcutService();
            _socketIOService = socketIOService;
            
            _attendanceService = new AttendanceService(configService);
            _attendanceService.AttendanceDataUpdated += OnAttendanceDataUpdated;
            _attendanceService.TimeSlotChanged += OnTimeSlotChanged;
            _attendanceService.LeaveAttendanceTime += OnLeaveAttendanceTime;
            _attendanceService.ErrorOccurred += OnAttendanceError;
            
            if (_socketIOService != null)
            {
                _socketIOService.AttendanceUpdated += OnWebSocketAttendanceUpdate;
            }
            
            _weatherService.DataUpdated += OnWeatherDataUpdated;
            _shortcutService.UsbDrivesChanged += OnUsbDrivesChanged;
        }

        private void OnWeatherDataUpdated(object? sender, WeatherCardModel e)
        {
            WeatherData = e;
            WeatherUpdated?.Invoke(this, e);
            
            if (_widgetWindow != null)
            {
                _widgetWindow.UpdateWeatherData(e);
            }
        }

        private void OnUsbDrivesChanged(object? sender, EventArgs e)
        {
            RefreshShortcutData();
        }

        private void OnAttendanceDataUpdated(object? sender, AttendanceData data)
        {
            if (AttendanceViewModel == null) return;

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                AttendanceViewModel.UpdateFromAttendanceData(data);
                UpdateAttendanceVisibility(data);
                AttendanceUpdated?.Invoke(this, AttendanceViewModel);
                _widgetWindow?.UpdateAttendanceData(AttendanceViewModel);
            });
        }

        private void OnTimeSlotChanged(object? sender, TimeSlot slot)
        {
            Debug.WriteLine($"[WidgetService] 时段变化: {slot.Name}");
            
            if (_attendanceService != null)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        Debug.WriteLine($"[WidgetService] 时段变化，获取考勤数据");
                        await _attendanceService.GetCurrentAttendanceAsync();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[WidgetService] 获取考勤数据失败: {ex.Message}");
                    }
                });
            }
            
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (_widgetWindow == null) return;
                
                if (!_userManuallyShownAttendance)
                {
                    Debug.WriteLine($"[WidgetService] 自动显示考勤卡片");
                    _widgetWindow.SetAttendanceCardVisibility(true);
                }
            });
        }

        private void OnLeaveAttendanceTime(object? sender, EventArgs e)
        {
            Debug.WriteLine("[WidgetService] 离开考勤时段");
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                UpdateAttendanceVisibility(null);
            });
        }

        private void OnAttendanceError(object? sender, string error)
        {
            Debug.WriteLine($"[WidgetService] 考勤错误: {error}");
        }

        private void OnWebSocketAttendanceUpdate(object? sender, JsonElement data)
        {
            Debug.WriteLine($"[WidgetService] 收到WebSocket考勤更新: {data}");
            
            try
            {
                if (data.TryGetProperty("data", out var dataElement))
                {
                    var classId = dataElement.GetProperty("classId").GetInt32();
                    var timeSlotId = dataElement.GetProperty("timeSlotId").GetInt32();
                    
                    if (_attendanceService != null && 
                        _attendanceService.CurrentClassId == classId &&
                        _attendanceService.CurrentTimeSlotId == timeSlotId)
                    {
                        _ = _attendanceService.RefreshAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WidgetService] 解析WebSocket考勤更新失败: {ex.Message}");
            }
        }

        private void UpdateAttendanceVisibility(AttendanceData? data)
        {
            if (_widgetWindow == null) return;

            var isAttendanceTime = data?.IsAttendanceTime ?? false;
            var config = _configService.GetWidgetConfig();
            var attendanceConfig = config.Cards.Find(c => c.Type == CardType.Attendance);
            
            if (isAttendanceTime)
            {
                if (!_userManuallyShownAttendance)
                {
                    _widgetWindow.SetAttendanceCardVisibility(true);
                }
            }
            else
            {
                if (!_userManuallyShownAttendance)
                {
                    _widgetWindow.SetAttendanceCardVisibility(false);
                }
            }
        }

        public void OnUserShowAttendanceCard()
        {
            _userManuallyShownAttendance = true;
        }

        public void OnUserHideAttendanceCard()
        {
            _userManuallyShownAttendance = false;
        }

        public async Task InitializeAsync()
        {
            Debug.WriteLine("[WidgetService] InitializeAsync 开始");
            WeatherData = await _weatherService.GetInitialDataAsync();
            AnnouncementData = new AnnouncementCardModel();
            AttendanceViewModel = new AttendanceCardViewModel(new AttendanceCardModel(), _attendanceService);
            
            ShortcutData = new ShortcutCardModel();
            RefreshShortcutData();

            if (_attendanceService != null)
            {
                var classId = _configService.GetClassId();
                Debug.WriteLine($"[WidgetService] _attendanceService 不为 null, classId={classId}");
                if (classId > 0)
                {
                    Debug.WriteLine($"[WidgetService] 调用 _attendanceService.InitializeAsync({classId})");
                    await _attendanceService.InitializeAsync(classId);
                }
                else
                {
                    Debug.WriteLine("[WidgetService] classId <= 0, 跳过初始化");
                }
            }
            else
            {
                Debug.WriteLine("[WidgetService] _attendanceService 为 null!");
            }
            Debug.WriteLine("[WidgetService] InitializeAsync 完成");
        }

        private void RefreshShortcutData()
        {
            if (ShortcutData == null)
                return;

            var drives = _shortcutService.GetUsbDrives();
            ShortcutData.UpdateUsbDrives(drives);

            var (type, name, path) = _shortcutService.GetDocumentCamera();
            System.Diagnostics.Debug.WriteLine($"DocumentCamera result: type={type}, name={name}, path={path}");
            ShortcutData.HasDocumentCamera = type != DocumentCameraType.None;
            ShortcutData.DocumentCameraName = name;
            ShortcutData.DocumentCameraType = type;

            ShortcutUpdated?.Invoke(this, ShortcutData);
            
            _widgetWindow?.RefreshShortcutData(ShortcutData);
        }

        public ShortcutService GetShortcutService()
        {
            return _shortcutService;
        }

        public AttendanceService? GetAttendanceService()
        {
            return _attendanceService;
        }

        public void ShowWidget()
        {
            if (_widgetWindow == null)
            {
                _widgetWindow = new DesktopWidgetWindow(this);
            }
            _widgetWindow.Show();
            _widgetWindow.Activate();
        }

        public void HideWidget()
        {
            _widgetWindow?.Hide();
        }

        public void CloseWidget()
        {
            if (_widgetWindow != null)
            {
                _widgetWindow.ForceClose();
                _widgetWindow = null;
            }
        }

        public WidgetConfig GetConfig()
        {
            return _configService.GetWidgetConfig();
        }

        public void SetWidgetEnabled(bool enabled)
        {
            _configService.UpdateWidgetEnabled(enabled);
        }

        public void SetWidgetOpacity(double opacity)
        {
            _configService.UpdateWidgetOpacity(opacity);
            if (_widgetWindow != null)
            {
                _widgetWindow.UpdateOpacity(opacity);
            }
        }

        public async Task SetWeatherLocationAsync(string cityId, string cityName, string province)
        {
            _configService.UpdateWeatherLocation(cityId, cityName, province);
            WeatherData = await _weatherService.GetInitialDataAsync();
            WeatherUpdated?.Invoke(this, WeatherData);
            
            if (_widgetWindow != null && WeatherData != null)
            {
                _widgetWindow.UpdateWeatherData(WeatherData);
                _widgetWindow.RefreshPosition();
            }
        }

        public void ClearWeatherLocation()
        {
            _configService.ClearWeatherLocation();
            WeatherData = new WeatherCardModel
            {
                IsNotConfigured = true,
                Message = "未设置地理位置，请在设置中配置"
            };
            WeatherUpdated?.Invoke(this, WeatherData);
            
            if (_widgetWindow != null && WeatherData != null)
            {
                _widgetWindow.UpdateWeatherData(WeatherData);
                _widgetWindow.RefreshPosition();
            }
        }

        public void RefreshWindowPosition()
        {
            _widgetWindow?.RefreshPosition();
        }

        public CitySearchService GetCitySearchService()
        {
            return _citySearchService;
        }

        public async Task<string?> GetAutoLocationAsync()
        {
            return await _weatherService.GetAutoLocationAsync();
        }

        public async Task RefreshAttendanceAsync()
        {
            if (_attendanceService != null)
            {
                await _attendanceService.RefreshAsync();
            }
        }

        public void Stop()
        {
            _weatherService.Stop();
            _shortcutService.Dispose();
            _attendanceService?.Dispose();
            CloseWidget();
        }
    }
}
