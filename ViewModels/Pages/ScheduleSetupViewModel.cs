using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexus.Models.Schedule;
using Nexus.Services;

namespace Nexus.ViewModels.Pages
{
    public partial class ScheduleSetupViewModel : ViewModelBase
    {
        private readonly ScheduleService _scheduleService;
        private readonly ConfigService _configService;

        [ObservableProperty]
        private ObservableCollection<TimeSlotScheduleItem> _timeSlotItems = new();

        [ObservableProperty]
        private ObservableCollection<TeacherModel> _teachers = new();

        [ObservableProperty]
        private ObservableCollection<TeacherModel> _filteredTeachers = new();

        [ObservableProperty]
        private string _teacherSearchText = string.Empty;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private int _classId;

        [ObservableProperty]
        private string _className = string.Empty;

        [ObservableProperty]
        private bool _isComplete;

        [ObservableProperty]
        private int _configuredCount;

        [ObservableProperty]
        private int _totalCount;

        public bool HasMissingSlots => !IsComplete && TotalCount > 0;

        public event Action? SetupCompleted;
        public event Action? RequestSkip;

        public ScheduleSetupViewModel(ScheduleService scheduleService, ConfigService configService)
        {
            _scheduleService = scheduleService;
            _configService = configService;

            _scheduleService.StatusChanged += OnStatusChanged;
            _scheduleService.LoadingStateChanged += OnLoadingStateChanged;
        }

        private void OnStatusChanged(string status)
        {
            StatusMessage = status;
        }

        private void OnLoadingStateChanged(bool isLoading)
        {
            IsLoading = isLoading;
        }

        public async Task InitializeAsync(int classId, string className)
        {
            ClassId = classId;
            ClassName = className;
            StatusMessage = "正在加载排班配置...";

            var teachers = await _scheduleService.GetTeachersAsync();
            if (teachers != null && teachers.Count > 0)
            {
                Teachers.Clear();
                foreach (var teacher in teachers)
                {
                    Teachers.Add(teacher);
                }
                FilteredTeachers.Clear();
                foreach (var teacher in Teachers)
                {
                    FilteredTeachers.Add(teacher);
                }
            }

            var completeness = await _scheduleService.CheckCompletenessAsync(classId);
            if (completeness == null)
            {
                StatusMessage = "加载排班配置失败";
                return;
            }

            IsComplete = completeness.IsComplete;
            TotalCount = completeness.FixedTimeSlots.Count * 5;
            ConfiguredCount = TotalCount - completeness.MissingSlots.Count;

            BuildTimeSlotItems(completeness);

            OnPropertyChanged(nameof(HasMissingSlots));
        }

        partial void OnTeacherSearchTextChanged(string value)
        {
            FilterTeachers(value);
        }

        private void FilterTeachers(string searchText)
        {
            FilteredTeachers.Clear();
            
            if (string.IsNullOrEmpty(searchText))
            {
                foreach (var teacher in Teachers)
                {
                    FilteredTeachers.Add(teacher);
                }
            }
            else
            {
                var search = searchText.ToLower();
                foreach (var teacher in Teachers)
                {
                    if (teacher.DisplayName.ToLower().Contains(search) ||
                        teacher.Username.ToLower().Contains(search))
                    {
                        FilteredTeachers.Add(teacher);
                    }
                }
            }
        }

        private void BuildTimeSlotItems(ScheduleCompletenessModel completeness)
        {
            TimeSlotItems.Clear();

            foreach (var slot in completeness.FixedTimeSlots)
            {
                var item = new TimeSlotScheduleItem
                {
                    TimeSlot = slot
                };

                for (int weekday = 1; weekday <= 5; weekday++)
                {
                    var existingRule = completeness.ExistingRules.FirstOrDefault(
                        r => r.TimeSlotId == slot.Id && r.Weekday == weekday);

                    var isMissing = completeness.MissingSlots.Any(
                        m => m.TimeSlotId == slot.Id && m.Weekday == weekday);

                    var weekdayItem = new WeekdayScheduleItem
                    {
                        Weekday = weekday,
                        WeekdayName = WeekdayHelper.GetWeekdayName(weekday),
                        IsConfigured = existingRule != null,
                        ExistingRule = existingRule,
                        SelectedTeacherId = existingRule?.TeacherId,
                        IsMissing = isMissing,
                        FilteredTeachers = FilteredTeachers
                    };

                    item.WeekdayItems.Add(weekdayItem);
                }

                TimeSlotItems.Add(item);
            }
        }

        [RelayCommand]
        private async Task SaveAndContinue()
        {
            if (ClassId <= 0)
            {
                StatusMessage = "班级ID无效";
                return;
            }

            var rulesToAdd = new List<AddPeriodicRuleRequest>();

            foreach (var slotItem in TimeSlotItems)
            {
                foreach (var weekdayItem in slotItem.WeekdayItems)
                {
                    if (weekdayItem.IsMissing && weekdayItem.SelectedTeacherId.HasValue)
                    {
                        rulesToAdd.Add(new AddPeriodicRuleRequest
                        {
                            ClassId = ClassId,
                            TimeSlotId = slotItem.TimeSlot.Id,
                            Weekday = weekdayItem.Weekday,
                            TeacherId = weekdayItem.SelectedTeacherId.Value
                        });
                    }
                }
            }

            if (rulesToAdd.Count == 0)
            {
                StatusMessage = "没有需要保存的配置";
                SetupCompleted?.Invoke();
                return;
            }

            StatusMessage = $"正在保存 {rulesToAdd.Count} 条排班规则...";

            var result = await _scheduleService.BatchAddPeriodicRulesAsync(ClassId, rulesToAdd);

            if (result != null)
            {
                StatusMessage = $"成功添加 {result.AddedCount} 条规则";
                
                if (result.SkippedCount > 0)
                {
                    StatusMessage += $"，跳过 {result.SkippedCount} 条已存在的规则";
                }

                await Task.Delay(500);
                SetupCompleted?.Invoke();
            }
            else
            {
                StatusMessage = "保存失败，请重试";
            }
        }

        [RelayCommand]
        private void Skip()
        {
            RequestSkip?.Invoke();
        }

        [RelayCommand]
        private async Task Refresh()
        {
            await InitializeAsync(ClassId, ClassName);
        }
    }
}
