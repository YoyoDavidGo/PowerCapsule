using System;
using System.Timers;
using PowerCapsule.Models;
using PowerCapsule.Utils;

namespace PowerCapsule.Services
{
    public class SleepPreventService : IDisposable
    {
        private Timer _durationTimer;
        private DateTime? _endTime;

        public bool IsActive { get; private set; }
        public bool IsKeepDisplayOn { get; private set; }

        public event Action StateChanged;
        public event Action<int> RemainingMinutesChanged;

        public void Start(bool keepDisplayOn, DurationMode durationMode, int customMinutes)
        {
            uint flags = NativeMethods.ES_CONTINUOUS | NativeMethods.ES_SYSTEM_REQUIRED;
            if (keepDisplayOn)
                flags |= NativeMethods.ES_DISPLAY_REQUIRED;

            NativeMethods.SetThreadExecutionState(flags);
            IsActive = true;
            IsKeepDisplayOn = keepDisplayOn;

            StopDurationTimer();

            int minutes = 0;
            switch (durationMode)
            {
                case DurationMode.Minutes30: minutes = 30; break;
                case DurationMode.Hours1: minutes = 60; break;
                case DurationMode.Hours2: minutes = 120; break;
                case DurationMode.Hours5: minutes = 300; break;
                case DurationMode.Hours10: minutes = 600; break;
                case DurationMode.Days1: minutes = 1440; break;
                case DurationMode.Days2: minutes = 2880; break;
                case DurationMode.Custom: minutes = Math.Max(1, customMinutes); break;
                case DurationMode.Always: break;
            }

            if (durationMode != DurationMode.Always && minutes > 0)
            {
                _endTime = DateTime.Now.AddMinutes(minutes);
                _durationTimer = new Timer(30000);
                _durationTimer.Elapsed += OnDurationTick;
                _durationTimer.Start();
            }

            StateChanged?.Invoke();
        }

        public void Stop()
        {
            NativeMethods.SetThreadExecutionState(NativeMethods.ES_CONTINUOUS);
            IsActive = false;
            IsKeepDisplayOn = false;
            StopDurationTimer();
            StateChanged?.Invoke();
        }

        public int GetRemainingMinutes()
        {
            if (!IsActive || _endTime == null)
                return 0;
            return Math.Max(0, (int)(_endTime.Value - DateTime.Now).TotalMinutes);
        }

        private void OnDurationTick(object sender, ElapsedEventArgs e)
        {
            var remaining = GetRemainingMinutes();
            RemainingMinutesChanged?.Invoke(remaining);

            if (remaining <= 0)
            {
                var app = System.Windows.Application.Current;
                if (app == null) return;
                var dispatcher = app.Dispatcher;
                if (dispatcher == null || dispatcher.HasShutdownStarted) return;
                dispatcher.BeginInvoke(new Action(Stop));
            }
        }

        private void StopDurationTimer()
        {
            if (_durationTimer != null)
            {
                _durationTimer.Stop();
                _durationTimer.Dispose();
                _durationTimer = null;
            }
            _endTime = null;
        }

        public void Dispose()
        {
            if (IsActive)
                Stop();
            StopDurationTimer();
        }
    }
}
