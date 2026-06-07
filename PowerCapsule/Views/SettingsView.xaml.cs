using System;
using System.Windows;
using System.Windows.Controls;
using PowerCapsule.Models;
using PowerCapsule.Services;

namespace PowerCapsule.Views
{
    public partial class SettingsView : Window
    {
        private readonly ConfigService _configService;
        private readonly StartupService _startupService;
        private readonly AppConfig _config;

        public event Action<double> OpacityChanged;

        public SettingsView(ConfigService configService, StartupService startupService, AppConfig config)
        {
            InitializeComponent();

            _configService = configService;
            _startupService = startupService;
            _config = config;

            StartupToggle.IsChecked = _startupService.IsStartupEnabled();
            ShowCapsuleToggle.IsChecked = _config.ShowCapsuleOnStartup;
            AutoCollapseToggle.IsChecked = _config.AutoCollapse;

            UpdateOpacityButtons(_config.Opacity);

            StartupToggle.Checked += (s, e) => _startupService.SetStartup(true);
            StartupToggle.Unchecked += (s, e) => _startupService.SetStartup(false);

            ShowCapsuleToggle.Checked += (s, e) => SaveSetting(() => _config.ShowCapsuleOnStartup = true);
            ShowCapsuleToggle.Unchecked += (s, e) => SaveSetting(() => _config.ShowCapsuleOnStartup = false);

            AutoCollapseToggle.Checked += (s, e) => SaveSetting(() => _config.AutoCollapse = true);
            AutoCollapseToggle.Unchecked += (s, e) => SaveSetting(() => _config.AutoCollapse = false);
        }

        private void OpacityBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag && double.TryParse(tag, out double opacity))
            {
                _config.Opacity = opacity;
                _configService.Save(_config);
                UpdateOpacityButtons(opacity);
                OpacityChanged?.Invoke(opacity);
            }
        }

        private void UpdateOpacityButtons(double selected)
        {
            SetOpacityBtn(Opacity80Btn, selected == 0.8);
            SetOpacityBtn(Opacity90Btn, selected == 0.9);
            SetOpacityBtn(Opacity100Btn, selected == 1.0);
        }

        private void SetOpacityBtn(Button btn, bool active)
        {
            if (active)
            {
                btn.Background = FindResource("AccentBlueBrush") as System.Windows.Media.Brush;
                btn.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x1C, 0x1C, 0x24));
            }
            else
            {
                // 恢复样式默认（深底浅字），不要设成透明导致看不清
                btn.ClearValue(BackgroundProperty);
                btn.ClearValue(ForegroundProperty);
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                DragMove();
        }

        private void SaveSetting(Action setter)
        {
            setter();
            _configService.Save(_config);
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
