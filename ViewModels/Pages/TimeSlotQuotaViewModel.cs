using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexus.Models;
using Nexus.Services;
using Nexus.Services.Http;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Nexus.ViewModels.Pages
{
    public partial class TimeSlotQuotaViewModel : ViewModelBase
    {
        private readonly ConfigService _configService;
        private readonly AuthService _authService;
        private readonly HttpService _httpService;

        public int ClassId => _configService.Config.BindInfo?.ClassId ?? 0;
        public string ClassName => _configService.Config.BindInfo?.ClassName ?? "未绑定";
        public bool IsBound => _configService.Config.IsBound;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowContent))]
        [NotifyPropertyChangedFor(nameof(ShowSaveButton))]
        private bool _isLoading;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowSaveButton))]
        private bool _isSaving;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowContent))]
        private bool _hasError;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private int _studentCount;

        [ObservableProperty]
        private ObservableCollection<TimeSlotQuotaItem> _quotaItems = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowContent))]
        [NotifyPropertyChangedFor(nameof(ShowSaveButton))]
        private bool _contentLoaded;

        public bool ShowContent => !IsLoading && !HasError && ContentLoaded;
        public bool ShowSaveButton => !IsLoading && !IsSaving && QuotaItems.Count > 0;
        public bool HasIncompleteQuotas => QuotaItems.Any(item => item.NeedsConfiguration);

        private RelayCommand? _saveCommand;
        public RelayCommand SaveCommand => _saveCommand ??= new RelayCommand(OnSave, CanSave);

        public TimeSlotQuotaViewModel(ConfigService configService, AuthService authService)
        {
            _configService = configService;
            _authService = authService;
            _httpService = authService;
        }

        public async Task InitializeAsync()
        {
            if (IsBound && ClassId > 0)
            {
                await LoadQuotasAsync();
            }
        }

        private async Task LoadQuotasAsync()
        {
            try
            {
                IsLoading = true;
                HasError = false;
                ErrorMessage = string.Empty;

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
                        StudentCount = QuotaItems[0].StudentCount;
                    }
                    ContentLoaded = true;
                    OnPropertyChanged(nameof(ShowSaveButton));
                    OnPropertyChanged(nameof(HasIncompleteQuotas));
                    SaveCommand.NotifyCanExecuteChanged();
                }
                else
                {
                    HasError = true;
                    ErrorMessage = response?.Msg ?? "获取时段人数配置失败";
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

        private bool CanSave()
        {
            return IsBound && ClassId > 0 && !IsLoading && !IsSaving && QuotaItems.Count > 0;
        }

        private async void OnSave()
        {
            if (!CanSave()) return;

            try
            {
                IsSaving = true;

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
                    ErrorMessage = "保存失败: " + response.Msg;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "保存失败: " + ex.Message;
            }
            finally
            {
                IsSaving = false;
                SaveCommand.NotifyCanExecuteChanged();
            }
        }

        partial void OnIsLoadingChanged(bool value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsSavingChanged(bool value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }
    }
}
