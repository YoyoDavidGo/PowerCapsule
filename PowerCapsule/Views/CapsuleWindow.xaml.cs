using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using PowerCapsule.Services;
using PowerCapsule.ViewModels;

namespace PowerCapsule.Views
{
    public partial class CapsuleWindow : Window
    {
        private readonly ConfigService _configService;
        private readonly ShutdownService _shutdownService;
        private readonly SleepPreventService _sleepPreventService;
        private readonly WakeTaskService _wakeTaskService;
        private readonly SleepService _sleepService;
        private readonly TrayService _trayService;
        private readonly StartupService _startupService;

        private CapsuleViewModel _viewModel;
        private Models.AppConfig _config;

        private SettingsView _settingsWindow;

        // 胶囊边框：活动(绿) / 空闲(暗)
        private readonly System.Windows.Media.SolidColorBrush _activeBorderBrush =
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2E, 0x9E, 0x4E));
        private readonly System.Windows.Media.SolidColorBrush _idleBorderBrush =
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x35, 0x35, 0x50));
        private readonly System.Windows.Media.SolidColorBrush _edgeIdleBrush =
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3A, 0x3A, 0x52));

        // 设置面板（无边框 Window，替代原 Popup）
        private DropPanel DropPanelControl;
        private Window _panelWindow;

        private bool _isMouseDown;
        private bool _isDragging;
        private Point _dragStartPoint;
        private const double DragThreshold = 5.0;

        private DispatcherTimer _edgeTimer;
        private DispatcherTimer _hideTimer;
        private bool _isEdgeSnapped;
        private bool _isHidden;
        private bool _isLeftEdge;
        private double _originalLeft;
        private int _slideGeneration;

        public CapsuleWindow()
        {
            InitializeComponent();

            // 为 AllowsTransparency 窗口修复鼠标激活问题
            SourceInitialized += (s, e) =>
            {
                var source = (System.Windows.Interop.HwndSource)PresentationSource.FromVisual(this);
                source.AddHook(WndProc);
            };

            _configService = new ConfigService();
            _shutdownService = new ShutdownService();
            _sleepPreventService = new SleepPreventService();
            _wakeTaskService = new WakeTaskService();
            _sleepService = new SleepService();
            _startupService = new StartupService();

            _config = _configService.Load();

            App.Current.Properties["Config"] = _config;

            // 按配置应用语言（中/英）
            LocalizationManager.Initialize(_config.Language == "en");

            _trayService = new TrayService();
            SetupTray();

            _viewModel = new CapsuleViewModel(_shutdownService, _sleepPreventService, _wakeTaskService, _sleepService);
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            DataContext = _viewModel;

            DropPanelControl = new DropPanel();
            DropPanelControl.Initialize(
                _configService, _shutdownService, _sleepPreventService,
                _wakeTaskService, _sleepService, _startupService, _config);

            DropPanelControl.PanelClosed += () => ClosePanel();

            DropPanelControl.StateApplied += () =>
            {
                _viewModel.UpdateDisplay();
                UpdateTrayTooltip();
            };

            _edgeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _edgeTimer.Tick += EdgeTimer_Tick;

            _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _hideTimer.Tick += HideTimer_Tick;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var screen = SystemParameters.WorkArea;
            // 默认位置：屏幕右侧 1/4 处水平居中、距顶部 60px。距屏幕边缘 > 10px，避免被 edge-snap 误判
            double defaultX = screen.Left + screen.Width * 0.75 - Width / 2;
            double defaultY = screen.Top + 60;

            double x = _config.CapsulePosition.X;
            double y = _config.CapsulePosition.Y;

            // 校验保存值是否在可见屏幕内；不合法则回退到默认位置
            bool valid = x >= screen.Left && y >= screen.Top
                         && x + Width <= screen.Right && y + Height <= screen.Bottom;
            Left = valid ? x : defaultX;
            Top = valid ? y : defaultY;

            Opacity = _config.Opacity;

            _viewModel.UpdateDisplay();

            if (_config.ShowCapsuleOnStartup)
                Show();
            else
                Hide();

            Utils.NativeMethods.SetWindowPos(
                new System.Windows.Interop.WindowInteropHelper(this).Handle,
                Utils.NativeMethods.HWND_TOPMOST,
                0, 0, 0, 0,
                Utils.NativeMethods.SWP_NOMOVE | Utils.NativeMethods.SWP_NOSIZE |
                Utils.NativeMethods.SWP_NOACTIVATE | Utils.NativeMethods.SWP_SHOWWINDOW);
        }

        private void SetupTray()
        {
            _trayService.ShowCapsuleRequested += () =>
            {
                Dispatcher.Invoke(() => { Show(); WindowState = WindowState.Normal; });
            };

            _trayService.HideCapsuleRequested += () =>
            {
                Dispatcher.Invoke(() => { ClosePanel(); Hide(); });
            };

            _trayService.TogglePreventSleepRequested += () =>
            {
                if (_sleepPreventService.IsActive)
                    _sleepPreventService.Stop();
                else
                    _sleepPreventService.Start(false, Models.DurationMode.Always, 0);
                _viewModel.UpdateDisplay();
            };

            _trayService.CancelShutdownRequested += () =>
            {
                _shutdownService.CancelShutdown();
                _viewModel.UpdateDisplay();
            };

            _trayService.CancelWakeRequested += () =>
            {
                _wakeTaskService.DeleteWakeTask();
                _viewModel.UpdateDisplay();
            };

            _trayService.CancelStandbyRequested += () =>
            {
                _sleepService.Cancel();
                _viewModel.UpdateDisplay();
            };

            _trayService.OpenSettingsRequested += () =>
            {
                Dispatcher.Invoke(() => OpenSettings());
            };

            _trayService.ExitRequested += () =>
            {
                HandleExit();
            };

            _trayService.LanguageRequested += () =>
            {
                LocalizationManager.Toggle();
                _config.Language = LocalizationManager.IsEnglish ? "en" : "zh";
                _configService.Save(_config);
                _viewModel.UpdateDisplay();
            };

            _trayService.Initialize();
        }

        private void UpdateTrayTooltip()
        {
            if (_trayService != null)
                _trayService.UpdateTooltip($"PowerCapsule - {_viewModel.MainText}");
        }

        private void CapsuleBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            BeginMouseDown(e);
        }

        private void ArrowButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            TogglePanel();
            e.Handled = true;
        }

        private void EdgeBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            BeginMouseDown(e);
            e.Handled = true;
        }

        private void BeginMouseDown(MouseButtonEventArgs e)
        {
            CancelSlideAnimation();
            _isMouseDown = true;
            _isDragging = false;
            _dragStartPoint = e.GetPosition(this);
            CapsuleBorder.CaptureMouse();
        }

        private void CancelSlideAnimation()
        {
            if (!_isEdgeSnapped) return;

            ++_slideGeneration;
            BeginAnimation(Window.LeftProperty, null);
            Left = _originalLeft;
            _isHidden = false;
            EdgeBar.Visibility = Visibility.Collapsed;
        }

        private bool IsPanelVisible => _panelWindow != null && _panelWindow.IsVisible;

        private void EnsurePanelWindow()
        {
            if (_panelWindow != null) return;

            _panelWindow = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                ShowInTaskbar = false,
                ResizeMode = ResizeMode.NoResize,
                SizeToContent = SizeToContent.WidthAndHeight,
                Topmost = true,
                ShowActivated = true,
                Owner = this,
                Content = DropPanelControl
            };
        }

        // 把面板窗口定位到胶囊上方（空间不足才放下方），水平相对胶囊居中
        private void PositionPanelWindow()
        {
            double panelW = DropPanelControl.Width;
            double panelH = DropPanelControl.Height;
            if (double.IsNaN(panelH) || panelH <= 0)
            {
                DropPanelControl.Measure(new Size(panelW, double.PositiveInfinity));
                panelH = DropPanelControl.DesiredSize.Height;
            }
            double capLeft = _isEdgeSnapped ? _originalLeft : Left;

            var wa = SystemParameters.WorkArea;
            double left = capLeft + (Width - panelW) / 2;
            left = Math.Max(wa.Left, Math.Min(left, wa.Right - panelW));

            // 窗口四周有 9px 透明发光边，按胶囊“视觉边缘”定位面板
            const double capMargin = 9;
            double above = (Top + capMargin) - panelH;
            double below = Top + Height - capMargin;
            double top = (above >= wa.Top) ? above : below;
            top = Math.Max(wa.Top, Math.Min(top, wa.Bottom - panelH));

            _panelWindow.Left = left;
            _panelWindow.Top = top;
        }

        private void RepositionPanel()
        {
            if (IsPanelVisible)
                PositionPanelWindow();
        }

        private void ShowPanelWindow()
        {
            PositionPanelWindow();
            _panelWindow.Show();
            _panelWindow.Activate();
        }

        private void TogglePanel()
        {
            if (IsPanelVisible)
            {
                ClosePanel();
                return;
            }

            // 立即同步标记面板已打开，阻止 MouseLeave / HideTimer 把胶囊滑回边缘
            _viewModel.IsPanelOpen = true;
            _viewModel.ArrowText = "▲";
            _hideTimer.Stop();
            _edgeTimer.Stop();

            DropPanelControl.RefreshStatus();
            EnsurePanelWindow();

            if (_isHidden)
                SlideOut(() => ShowPanelWindow());
            else
                ShowPanelWindow();
        }

        private void ClosePanel()
        {
            if (IsPanelVisible)
                _panelWindow.Hide();

            _viewModel.IsPanelOpen = false;
            _viewModel.ArrowText = "▼";

            // 面板关闭后若仍在边缘吸附且鼠标不在胶囊上，稍后滑回隐藏
            if (_isEdgeSnapped && !_isHidden && !CapsuleBorder.IsMouseOver)
                _hideTimer.Start();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (!_isMouseDown || e.LeftButton != MouseButtonState.Pressed) return;

            var pos = e.GetPosition(this);
            var diff = pos - _dragStartPoint;

            if (!_isDragging)
            {
                if (Math.Abs(diff.X) < DragThreshold && Math.Abs(diff.Y) < DragThreshold)
                    return;
                _isDragging = true;
            }

            Left += diff.X;
            Top += diff.Y;

            var screen = SystemParameters.WorkArea;
            Left = Math.Max(screen.Left - Width + 30, Math.Min(Left, screen.Right - 30));
            Top = Math.Max(screen.Top, Math.Min(Top, screen.Bottom - Height));

            RepositionPanel();
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);

            if (!_isMouseDown) return;

            bool wasDragging = _isDragging;
            _isMouseDown = false;
            _isDragging = false;
            CapsuleBorder.ReleaseMouseCapture();

            if (wasDragging)
            {
                SavePosition();
                _edgeTimer.Start();
            }
            else
            {
                // 简单点击 — 不属于拖动，触发打开面板
                TogglePanel();
            }
        }

        private void EdgeTimer_Tick(object sender, EventArgs e)
        {
            _edgeTimer.Stop();
            EvaluateEdgeSnap();
        }

        // 供面板拖动结束后调用：判断胶囊是否越界 1/3 并吸附收起
        public void EvaluateEdgeSnap()
        {
            if (!_config.AutoCollapse) return;

            var screen = SystemParameters.WorkArea;
            // 胶囊越过屏幕边缘达到自身 1/3 宽度后才吸附收起
            double third = Width / 3.0;

            bool snapLeft = Left <= screen.Left - third;
            bool snapRight = Left + Width >= screen.Right + third;

            if (snapLeft || snapRight)
            {
                // 主界面若展开，先收回面板，再整体吸附收起
                if (IsPanelVisible) ClosePanel();

                if (snapLeft)
                    SnapToEdge(screen.Left, isLeft: true);
                else
                    SnapToEdge(screen.Right - Width, isLeft: false);

                // 吸附后立即持久化屏内坐标（_originalLeft），避免进程被强杀时配置残留屏外无效坐标
                SavePosition();
            }
            else
            {
                // 未达侵入阈值 — 清除残留吸附状态，避免之后 MouseLeave 误把胶囊滑回旧边缘
                _isEdgeSnapped = false;
                _isHidden = false;
                EdgeBar.Visibility = Visibility.Collapsed;
            }
        }

        private void SnapToEdge(double targetX, bool isLeft)
        {
            ++_slideGeneration;
            BeginAnimation(Window.LeftProperty, null);

            _isEdgeSnapped = true;
            _isHidden = true;
            _isLeftEdge = isLeft;
            _originalLeft = targetX;

            EdgeBar.HorizontalAlignment = isLeft ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            EdgeBar.Visibility = Visibility.Visible;
            // CapsuleBorder 始终保持 Visible，仅被 EdgeBar 蒙层覆盖

            if (isLeft)
                Left = -Width + 5;
            else
                Left = SystemParameters.WorkArea.Right - 5;
        }

        private void CapsuleBorder_MouseEnter(object sender, MouseEventArgs e)
        {
            OnMouseEnterEdgeArea();
        }

        private void EdgeBar_MouseEnter(object sender, MouseEventArgs e)
        {
            OnMouseEnterEdgeArea();
        }

        private void OnMouseEnterEdgeArea()
        {
            _hideTimer.Stop();
            if (_isEdgeSnapped && _isHidden)
                SlideOut();
        }

        private void CapsuleBorder_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_isEdgeSnapped && !_isHidden && !_viewModel.IsPanelOpen)
                _hideTimer.Start();
        }

        private void HideTimer_Tick(object sender, EventArgs e)
        {
            _hideTimer.Stop();
            if (_isEdgeSnapped && !_viewModel.IsPanelOpen)
                SlideIn();
        }

        private void SlideOut(Action onCompleted = null)
        {
            _isHidden = false;
            EdgeBar.Visibility = Visibility.Collapsed;
            // CapsuleBorder 始终保持 Visible，无需切换

            int gen = ++_slideGeneration;
            var animation = new System.Windows.Media.Animation.DoubleAnimation(
                _originalLeft,
                new Duration(TimeSpan.FromMilliseconds(200)));
            animation.Completed += (s, e) =>
            {
                if (gen != _slideGeneration) return;
                RepositionPanel();
                onCompleted?.Invoke();
            };
            BeginAnimation(Window.LeftProperty, animation);
        }

        private void SlideIn()
        {
            _isHidden = true;

            int gen = ++_slideGeneration;
            var targetX = _isLeftEdge
                ? -Width + 5
                : SystemParameters.WorkArea.Right - 5;

            var animation = new System.Windows.Media.Animation.DoubleAnimation(
                targetX,
                new Duration(TimeSpan.FromMilliseconds(200)));
            animation.Completed += (s, e) =>
            {
                if (gen != _slideGeneration) return;
                EdgeBar.Visibility = Visibility.Visible;
                // CapsuleBorder 保持 Visible，仅被 EdgeBar 覆盖
            };
            BeginAnimation(Window.LeftProperty, animation);
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CapsuleViewModel.MainText) ||
                e.PropertyName == nameof(CapsuleViewModel.SubText))
            {
                StatusText.Text = _viewModel.DisplayMode == Models.CapsuleDisplayMode.Idle
                    ? $"{_viewModel.MainText}  ·  {_viewModel.SubText}"
                    : _viewModel.MainText;
            }
            else if (e.PropertyName == nameof(CapsuleViewModel.ShowGreenDot))
            {
                // 有任务 → 整圈绿色发光；空闲 → 普通暗阴影
                if (_viewModel.ShowGreenDot)
                {
                    // 有任务：清晰绿色描边 + 绿色竖条
                    CapsuleBorder.BorderBrush = _activeBorderBrush;
                    CapsuleBorder.BorderThickness = new Thickness(2);
                    EdgeBar.Background = _activeBorderBrush;
                }
                else
                {
                    CapsuleBorder.BorderBrush = _idleBorderBrush;
                    CapsuleBorder.BorderThickness = new Thickness(1);
                    EdgeBar.Background = _edgeIdleBrush;
                }
            }
            else if (e.PropertyName == nameof(CapsuleViewModel.ArrowText))
            {
                ArrowText.Text = _viewModel.ArrowText;
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel = true;
            ClosePanel();
            Hide();
        }

        private void OpenSettings()
        {
            if (_settingsWindow != null && _settingsWindow.IsVisible)
            {
                _settingsWindow.Activate();
                return;
            }

            _settingsWindow = new SettingsView(_configService, _startupService, _config);
            _settingsWindow.OpacityChanged += (opacity) =>
            {
                Opacity = opacity;
            };
            _settingsWindow.Closed += (s, e) => _settingsWindow = null;
            _settingsWindow.Owner = this;
            _settingsWindow.Show();
        }

        private void HandleExit()
        {
            if (_shutdownService.IsShutdownScheduled)
            {
                var result = MessageBox.Show(
                    "当前存在定时关机任务，退出程序后任务仍可能继续执行。\n是否取消关机任务？",
                    "PowerCapsule - 确认退出",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                    _shutdownService.CancelShutdown();
                else if (result == MessageBoxResult.Cancel)
                    return;
            }

            SavePosition();
            _configService.Save(_config);

            _sleepPreventService.Dispose();
            _trayService.Dispose();
            _viewModel.Dispose();

            Application.Current.Shutdown();
        }

        // 供设置面板拖动后调用：保存胶囊新位置
        public void SaveCurrentPosition() => SavePosition();

        private void SavePosition()
        {
            // 吸附状态下 Left 是屏幕外的位置（-Width+5 或屏右-5），直接保存会导致下次启动胶囊漂在屏幕外。
            // 保存吸附前的展开位置（_originalLeft）。
            _config.CapsulePosition.X = _isEdgeSnapped ? _originalLeft : Left;
            _config.CapsulePosition.Y = Top;
            _configService.Save(_config);
        }

        /// <summary>
        /// 修复 AllowsTransparency 窗口的鼠标激活问题。
        /// 透明分层窗口默认不响应 MA_ACTIVATE，导致控件点击迟钝、ComboBox 下拉难展开。
        /// </summary>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_MOUSEACTIVATE = 0x0021;
            if (msg == WM_MOUSEACTIVATE)
            {
                handled = true;
                return (IntPtr)1; // MA_ACTIVATE — 强制激活
            }
            return IntPtr.Zero;
        }
    }
}
