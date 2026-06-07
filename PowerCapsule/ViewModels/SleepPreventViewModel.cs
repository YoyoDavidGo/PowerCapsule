using System;
using PowerCapsule.Models;
using PowerCapsule.Services;

namespace PowerCapsule.ViewModels
{
    public class SleepPreventViewModel : ViewModelBase
    {
        private readonly SleepPreventService _sleepPreventService;

        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        private bool _keepDisplayOn;
        public bool KeepDisplayOn
        {
            get => _keepDisplayOn;
            set => SetProperty(ref _keepDisplayOn, value);
        }

        private DurationMode _selectedDuration = DurationMode.Always;
        public DurationMode SelectedDuration
        {
            get => _selectedDuration;
            set
            {
                SetProperty(ref _selectedDuration, value);
                OnPropertyChanged(nameof(IsCustomDuration));
            }
        }

        public bool IsCustomDuration => SelectedDuration == DurationMode.Custom;

        private int _customMinutes = 30;
        public int CustomMinutes
        {
            get => _customMinutes;
            set => SetProperty(ref _customMinutes, value);
        }

        private string _statusText;
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public SleepPreventViewModel(SleepPreventService sleepPreventService)
        {
            _sleepPreventService = sleepPreventService;

            var config = App.Current.Properties["Config"] as Models.AppConfig;
            if (config?.PreventSleep != null)
            {
                IsEnabled = config.PreventSleep.Enabled;
                KeepDisplayOn = config.PreventSleep.KeepDisplayOn;
                SelectedDuration = Enum.TryParse(config.PreventSleep.DurationMode, out DurationMode mode)
                    ? mode : DurationMode.Always;
                CustomMinutes = config.PreventSleep.CustomMinutes > 0 ? config.PreventSleep.CustomMinutes : 30;
            }

            _sleepPreventService.StateChanged += UpdateStatus;
            UpdateStatus();
        }

        public void Apply()
        {
            if (IsEnabled)
            {
                _sleepPreventService.Start(KeepDisplayOn, SelectedDuration, CustomMinutes);
                StatusText = LocalizationManager.L("Sl.OnMsg");
            }
            else
            {
                _sleepPreventService.Stop();
                StatusText = LocalizationManager.L("Sl.OffMsg");
            }
        }

        public void UpdateStatus()
        {
            IsEnabled = _sleepPreventService.IsActive;
            KeepDisplayOn = _sleepPreventService.IsKeepDisplayOn;

            if (_sleepPreventService.IsActive)
            {
                var remaining = _sleepPreventService.GetRemainingMinutes();
                StatusText = remaining > 0
                    ? LocalizationManager.LF("St.AwakeFmt", Utils.TimeHelper.FormatRemainingTime(remaining * 60))
                    : LocalizationManager.L("Cap.PreventSleep");
            }
            else
            {
                StatusText = LocalizationManager.L("St.NotOn");
            }
        }
    }
}
