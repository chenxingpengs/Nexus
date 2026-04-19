using CommunityToolkit.Mvvm.Input;
using Nexus.Services;
using Nexus.Services.Http;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Nexus.ViewModels.Pages
{
    public partial class SettingsViewModel : ViewModelBase
    {
        private readonly ConfigService _configService;
        private readonly AuthService _authService;
        private readonly ScheduleService _scheduleService;
        private readonly HttpService _httpService;

        public string DeviceId => _configService.Config.DeviceId;
        public string DeviceName => _configService.Config.DeviceName;
        public string ClassName => _configService.Config.BindInfo?.ClassName ?? "未绑定";
        public string ServerUrl => _configService.Config.ServerUrl;
        public int ClassId => _configService.Config.BindInfo?.ClassId ?? 0;
        public bool IsBound => _configService.Config.IsBound;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(OpenScheduleConfigCommand))]
        private bool _isLoading;

        [ObservableProperty]
        private bool _hasMissingSlots;

        [ObservableProperty]
        private bool _scheduleComplete;

        [ObservableProperty]
        private int _configuredCount;

        [ObservableProperty]
        private int _totalCount;

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(QuotaShowContent))]
        [NotifyPropertyChangedFor(nameof(QuotaShowSaveButton))]
        private bool _quotaIsLoading;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(QuotaShowSaveButton))]
        private bool _quotaIsSaving;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(QuotaShowContent))]
        private bool _quotaHasError;

        [ObservableProperty]
        private string _quotaErrorMessage = string.Empty;

        [ObservableProperty]
        private int _quotaStudentCount;

        [ObservableProperty]
        private ObservableCollection<TimeSlotQuotaItem> _quotaItems = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(QuotaShowContent))]
        [NotifyPropertyChangedFor(nameof(QuotaShowSaveButton))]
        private bool _quotaContentLoaded;

        public string QuotaClassName => _configService.Config.BindInfo?.ClassName ?? "未绑定";
        public bool QuotaShowContent => !QuotaIsLoading && !QuotaHasError && QuotaContentLoaded;
        public bool QuotaShowSaveButton => !QuotaIsLoading && !QuotaIsSaving && QuotaItems.Count > 0;
        public bool HasIncompleteQuotas => QuotaItems.Any(item => item.NeedsConfiguration);

        public ICommand UnbindCommand { get; }

        private RelayCommand? _saveQuotaCommand;
        public RelayCommand SaveQuotaCommand => _saveQuotaCommand ??= new RelayCommand(OnSaveQuota, CanSaveQuota);

        public event Action? RequestLogout;
        public event Action<int, string>? RequestOpenScheduleConfig;

        public SettingsViewModel(ConfigService configService, AuthService authService, ScheduleService scheduleService)
        {
            _configService = configService;
            _authService = authService;
            _scheduleService = scheduleService;
            _httpService = authService;

            UnbindCommand = new RelayCommand(OnUnbind);
            
            OpenScheduleConfigCommand.NotifyCanExecuteChanged();
        }

        public async Task InitializeAsync()
        {
            if (IsBound && ClassId > 0)
            {
                await Task.WhenAll(LoadScheduleCompletenessAsync(), LoadQuotasAsync());
            }
        }

        #region 周期排班

        private async Task LoadScheduleCompletenessAsync()
        {
            try
            {
                IsLoading = true;
                HasError = false;
                ErrorMessage = string.Empty;

                var completeness = await _scheduleService.CheckCompletenessAsync(ClassId);
                
                if (completeness != null)
                {
                    HasMissingSlots = !completeness.IsComplete && completeness.MissingSlots.Count > 0;
                    ScheduleComplete = completeness.IsComplete;
                    TotalCount = completeness.FixedTimeSlots.Count * 5;
                    ConfiguredCount = TotalCount - completeness.MissingSlots.Count;
                }
                else
                {
                    HasError = true;
                    ErrorMessage = "获取排班配置失败，请检查网络连接";
                }
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = "加载失败: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private bool CanOpenScheduleConfig()
        {
            return IsBound && ClassId > 0 && !IsLoading;
        }

        [RelayCommand(CanExecute = nameof(CanOpenScheduleConfig))]
        private void OpenScheduleConfig()
        {
            if (IsBound && ClassId > 0)
            {
                RequestOpenScheduleConfig?.Invoke(ClassId, ClassName);
            }
        }

        #endregion

        #region 时段人数配置

        private async Task LoadQuotasAsync()
        {
            try
            {
                QuotaIsLoading = true;
                QuotaHasError = false;
                QuotaErrorMessage = string.Empty;

                var response = await _httpService.GetAsync<TimeSlotQuotaItem[]>(
                    $"/web/attendance/time-slot-quota/by-class/{ClassId}",
                    new RequestOptions { RequireAuth = true, ShowErrorToast = false }
                );

                if (response != null && response.IsSuccess && response.Data != null)
                {
                    QuotaItems.Clear();
                    foreach (var item in response.Data)
                    {
                        QuotaItems.Add(item);
                    }

                    if (QuotaItems.Count > 0)
                    {
                        QuotaStudentCount = QuotaItems[0].StudentCount;
                    }
                    QuotaContentLoaded = true;
                    OnPropertyChanged(nameof(QuotaShowSaveButton));
                    OnPropertyChanged(nameof(HasIncompleteQuotas));
                    SaveQuotaCommand.NotifyCanExecuteChanged();
                }
                else
                {
                    QuotaHasError = true;
                    QuotaErrorMessage = response?.Msg ?? "获取时段人数配置失败";
                }
            }
            catch (Exception ex)
            {
                QuotaHasError = true;
                QuotaErrorMessage = "加载失败: " + ex.Message;
            }
            finally
            {
                QuotaIsLoading = false;
            }
        }

        private bool CanSaveQuota()
        {
            return IsBound && ClassId > 0 && !QuotaIsLoading && !QuotaIsSaving && QuotaItems.Count > 0;
        }

        private async void OnSaveQuota()
        {
            if (!CanSaveQuota()) return;

            try
            {
                QuotaIsSaving = true;

                var quotas = new System.Collections.Generic.List<object>();
                foreach (var item in QuotaItems)
                {
                    quotas.Add(new { id = item.Id, quota = item.Quota });
                }

                var response = await _httpService.PutAsync<object>(
                    $"/web/attendance/time-slot-quota/by-class/{ClassId}",
                    new { quotas },
                    new RequestOptions { RequireAuth = true, ShowSuccessToast = true }
                );

                if (response != null && !response.IsSuccess)
                {
                    QuotaErrorMessage = "保存失败: " + response.Msg;
                }
            }
            catch (Exception ex)
            {
                QuotaErrorMessage = "保存失败: " + ex.Message;
            }
            finally
            {
                QuotaIsSaving = false;
                SaveQuotaCommand.NotifyCanExecuteChanged();
            }
        }

        partial void OnQuotaIsLoadingChanged(bool value)
        {
            SaveQuotaCommand.NotifyCanExecuteChanged();
        }

        partial void OnQuotaIsSavingChanged(bool value)
        {
            SaveQuotaCommand.NotifyCanExecuteChanged();
        }

        #endregion

        #region 通用

        private void OnUnbind()
        {
            _configService.ClearBindInfo();
            RequestLogout?.Invoke();
        }

        #endregion
    }

    public partial class TimeSlotQuotaItem : ObservableObject
    {
        public int Id { get; set; }
        public int TimeSlotId { get; set; }
        public string TimeSlotName { get; set; } = string.Empty;
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;

        [ObservableProperty]
        private int _quota;

        public int StudentCount { get; set; }
        
        public int InheritClassSize { get; set; }
        
        public bool NeedsConfiguration => InheritClassSize == 0 && Quota == 0;
    }
}
