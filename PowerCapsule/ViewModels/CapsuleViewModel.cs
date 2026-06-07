using System;
using System.Collections.Generic;
using System.Windows.Threading;
using PowerCapsule.Models;
using PowerCapsule.Services;

namespace PowerCapsule.ViewModels
{
    public class CapsuleViewModel : ViewModelBase
    {
        private readonly ShutdownService _shutdownService;
        private readonly SleepPreventService _sleepPreventService;
        private readonly WakeTaskService _wakeTaskService;
        private readonly SleepService _sleepService;
        private DispatcherTimer _updateTimer;
        private int _rotationIndex;   // 当前轮播到第几条状态
        private int _rotationTick;    // 累计秒数，满 2 秒推进一次

        private string _mainText = "空闲";
        public string MainText
        {
            get => _mainText;
            set => SetProperty(ref _mainText, value);
        }

        private string _subText = "点击设置";
        public string SubText
        {
            get => _subText;
            set => SetProperty(ref _subText, value);
        }

        private bool _showGreenDot;
        public bool ShowGreenDot
        {
            get => _showGreenDot;
            set => SetProperty(ref _showGreenDot, value);
        }

        private bool _isPanelOpen;
        public bool IsPanelOpen
        {
            get => _isPanelOpen;
            set => SetProperty(ref _isPanelOpen, value);
        }

        private string _arrowText = "▼";
        public string ArrowText
        {
            get => _arrowText;
            set => SetProperty(ref _arrowText, value);
        }

        private CapsuleDisplayMode _displayMode;
        public CapsuleDisplayMode DisplayMode
        {
            get => _displayMode;
            set => SetProperty(ref _displayMode, value);
        }

        public CapsuleViewModel(
            ShutdownService shutdownService,
            SleepPreventService sleepPreventService,
            WakeTaskService wakeTaskService,
            SleepService sleepService)
        {
            _shutdownService = shutdownService;
            _sleepPreventService = sleepPreventService;
            _wakeTaskService = wakeTaskService;
            _sleepService = sleepService;

            _sleepPreventService.StateChanged += OnStateChanged;
            _sleepService.StateChanged += OnStateChanged;
            Services.LocalizationManager.LanguageChanged += UpdateDisplay;

            _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _updateTimer.Tick += (s, e) =>
            {
                // 每 5 秒轮换到下一条状态（多个任务时滚动显示）
                if (++_rotationTick >= 5) { _rotationTick = 0; _rotationIndex++; }
                UpdateDisplay();
            };
            _updateTimer.Start();
        }

        public void UpdateDisplay()
        {
            var items = GetActiveStates();
            if (items.Count == 0) return;

            var cur = items[_rotationIndex % items.Count];
            DisplayMode = cur.Mode;
            MainText = cur.Main;
            SubText = cur.Sub;
            ShowGreenDot = cur.GreenDot;
        }

        private static string L(string key) => Services.LocalizationManager.L(key);
        private static string Remain(string formatted) => Services.LocalizationManager.LF("Cap.RemainFmt", formatted);

        // 收集所有进行中的状态；多个时由计时器每 2 秒轮换显示
        private List<StatusItem> GetActiveStates()
        {
            var list = new List<StatusItem>();

            if (_shutdownService.IsShutdownScheduled)
            {
                int remaining = _shutdownService.GetRemainingSeconds();
                if (remaining <= 60 && remaining > 0)
                {
                    // 临界关机：紧急，独占显示，不参与轮换
                    list.Add(new StatusItem(CapsuleDisplayMode.CriticalCountdown,
                        Services.LocalizationManager.LF("Cap.SecToOffFmt", remaining),
                        L("Cap.Imminent"), true));
                    return list;
                }
                var formatted = Utils.TimeHelper.FormatRemainingTime(remaining);
                var execTime = DateTime.Now.AddSeconds(remaining);
                list.Add(new StatusItem(CapsuleDisplayMode.TimedShutdown,
                    $"{execTime:HH:mm} {L("Cap.Off")}  ·  {Remain(formatted)}", L("Tab.Shutdown"), true));
            }

            if (_sleepPreventService.IsActive)
            {
                int remMin = _sleepPreventService.GetRemainingMinutes();
                var label = _sleepPreventService.IsKeepDisplayOn ? L("Cap.KeepAwake") : L("Cap.PreventSleep");
                var tail = remMin > 0
                    ? Remain(Utils.TimeHelper.FormatRemainingTime(remMin * 60))
                    : L("Cap.Always");
                list.Add(new StatusItem(CapsuleDisplayMode.PreventSleep, $"{label}  ·  {tail}", "", true));
            }

            if (_wakeTaskService.IsWakeScheduled)
            {
                var wake = _wakeTaskService.WakeTime;
                if (wake != null)
                {
                    int sec = (int)(wake.Value - DateTime.Now).TotalSeconds;
                    var remStr = Utils.TimeHelper.FormatRemainingTime(sec);
                    list.Add(new StatusItem(CapsuleDisplayMode.TimedWake,
                        $"{wake.Value:HH:mm} {L("Cap.Wake")}  ·  {Remain(remStr)}", L("Tab.Wake"), true));
                }
                else
                {
                    list.Add(new StatusItem(CapsuleDisplayMode.TimedWake,
                        L("Cap.Wake"), L("Tab.Wake"), true));
                }
            }


            if (_sleepService.IsScheduled)
            {
                int sec = _sleepService.GetRemainingSeconds();
                var act = _sleepService.IsHibernate ? L("Cap.Hibernate") : L("Cap.Sleep");
                var remStr = Utils.TimeHelper.FormatRemainingTime(sec);
                list.Add(new StatusItem(CapsuleDisplayMode.PreventSleep,
                    $"{act}  ·  {Remain(remStr)}", L("Wk.Standby"), true));
            }

            if (list.Count == 0)
                list.Add(new StatusItem(CapsuleDisplayMode.Idle, L("Cap.Idle"), L("Cap.ClickSet"), false));

            return list;
        }

        private struct StatusItem
        {
            public CapsuleDisplayMode Mode;
            public string Main;
            public string Sub;
            public bool GreenDot;
            public StatusItem(CapsuleDisplayMode mode, string main, string sub, bool greenDot)
            {
                Mode = mode;
                Main = main;
                Sub = sub;
                GreenDot = greenDot;
            }
        }

        public void TogglePanel()
        {
            IsPanelOpen = !IsPanelOpen;
            ArrowText = IsPanelOpen ? "▲" : "▼";
        }

        private void OnStateChanged()
        {
            var app = System.Windows.Application.Current;
            if (app == null) return;
            var dispatcher = app.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted) return;
            dispatcher.BeginInvoke(new Action(UpdateDisplay));
        }

        public void Dispose()
        {
            _updateTimer?.Stop();
            _sleepPreventService.StateChanged -= OnStateChanged;
            _sleepService.StateChanged -= OnStateChanged;
            Services.LocalizationManager.LanguageChanged -= UpdateDisplay;
        }
    }
}
