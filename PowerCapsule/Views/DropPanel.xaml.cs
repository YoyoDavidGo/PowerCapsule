using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PowerCapsule.Models;
using PowerCapsule.Services;
using PowerCapsule.ViewModels;

namespace PowerCapsule.Views
{
    public partial class DropPanel : UserControl
    {
        private ConfigService _configService;
        private ShutdownService _shutdownService;
        private SleepPreventService _sleepPreventService;
        private WakeTaskService _wakeTaskService;
        private SleepService _sleepService;
        private StartupService _startupService;
        private AppConfig _config;

        private ShutdownViewModel _shutdownVM;
        private SleepPreventViewModel _sleepVM;
        private WakeViewModel _wakeVM;

        // 防睡眠持续时间
        private DurationMode _currentDurationMode = DurationMode.Always;

        // 定时 ↔ 倒计时 联动
        private bool _suppressLink;          // 程序化改写控件时置位，避免联动递归
        private DateTime? _previewTarget;    // 当前预览的关机目标时刻
        private DispatcherTimer _linkTimer;  // 每秒刷新倒计时显示

        // 自定义日期
        private DateTime? _shutdownCustomDate;
        private DateTime? _wakeCustomDate;
        private DateTime? _sleepCustomDate;
        private int _sleepCustomMinutes = 1440;
        private string _prevShutdownDateTag = "Today";
        private string _prevWakeDateTag = "Tomorrow";
        private DurationMode _prevDurationMode = DurationMode.Always;

        public event Action PanelClosed;
        public event Action StateApplied;

        public DropPanel()
        {
            InitializeComponent();

            // 初始化时间下拉时不触发联动（避免默认就把倒计时填满）
            _suppressLink = true;
            InitializeTimeCombos();
            _suppressLink = false;

            _linkTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _linkTimer.Tick += LinkTimer_Tick;
            _linkTimer.Start();
        }

        public void Initialize(
            ConfigService configService,
            ShutdownService shutdownService,
            SleepPreventService sleepPreventService,
            WakeTaskService wakeTaskService,
            SleepService sleepService,
            StartupService startupService,
            AppConfig config)
        {
            _configService = configService;
            _shutdownService = shutdownService;
            _sleepPreventService = sleepPreventService;
            _wakeTaskService = wakeTaskService;
            _sleepService = sleepService;
            _startupService = startupService;
            _config = config;

            _shutdownVM = new ShutdownViewModel(_shutdownService);
            _sleepVM = new SleepPreventViewModel(_sleepPreventService);
            _wakeVM = new WakeViewModel(_wakeTaskService);

            // 从配置同步 ReminderCombo 初始值
            foreach (ComboBoxItem item in ReminderCombo.Items)
            {
                if (item.Tag is string tag && int.TryParse(tag, out int sec)
                    && sec == _config.ShutdownReminderSeconds)
                {
                    item.IsSelected = true;
                    break;
                }
            }

            // 从配置同步 DurationMode
            _currentDurationMode = _sleepVM.SelectedDuration;

            // 恢复上次 Tab
            RestoreLastTab();
            BindViewModels();
            RefreshStatus();

            SyncDurationComboSelection(_sleepVM.SelectedDuration);
            UpdateReminderOptions();

            // 启动时按默认定时（如 23:30）立即联动出倒计时，并随时间实时更新
            RelinkShutdownFromTime();

            LocalizationManager.LanguageChanged += OnLanguageChanged;
        }

        private void OnLanguageChanged()
        {
            if (_shutdownVM == null) return;
            RefreshStatus();
        }

        private void SyncDurationComboSelection(DurationMode mode)
        {
            bool prev = _suppressLink;
            _suppressLink = true;
            var tag = mode.ToString();
            foreach (ComboBoxItem item in DurationCombo.Items)
            {
                if (item.Tag is string t && t == tag)
                {
                    item.IsSelected = true;
                    break;
                }
            }
            _suppressLink = prev;
            _currentDurationMode = mode;
            _prevDurationMode = mode;
        }

        private void InitializeTimeCombos()
        {
            // 小时 0-23
            for (int h = 0; h < 24; h++)
            {
                HourCombo.Items.Add(new ComboBoxItem { Content = h.ToString("D2"), Tag = h });
                WakeHourCombo.Items.Add(new ComboBoxItem { Content = h.ToString("D2"), Tag = h });
            }
            HourCombo.SelectedIndex = 23;
            WakeHourCombo.SelectedIndex = 8;

            // 分钟 0-59，步长 1（联动可精确显示任意分钟）
            for (int m = 0; m < 60; m++)
            {
                MinuteCombo.Items.Add(new ComboBoxItem { Content = m.ToString("D2"), Tag = m });
                WakeMinuteCombo.Items.Add(new ComboBoxItem { Content = m.ToString("D2"), Tag = m });
            }
            MinuteCombo.SelectedIndex = 30;
            WakeMinuteCombo.SelectedIndex = 0;
        }

        private void RestoreLastTab()
        {
            switch (_config.LastTab)
            {
                case "PreventSleep":
                    MainTabControl.SelectedIndex = 1;
                    break;
                case "Wake":
                    MainTabControl.SelectedIndex = 2;
                    break;
                default:
                    MainTabControl.SelectedIndex = 0;
                    break;
            }
        }

        private void BindViewModels()
        {
            // 关机相关绑定
            _shutdownVM.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ShutdownViewModel.IsShutdownActive) ||
                    e.PropertyName == nameof(ShutdownViewModel.ActiveShutdownInfo))
                {
                    ShutdownStatusText.Text = _shutdownVM.ActiveShutdownInfo;
                }
            };
            ShutdownStatusText.Text = _shutdownVM.ActiveShutdownInfo;

            // 防睡眠相关绑定
            _sleepVM.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SleepPreventViewModel.StatusText))
                {
                    SleepStatusText.Text = _sleepVM.StatusText;
                }
                if (e.PropertyName == nameof(SleepPreventViewModel.IsEnabled))
                {
                    PreventSleepToggle.IsChecked = _sleepVM.IsEnabled;
                }
                if (e.PropertyName == nameof(SleepPreventViewModel.KeepDisplayOn))
                {
                    KeepDisplayToggle.IsChecked = _sleepVM.KeepDisplayOn;
                }
            };
            PreventSleepToggle.IsChecked = _sleepVM.IsEnabled;
            KeepDisplayToggle.IsChecked = _sleepVM.KeepDisplayOn;
            SleepStatusText.Text = _sleepVM.StatusText;

            if (_sleepVM.SelectedDuration == DurationMode.Custom)
            {
                _sleepCustomMinutes = Math.Max(1, _sleepVM.CustomMinutes);
                int days = Math.Max(1, _sleepCustomMinutes / 1440);
                _sleepCustomDate = DateTime.Today.AddDays(days);
                if (CustomDateLabel != null)
                    CustomDateLabel.Text = $"保持至 {_sleepCustomDate:MM-dd}（约 {days} 天）";
                CustomMinutesPanel.Visibility = Visibility.Visible;
            }

            // 唤醒相关绑定
            _wakeVM.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(WakeViewModel.StatusText))
                {
                    WakeStatusText.Text = _wakeVM.StatusText;
                }
            };
            WakeStatusText.Text = _wakeVM.StatusText;
        }

        public void RefreshStatus()
        {
            _shutdownVM.UpdateActiveState();
            _sleepVM.UpdateStatus();
            _wakeVM.UpdateStatus();
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            var parts = new List<string>();

            // 当前状态
            if (_sleepPreventService.IsActive)
            {
                parts.Add(LocalizationManager.L(_sleepPreventService.IsKeepDisplayOn ? "Cap.KeepAwake" : "Cap.PreventSleep"));
            }
            else if (_shutdownService.IsShutdownScheduled)
            {
                parts.Add(LocalizationManager.L("Sum.WaitShutdown"));
            }
            else
            {
                parts.Add(LocalizationManager.L("Cap.Idle"));
            }

            // 下个任务
            if (_shutdownService.IsShutdownScheduled)
            {
                var sec = _shutdownService.GetRemainingSeconds();
                var execTime = DateTime.Now.AddSeconds(sec);
                parts.Add(LocalizationManager.LF("Sum.OffAtFmt", execTime.ToString("HH:mm")));
                parts.Add(Utils.TimeHelper.FormatRemainingTime(sec));
            }
            else if (_wakeTaskService.IsWakeScheduled)
            {
                parts.Add(LocalizationManager.L("Sum.WakePending"));
            }

            SummaryCurrentState.Text = parts.Count > 0 ? parts[0] : LocalizationManager.L("Cap.Idle");
            SummaryNextTask.Text = parts.Count > 1 ? $"  ·  {parts[1]}" : "";
            SummaryRemaining.Text = parts.Count > 2 ? $"  ·  {parts[2]}" : "";
        }

        // 面板空白处（非交互热区）按下左键 → 拖动整个面板窗口
        private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DatePickerOverlay != null && DatePickerOverlay.Visibility == Visibility.Visible) return;
            if (IsInteractive(e.OriginalSource as DependencyObject)) return;

            var panelWin = Window.GetWindow(this);
            if (panelWin == null) return;
            var capsule = panelWin.Owner;

            if (capsule != null)
            {
                // 拖面板时让胶囊同步平移（整体移动）
                double startPanelLeft = panelWin.Left, startPanelTop = panelWin.Top;
                double startCapLeft = capsule.Left, startCapTop = capsule.Top;
                EventHandler handler = (s, ev) =>
                {
                    capsule.Left = startCapLeft + (panelWin.Left - startPanelLeft);
                    capsule.Top = startCapTop + (panelWin.Top - startPanelTop);
                };
                panelWin.LocationChanged += handler;
                try { panelWin.DragMove(); } catch { }
                panelWin.LocationChanged -= handler;
                // 先判断越界 1/3 → 吸附收起（会先关闭面板并把 _originalLeft 置为屏内坐标）
                (capsule as CapsuleWindow)?.EvaluateEdgeSnap();
                // 再保存：吸附时保存 _originalLeft（屏内），未吸附时保存当前位置
                (capsule as CapsuleWindow)?.SaveCurrentPosition();
            }
            else
            {
                try { panelWin.DragMove(); } catch { }
            }
        }

        // 判断点击源是否落在交互控件上（按钮 / 下拉 / 输入框 / 日历 / 标签）
        private bool IsInteractive(DependencyObject d)
        {
            while (d != null && !(d is DropPanel))
            {
                if (d is ButtonBase || d is ComboBox || d is TextBoxBase ||
                    d is Calendar || d is TabItem)
                    return true;
                d = VisualTreeHelper.GetParent(d) ?? LogicalTreeHelper.GetParent(d);
            }
            return false;
        }

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_config == null || _configService == null) return;

            switch (MainTabControl.SelectedIndex)
            {
                case 1: _config.LastTab = "PreventSleep"; break;
                case 2: _config.LastTab = "Wake"; break;
                default: _config.LastTab = "Shutdown"; break;
            }
            _configService.Save(_config);
        }

        #region 定时关机 Tab 事件

        // 选择快捷时长 → 填入倒计时框（再由联动同步到定时）
        private void CountdownPreset_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!(CountdownPresetCombo.SelectedItem is ComboBoxItem item)) return;
            if (!(item.Tag is string t) || !int.TryParse(t, out int mins) || mins <= 0) return;
            CountdownMinutesBox.Text = mins.ToString();
        }

        // 用户改倒计时分钟 → 实时反推定时（时:分 + 今天/明天）
        private void CountdownMinutesBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_suppressLink)
            {
                if (int.TryParse(CountdownMinutesBox.Text, out int mins) && mins > 0)
                {
                    _suppressLink = true;
                    try
                    {
                        _previewTarget = DateTime.Now.AddMinutes(mins);
                        SetTimeFromTarget(_previewTarget.Value);
                    }
                    finally { _suppressLink = false; }
                }
                else
                {
                    _previewTarget = null;
                }
            }
            UpdateReminderOptions();
        }

        // 每秒刷新：倒计时随时间流逝递减（定时时刻固定不动）
        private void LinkTimer_Tick(object sender, EventArgs e)
        {
            if (_previewTarget == null) return;
            if (CountdownMinutesBox.IsKeyboardFocused) return; // 用户正在输入时不打扰

            var remain = _previewTarget.Value - DateTime.Now;
            if (remain.TotalSeconds <= 0) { _previewTarget = null; return; }

            string s = ((int)Math.Ceiling(remain.TotalMinutes)).ToString();
            if (CountdownMinutesBox.Text != s)
            {
                _suppressLink = true;
                CountdownMinutesBox.Text = s;
                _suppressLink = false;
                UpdateReminderOptions();
            }
        }

        // 根据目标时刻回填 时:分 + 日期选项（精确到分钟）
        private void SetTimeFromTarget(DateTime target)
        {
            SelectComboByIntTag(HourCombo, target.Hour);
            SelectComboByIntTag(MinuteCombo, target.Minute);

            int dayOffset = (target.Date - DateTime.Today).Days;
            string tag;
            if (dayOffset == 0) tag = "Today";
            else if (dayOffset == 1) tag = "Tomorrow";
            else if (dayOffset == 2) tag = "DayAfter";
            else { tag = "Custom"; _shutdownCustomDate = target.Date; }

            foreach (ComboBoxItem item in DateModeCombo.Items)
            {
                if (item.Tag?.ToString() == tag)
                {
                    if (tag == "Custom") item.Content = $"自定义 {target:MM-dd}";
                    item.IsSelected = true;
                    break;
                }
            }
        }

        private void SelectComboByIntTag(ComboBox combo, int tagValue)
        {
            foreach (ComboBoxItem item in combo.Items)
            {
                if (item.Tag is int v && v == tagValue)
                {
                    item.IsSelected = true;
                    return;
                }
            }
        }

        // 由 时:分 + 日期选项（今天/明天/后天/自定义）计算目标关机时刻
        private DateTime? ComputeTargetFromTime()
        {
            if (!(HourCombo?.SelectedItem is ComboBoxItem hourItem) ||
                !(MinuteCombo?.SelectedItem is ComboBoxItem minItem) ||
                !(DateModeCombo?.SelectedItem is ComboBoxItem dateItem))
                return null;

            int hour = hourItem.Tag is int h ? h : 0;
            int minute = minItem.Tag is int m ? m : 0;
            var now = DateTime.Now;
            var date = now.Date;
            var tag = dateItem.Tag?.ToString();
            if (tag == "Tomorrow") date = date.AddDays(1);
            else if (tag == "DayAfter") date = date.AddDays(2);
            else if (tag == "Custom") date = (_shutdownCustomDate ?? now.Date);

            var target = date + new TimeSpan(hour, minute, 0);
            if (tag == "Today" && target <= now) target = target.AddDays(1);
            return target;
        }

        // 由 时:分 + 日期选项计算目标唤醒时刻
        private DateTime? ComputeWakeTarget()
        {
            if (!(WakeHourCombo?.SelectedItem is ComboBoxItem hourItem) ||
                !(WakeMinuteCombo?.SelectedItem is ComboBoxItem minItem) ||
                !(WakeDateModeCombo?.SelectedItem is ComboBoxItem dateItem))
                return null;

            int hour = hourItem.Tag is int h ? h : 8;
            int minute = minItem.Tag is int m ? m : 0;
            var now = DateTime.Now;
            var date = now.Date;
            var tag = dateItem.Tag?.ToString();
            if (tag == "Tomorrow") date = date.AddDays(1);
            else if (tag == "DayAfter") date = date.AddDays(2);
            else if (tag == "Custom") date = (_wakeCustomDate ?? now.Date.AddDays(1));

            var target = date + new TimeSpan(hour, minute, 0);
            if (tag == "Today" && target <= now) target = target.AddDays(1);
            return target;
        }

        // 在主面板内部弹出日历覆盖层（不开新窗口，避免独立窗口拿不到前台激活的问题）
        private Action<DateTime?> _datePickerCallback;

        private void ShowDatePicker(DateTime? initial, DateTime minDate, Action<DateTime?> onResult)
        {
            var start = initial ?? minDate;
            if (start < minDate) start = minDate;

            _datePickerCallback = onResult;
            OverlayCalendar.DisplayDateStart = minDate.Date;
            OverlayCalendar.DisplayDate = start.Date;
            OverlayCalendar.SelectedDate = start.Date;
            DatePickerOverlay.Visibility = Visibility.Visible;
        }

        private void OverlayOk_Click(object sender, RoutedEventArgs e)
        {
            var d = OverlayCalendar.SelectedDate;
            DatePickerOverlay.Visibility = Visibility.Collapsed;
            var cb = _datePickerCallback;
            _datePickerCallback = null;
            cb?.Invoke(d?.Date);
        }

        private void OverlayCancel_Click(object sender, RoutedEventArgs e)
        {
            DatePickerOverlay.Visibility = Visibility.Collapsed;
            var cb = _datePickerCallback;
            _datePickerCallback = null;
            cb?.Invoke(null);
        }

        // 程序化把下拉切回指定 Tag（不触发联动 / 不再弹对话框）
        private void RestoreComboTag(ComboBox combo, string tag)
        {
            bool prev = _suppressLink;
            _suppressLink = true;
            foreach (ComboBoxItem item in combo.Items)
            {
                if (item.Tag?.ToString() == tag) { item.IsSelected = true; break; }
            }
            _suppressLink = prev;
        }

        // 改时/分 → 联动倒计时
        private void ShutdownTime_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressLink) return;
            RelinkShutdownFromTime();
        }

        // 改日期下拉（今天/明天/后天）→ 联动；自定义日期由下拉项点击处理
        private void ShutdownDate_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressLink) return;
            RelinkShutdownFromTime();
        }

        private void DateCombo_DropOpened(object sender, EventArgs e)
        {
            _prevShutdownDateTag = (DateModeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Today";
        }

        // 点击“自定义”项（即使已选中也触发）→ 延迟到下拉关闭后弹日期对话框
        private void ShutdownCustom_Pick(object sender, MouseButtonEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ShowDatePicker(_shutdownCustomDate, DateTime.Today, picked =>
                {
                    if (picked == null) { RestoreComboTag(DateModeCombo, _prevShutdownDateTag); return; }
                    _shutdownCustomDate = picked.Value;
                    SetCustomItemContent(DateModeCombo, $"自定义 {picked.Value:MM-dd}");
                    RelinkShutdownFromTime();
                });
            }), DispatcherPriority.Background);
        }

        // 唤醒日期改变：选“今天”但时间已过则跳到明天（自定义由下拉项点击处理）
        private void WakeDate_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressLink) return;
            AutoRollWakeIfPast();
        }

        // 唤醒时/分改变：同样的过期顺延
        private void WakeTime_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressLink) return;
            AutoRollWakeIfPast();
        }

        private void AutoRollWakeIfPast()
        {
            if (!(WakeHourCombo?.SelectedItem is ComboBoxItem hourItem)) return;
            if (!(WakeMinuteCombo?.SelectedItem is ComboBoxItem minItem)) return;
            if (!(WakeDateModeCombo?.SelectedItem is ComboBoxItem dateItem)) return;
            if (dateItem.Tag?.ToString() != "Today") return;

            int hour = hourItem.Tag is int h ? h : -1;
            int minute = minItem.Tag is int m ? m : -1;
            if (hour < 0 || minute < 0) return;

            var now = DateTime.Now;
            var target = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0);
            if (target <= now)
            {
                _suppressLink = true;
                foreach (ComboBoxItem item in WakeDateModeCombo.Items)
                {
                    if (item.Tag?.ToString() == "Tomorrow")
                    {
                        item.IsSelected = true;
                        _prevWakeDateTag = "Tomorrow";
                        break;
                    }
                }
                _suppressLink = false;
            }
        }

        private void WakeCombo_DropOpened(object sender, EventArgs e)
        {
            _prevWakeDateTag = (WakeDateModeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Tomorrow";
        }

        private void WakeCustom_Pick(object sender, MouseButtonEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ShowDatePicker(_wakeCustomDate, DateTime.Today, picked =>
                {
                    if (picked == null) { RestoreComboTag(WakeDateModeCombo, _prevWakeDateTag); return; }
                    _wakeCustomDate = picked.Value;
                    SetCustomItemContent(WakeDateModeCombo, $"自定义 {picked.Value:MM-dd}");
                });
            }), DispatcherPriority.Background);
        }

        private void SetCustomItemContent(ComboBox combo, string text)
        {
            foreach (ComboBoxItem item in combo.Items)
            {
                if (item.Tag?.ToString() == "Custom") { item.Content = text; break; }
            }
        }

        // 由定时（时:分 + 日期）反算并联动倒计时框
        private void RelinkShutdownFromTime()
        {
            _suppressLink = true;
            try
            {
                if ((DateModeCombo?.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "Today")
                    AutoRollToTomorrowIfPast();
                var target = ComputeTargetFromTime();
                if (target != null)
                {
                    _previewTarget = target;
                    int mins = Math.Max(1, (int)Math.Ceiling((target.Value - DateTime.Now).TotalMinutes));
                    CountdownMinutesBox.Text = mins.ToString();
                }
            }
            finally { _suppressLink = false; }
            UpdateReminderOptions();
        }

        // 选“今天”但所选时:分已过当前时间 → 立即把日期切到“明天”
        private void AutoRollToTomorrowIfPast()
        {
            if (!(HourCombo?.SelectedItem is ComboBoxItem hourItem)) return;
            if (!(MinuteCombo?.SelectedItem is ComboBoxItem minItem)) return;
            if (!(DateModeCombo?.SelectedItem is ComboBoxItem dateItem)) return;
            if (dateItem.Tag?.ToString() != "Today") return;

            int hour = hourItem.Tag is int h ? h : -1;
            int minute = minItem.Tag is int m ? m : -1;
            if (hour < 0 || minute < 0) return;

            var now = DateTime.Now;
            var target = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0);
            if (target <= now)
            {
                foreach (ComboBoxItem item in DateModeCombo.Items)
                {
                    if (item.Tag?.ToString() == "Tomorrow")
                    {
                        item.IsSelected = true;
                        _prevShutdownDateTag = "Tomorrow";
                        break;
                    }
                }
            }
        }

        // 按距关机的剩余时间，禁用提前量过大的提醒项；当前项失效则回退到最大可用项
        private void UpdateReminderOptions()
        {
            if (ReminderCombo == null || ReminderCombo.Items.Count == 0) return;

            double secondsUntil;
            if (int.TryParse(CountdownMinutesBox.Text, out int mins) && mins > 0)
            {
                secondsUntil = mins * 60.0;
            }
            else
            {
                if (!(HourCombo?.SelectedItem is ComboBoxItem hourItem) ||
                    !(MinuteCombo?.SelectedItem is ComboBoxItem minItem) ||
                    !(DateModeCombo?.SelectedItem is ComboBoxItem dateItem))
                    return;

                int hour = hourItem.Tag is int h ? h : 0;
                int minute = minItem.Tag is int m ? m : 0;
                var now = DateTime.Now;
                var target = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0);
                if (dateItem.Tag?.ToString() == "Tomorrow") target = target.AddDays(1);
                else if (target <= now) target = target.AddDays(1);
                secondsUntil = (target - now).TotalSeconds;
            }

            var currentSel = ReminderCombo.SelectedItem as ComboBoxItem;
            ComboBoxItem lastEnabled = null;
            bool currentDisabled = false;

            foreach (ComboBoxItem item in ReminderCombo.Items)
            {
                int sec = (item.Tag is string t && int.TryParse(t, out int s)) ? s : 0;
                bool ok = sec < secondsUntil;
                item.IsEnabled = ok;
                if (ok) lastEnabled = item;
                if (item == currentSel && !ok) currentDisabled = true;
            }

            // 当前选中项已不可用 → 回退到仍可用的最大提前量
            if (currentDisabled && lastEnabled != null)
                lastEnabled.IsSelected = true;
        }

        private void CancelShutdownBtn_Click(object sender, RoutedEventArgs e)
        {
            _shutdownService.CancelShutdown();
            _shutdownVM.UpdateActiveState();
            RefreshStatus();
            StateApplied?.Invoke();
        }

        #endregion

        #region 防止睡眠 Tab 事件

        private void PreventSleepToggle_Changed(object sender, RoutedEventArgs e)
        {
            // 屏幕常亮是“防止系统睡眠”的必要条件，二者联动：
            if (sender == KeepDisplayToggle)
            {
                // 勾上屏幕常亮 → 自动勾上防止系统睡眠
                if (KeepDisplayToggle.IsChecked == true)
                    PreventSleepToggle.IsChecked = true;
            }
            else if (sender == PreventSleepToggle)
            {
                // 取消防止系统睡眠 → 屏幕常亮也随之取消（失去依托）
                if (PreventSleepToggle.IsChecked != true)
                    KeepDisplayToggle.IsChecked = false;
            }
        }

        private void DurationCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressLink) return;
            if (!(DurationCombo.SelectedItem is ComboBoxItem item) || !(item.Tag is string tag))
                return;
            if (!Enum.TryParse(tag, out DurationMode mode))
                return;

            if (CustomMinutesPanel != null)
                CustomMinutesPanel.Visibility = mode == DurationMode.Custom
                    ? Visibility.Visible : Visibility.Collapsed;

            _currentDurationMode = mode;
            // 自定义的具体日期/天数由下拉项点击处理
        }

        private void DurationCombo_DropOpened(object sender, EventArgs e)
        {
            _prevDurationMode = _currentDurationMode;
        }

        // 点击“自定义”项 → 弹日期对话框（保持到该日期，至少明天）
        private void SleepCustom_Pick(object sender, MouseButtonEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ShowDatePicker(_sleepCustomDate, DateTime.Today.AddDays(1), picked =>
                {
                    if (picked == null)
                    {
                        RestoreComboTag(DurationCombo, _prevDurationMode.ToString());
                        _currentDurationMode = _prevDurationMode;
                        if (_prevDurationMode != DurationMode.Custom && CustomMinutesPanel != null)
                            CustomMinutesPanel.Visibility = Visibility.Collapsed;
                        return;
                    }
                    _sleepCustomDate = picked.Value;
                    int days = Math.Max(1, (int)(picked.Value.Date - DateTime.Today).TotalDays);
                    _sleepCustomMinutes = days * 1440;
                    if (CustomDateLabel != null)
                        CustomDateLabel.Text = $"保持至 {picked.Value:MM-dd}（约 {days} 天）";
                    if (CustomMinutesPanel != null)
                        CustomMinutesPanel.Visibility = Visibility.Visible;
                    _currentDurationMode = DurationMode.Custom;
                });
            }), DispatcherPriority.Background);
        }

        // 清除防睡眠任务：关闭“防止睡眠”和“保持屏幕常亮”
        private void ClearSleepBtn_Click(object sender, RoutedEventArgs e)
        {
            PreventSleepToggle.IsChecked = false;
            KeepDisplayToggle.IsChecked = false;
            ApplySleepPrevent();
            RefreshStatus();
            StateApplied?.Invoke();
        }

        #endregion

        #region 定时唤醒 Tab 事件

        private void CancelWakeBtn_Click(object sender, RoutedEventArgs e)
        {
            _wakeVM.CancelWake();
            _sleepService.Cancel();
            RefreshStatus();
            StateApplied?.Invoke();
        }

        #endregion

        #region 底部操作栏

        // 底部统一“清除任务”：对当前所在 Tab 执行对应清除
        private void ClearTaskBtn_Click(object sender, RoutedEventArgs e)
        {
            switch (MainTabControl.SelectedIndex)
            {
                case 1: ClearSleepBtn_Click(sender, e); break;
                case 2: CancelWakeBtn_Click(sender, e); break;
                default: CancelShutdownBtn_Click(sender, e); break;
            }
        }

        private void ApplyBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                switch (MainTabControl.SelectedIndex)
                {
                    case 0: // 定时关机
                        ApplyShutdown();
                        break;
                    case 1: // 防止睡眠
                        ApplySleepPrevent();
                        break;
                    case 2: // 定时唤醒
                        ApplyWake();
                        break;
                }

                RefreshStatus();
                StateApplied?.Invoke();
            }
            catch (Exception ex)
            {
                var owner = Window.GetWindow(this);
                if (owner != null)
                    MessageBox.Show(owner, ex.Message, "PowerCapsule", MessageBoxButton.OK, MessageBoxImage.Warning);
                else
                    MessageBox.Show(ex.Message, "PowerCapsule", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // 冲突确认弹窗：用户选“是”才继续，否则取消本次设置
        // 必须以面板窗口为 owner，否则会被 Topmost 面板遮挡（模态框看不见 → 像卡死）
        private bool ConfirmConflict(string msgKey)
        {
            var owner = Window.GetWindow(this);
            var msg = LocalizationManager.L(msgKey);
            var title = LocalizationManager.L("Conflict.Title");
            var r = owner != null
                ? MessageBox.Show(owner, msg, title, MessageBoxButton.YesNo, MessageBoxImage.Warning)
                : MessageBox.Show(msg, title, MessageBoxButton.YesNo, MessageBoxImage.Warning);
            return r == MessageBoxResult.Yes;
        }

        private void ApplyShutdown()
        {
            // 冲突：已有定时唤醒 → 关机后无法被唤醒，唤醒失效
            if (_wakeTaskService.IsWakeScheduled && !ConfirmConflict("Conflict.ShutdownVsWake"))
                return;

            // 如果倒计时输入框有有效值，使用倒计时模式
            if (int.TryParse(CountdownMinutesBox.Text, out int mins) && mins > 0)
            {
                _shutdownService.ScheduleCountdown(mins * 60);
            }
            else
            {
                // 使用固定时间模式（支持 今天/明天/后天/自定义）
                var target = ComputeTargetFromTime();
                if (target == null) return;
                _shutdownService.ScheduleFixedTime(target.Value);
            }

            _shutdownVM.UpdateActiveState();

            // 保存提醒设置
            var reminderItem = ReminderCombo.SelectedItem as ComboBoxItem;
            if (reminderItem?.Tag is string tag && int.TryParse(tag, out int reminderSec))
            {
                _config.ShutdownReminderSeconds = reminderSec;
                _configService.Save(_config);
            }
        }

        private void ApplySleepPrevent()
        {
            var isEnabled = PreventSleepToggle.IsChecked ?? false;
            var keepDisplay = KeepDisplayToggle.IsChecked ?? false;

            // 冲突：开启防睡眠时若有待机任务（主动睡眠/休眠）→ 二者矛盾
            if (isEnabled && _sleepService.IsScheduled)
            {
                if (!ConfirmConflict("Conflict.PreventVsSleep")) return;
                _sleepService.Cancel();
            }

            var customMins = 30;
            if (_currentDurationMode == DurationMode.Custom)
                customMins = _sleepCustomMinutes;

            _sleepVM.IsEnabled = isEnabled;
            _sleepVM.KeepDisplayOn = keepDisplay;
            _sleepVM.SelectedDuration = _currentDurationMode;
            _sleepVM.CustomMinutes = Math.Max(1, customMins);

            _sleepVM.Apply();

            // 保存配置
            _config.PreventSleep.Enabled = isEnabled;
            _config.PreventSleep.KeepDisplayOn = keepDisplay;
            _config.PreventSleep.DurationMode = _currentDurationMode.ToString();
            _config.PreventSleep.CustomMinutes = Math.Max(1, customMins);
            _configService.Save(_config);
        }

        private void ApplyWake()
        {
            var target = ComputeWakeTarget();
            var actionTag = (SleepActionCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            bool wantStandby = actionTag == "Sleep" || actionTag == "Hibernate";

            // 冲突1：待机（主动睡眠/休眠）与已开启的防止睡眠矛盾
            if (wantStandby && _sleepPreventService.IsActive)
            {
                if (!ConfirmConflict("Conflict.SleepVsPrevent")) return;
                // 自动关闭防止睡眠 + 屏幕常亮，并同步 UI / 配置
                _sleepPreventService.Stop();
                _sleepVM.IsEnabled = false;
                _sleepVM.KeepDisplayOn = false;
                PreventSleepToggle.IsChecked = false;
                KeepDisplayToggle.IsChecked = false;
                _config.PreventSleep.Enabled = false;
                _config.PreventSleep.KeepDisplayOn = false;
                _configService.Save(_config);
            }

            // 冲突2：设定唤醒，但已有定时关机 → 关机后无法被唤醒
            if (target != null && _shutdownService.IsShutdownScheduled)
            {
                if (!ConfirmConflict("Conflict.WakeVsShutdown")) return;
            }

            if (target != null)
            {
                _wakeTaskService.ScheduleWake(target.Value);

                _config.Wake.Enabled = true;
                _config.Wake.Time = target.Value.ToString("HH:mm");
                _config.Wake.DateMode = (WakeDateModeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Tomorrow";
                _configService.Save(_config);
            }

            // 延时睡眠 / 休眠
            if (actionTag == "Sleep" || actionTag == "Hibernate")
            {
                int delay = 0;
                if ((SleepDelayCombo.SelectedItem as ComboBoxItem)?.Tag is string dt)
                    int.TryParse(dt, out delay);
                _sleepService.Schedule(actionTag == "Hibernate", delay);
            }
            else
            {
                _sleepService.Cancel();
            }

            _wakeVM.UpdateStatus();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            // 只撤销未应用的修改，不关闭面板
            _shutdownVM.UpdateActiveState();
            _sleepVM.UpdateStatus();
            _wakeVM.UpdateStatus();

            PreventSleepToggle.IsChecked = _sleepVM.IsEnabled;
            KeepDisplayToggle.IsChecked = _sleepVM.KeepDisplayOn;
            SleepStatusText.Text = _sleepVM.StatusText;
            ShutdownStatusText.Text = _shutdownVM.ActiveShutdownInfo;
            WakeStatusText.Text = _wakeVM.StatusText;

            PanelClosed?.Invoke();
        }

        #endregion
    }
}
