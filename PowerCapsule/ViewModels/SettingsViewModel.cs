using PowerCapsule.Models;
using PowerCapsule.Services;

namespace PowerCapsule.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly ConfigService _configService;
        private readonly StartupService _startupService;
        private AppConfig _config;

        private bool _startWithWindows;
        public bool StartWithWindows
        {
            get => _startWithWindows;
            set
            {
                SetProperty(ref _startWithWindows, value);
                _startupService.SetStartup(value);
            }
        }

        private bool _showCapsuleOnStartup;
        public bool ShowCapsuleOnStartup
        {
            get => _showCapsuleOnStartup;
            set
            {
                SetProperty(ref _showCapsuleOnStartup, value);
                _config.ShowCapsuleOnStartup = value;
                _configService.Save(_config);
            }
        }

        private bool _autoCollapse;
        public bool AutoCollapse
        {
            get => _autoCollapse;
            set
            {
                SetProperty(ref _autoCollapse, value);
                _config.AutoCollapse = value;
                _configService.Save(_config);
            }
        }

        private double _opacity;
        public double Opacity
        {
            get => _opacity;
            set
            {
                SetProperty(ref _opacity, value);
                _config.Opacity = value;
                _configService.Save(_config);
            }
        }

        private int _shutdownReminderSeconds;
        public int ShutdownReminderSeconds
        {
            get => _shutdownReminderSeconds;
            set
            {
                SetProperty(ref _shutdownReminderSeconds, value);
                _config.ShutdownReminderSeconds = value;
                _configService.Save(_config);
            }
        }

        public SettingsViewModel(ConfigService configService, StartupService startupService)
        {
            _configService = configService;
            _startupService = startupService;
            _config = configService.Load();

            _startWithWindows = startupService.IsStartupEnabled();
            _showCapsuleOnStartup = _config.ShowCapsuleOnStartup;
            _autoCollapse = _config.AutoCollapse;
            _opacity = _config.Opacity;
            _shutdownReminderSeconds = _config.ShutdownReminderSeconds;
        }
    }
}
