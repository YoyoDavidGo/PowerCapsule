using System;
using System.ComponentModel;
using PowerCapsule.Models;
using PowerCapsule.Services;

namespace PowerCapsule.ViewModels
{
    public class ShutdownViewModel : ViewModelBase
    {
        private readonly ShutdownService _shutdownService;

        private ShutdownMode _selectedMode = ShutdownMode.FixedTime;
        public ShutdownMode SelectedMode
        {
            get => _selectedMode;
            set
            {
                SetProperty(ref _selectedMode, value);
                OnPropertyChanged(nameof(IsFixedTimeMode));
                OnPropertyChanged(nameof(IsCountdownMode));
            }
        }

        public bool IsFixedTimeMode => SelectedMode == ShutdownMode.FixedTime;
        public bool IsCountdownMode => SelectedMode == ShutdownMode.Countdown;

        // 固定时间
        private int _selectedHour = 23;
        public int SelectedHour
        {
            get => _selectedHour;
            set => SetProperty(ref _selectedHour, value);
        }

        private int _selectedMinute = 30;
        public int SelectedMinute
        {
            get => _selectedMinute;
            set => SetProperty(ref _selectedMinute, value);
        }

        private DateMode _selectedDateMode = DateMode.Today;
        public DateMode SelectedDateMode
        {
            get => _selectedDateMode;
            set => SetProperty(ref _selectedDateMode, value);
        }

        // 倒计时
        private int _countdownMinutes = 30;
        public int CountdownMinutes
        {
            get => _countdownMinutes;
            set
            {
                SetProperty(ref _countdownMinutes, value);
                OnPropertyChanged(nameof(EstimatedExecutionTime));
            }
        }

        public string EstimatedExecutionTime
        {
            get
            {
                var execTime = DateTime.Now.AddMinutes(_countdownMinutes);
                return $"预计 {execTime:yyyy/MM/dd HH:mm} 执行";
            }
        }

        private int _reminderSeconds = 60;
        public int ReminderSeconds
        {
            get => _reminderSeconds;
            set => SetProperty(ref _reminderSeconds, value);
        }

        private bool _isShutdownActive;
        public bool IsShutdownActive
        {
            get => _isShutdownActive;
            set => SetProperty(ref _isShutdownActive, value);
        }

        private string _activeShutdownInfo;
        public string ActiveShutdownInfo
        {
            get => _activeShutdownInfo;
            set => SetProperty(ref _activeShutdownInfo, value);
        }

        public ShutdownViewModel(ShutdownService shutdownService)
        {
            _shutdownService = shutdownService;
            UpdateActiveState();
        }

        public void Apply()
        {
            if (SelectedMode == ShutdownMode.FixedTime)
            {
                var targetTime = new DateTime(
                    DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day,
                    SelectedHour, SelectedMinute, 0);

                if (SelectedDateMode == DateMode.Tomorrow)
                    targetTime = targetTime.AddDays(1);

                _shutdownService.ScheduleFixedTime(targetTime);
            }
            else
            {
                _shutdownService.ScheduleCountdown(_countdownMinutes * 60);
            }

            UpdateActiveState();
        }

        public void CancelShutdown()
        {
            _shutdownService.CancelShutdown();
            UpdateActiveState();
        }

        public void DelayShutdown(int delayMinutes)
        {
            _shutdownService.CancelShutdown();
            _shutdownService.ScheduleCountdown(delayMinutes * 60);
            UpdateActiveState();
        }

        public void UpdateActiveState()
        {
            IsShutdownActive = _shutdownService.IsShutdownScheduled;
            if (IsShutdownActive)
            {
                var remaining = _shutdownService.GetRemainingSeconds();
                var formatted = Utils.TimeHelper.FormatRemainingTime(remaining);
                var execTime = DateTime.Now.AddSeconds(remaining);
                ActiveShutdownInfo = LocalizationManager.LF("St.ShutdownSetFmt",
                    execTime.ToString("HH:mm"), formatted);
            }
            else
            {
                ActiveShutdownInfo = LocalizationManager.L("St.NotSet");
            }
        }
    }
}
