using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Nexus.Models.Widget;
using Nexus.Models.Attendance;
using Nexus.Services;
using Nexus.Services.Attendance;
using Nexus.Views.Widget;

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
        public event EventHandler<AttendanceCardModel>? AttendanceUpdated;
        
        public WeatherCardModel? WeatherData { get; private set; }
        public AnnouncementCardModel? AnnouncementData { get; private set; }
        public AttendanceCardModel? AttendanceData { get; private set; }
        public ShortcutCardModel? ShortcutData { get; private set; }

        public WidgetService(ConfigService configService, SocketIOService? socketIOService = null)
        {
            _configService = configService;
            _weatherService = new WeatherService(configService);
            _citySearchService = new CitySearchService();
            _shortcutService = new ShortcutService();
            _socketIOService = socketIOService;
            
            if (_socketIOService != null)
            {
                _attendanceService = new AttendanceService(configService);
                _attendanceService.AttendanceDataUpdated += OnAttendanceDataUpdated;
                _attendanceService.TimeSlotChanged += OnTimeSlotChanged;
                _attendanceService.LeaveAttendanceTime += OnLeaveAttendanceTime;
                _attendanceService.ErrorOccurred += OnAttendanceError;
                
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
            if (AttendanceData == null) return;

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                AttendanceData.UpdateFromAttendanceData(data);
                UpdateAttendanceVisibility(data);
                AttendanceUpdated?.Invoke(this, AttendanceData);
                _widgetWindow?.UpdateAttendanceData(AttendanceData);
            });
        }

        private void OnTimeSlotChanged(object? sender, TimeSlot slot)
        {
            Debug.WriteLine($"[WidgetService] 时段变化: {slot.Name}");
        }

        private void OnLeaveAttendanceTime(object? sender, EventArgs e)
        {
            Debug.WriteLine("[WidgetService] 离开考勤时段");
            UpdateAttendanceVisibility(null);
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
            WeatherData = await _weatherService.GetInitialDataAsync();
            AnnouncementData = new AnnouncementCardModel();
            AttendanceData = new AttendanceCardModel();
            
            ShortcutData = new ShortcutCardModel();
            RefreshShortcutData();

            if (_attendanceService != null)
            {
                var classId = _configService.GetClassId();
                if (classId > 0)
                {
                    await _attendanceService.InitializeAsync(classId);
                }
            }
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
