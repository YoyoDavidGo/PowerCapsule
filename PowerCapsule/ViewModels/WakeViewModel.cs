using System;
using PowerCapsule.Models;
using PowerCapsule.Services;

namespace PowerCapsule.ViewModels
{
    public class WakeViewModel : ViewModelBase
    {
        private readonly WakeTaskService _wakeTaskService;

        private int _selectedHour = 8;
        public int SelectedHour
        {
            get => _selectedHour;
            set => SetProperty(ref _selectedHour, value);
        }

        private int _selectedMinute = 0;
        public int SelectedMinute
        {
            get => _selectedMinute;
            set => SetProperty(ref _selectedMinute, value);
        }

        private DateMode _selectedDateMode = DateMode.Tomorrow;
        public DateMode SelectedDateMode
        {
            get => _selectedDateMode;
            set => SetProperty(ref _selectedDateMode, value);
        }

        private bool _isWakeScheduled;
        public bool IsWakeScheduled
        {
            get => _isWakeScheduled;
            set => SetProperty(ref _isWakeScheduled, value);
        }

        private string _statusText = "未设置";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public WakeViewModel(WakeTaskService wakeTaskService)
        {
            _wakeTaskService = wakeTaskService;
            UpdateStatus();
        }

        public void Apply()
        {
            var wakeTime = new DateTime(
                DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day,
                SelectedHour, SelectedMinute, 0);

            if (SelectedDateMode == DateMode.Tomorrow)
                wakeTime = wakeTime.AddDays(1);

            if (SelectedDateMode == DateMode.Today && wakeTime <= DateTime.Now)
                wakeTime = wakeTime.AddDays(1);

            _wakeTaskService.ScheduleWake(wakeTime);
            UpdateStatus();
        }

        public void CancelWake()
        {
            _wakeTaskService.DeleteWakeTask();
            UpdateStatus();
        }

        public void UpdateStatus()
        {
            IsWakeScheduled = _wakeTaskService.IsWakeScheduled;
            StatusText = IsWakeScheduled
                ? LocalizationManager.L("St.WakeSet")
                : LocalizationManager.L("St.NotSet");
        }
    }
}
