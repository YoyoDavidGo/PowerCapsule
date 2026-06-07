using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace PowerCapsule.Services
{
    // 延时让系统进入睡眠 / 休眠
    public class SleepService
    {
        [DllImport("powrprof.dll", SetLastError = true)]
        private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

        private DispatcherTimer _timer;

        public bool IsScheduled { get; private set; }
        public bool IsHibernate { get; private set; }
        public DateTime? TargetTime { get; private set; }

        public event Action StateChanged;

        // delayMinutes <= 0 表示“立即”
        public void Schedule(bool hibernate, int delayMinutes)
        {
            Cancel();
            IsHibernate = hibernate;

            if (delayMinutes <= 0)
            {
                Execute();
                return;
            }

            TargetTime = DateTime.Now.AddMinutes(delayMinutes);
            IsScheduled = true;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) =>
            {
                if (TargetTime != null && DateTime.Now >= TargetTime.Value)
                    Execute();
            };
            _timer.Start();
            StateChanged?.Invoke();
        }

        public void Cancel()
        {
            StopTimer();
            IsScheduled = false;
            TargetTime = null;
            StateChanged?.Invoke();
        }

        public int GetRemainingSeconds()
        {
            if (!IsScheduled || TargetTime == null) return 0;
            return Math.Max(0, (int)(TargetTime.Value - DateTime.Now).TotalSeconds);
        }

        private void Execute()
        {
            StopTimer();
            IsScheduled = false;
            var hib = IsHibernate;
            TargetTime = null;
            StateChanged?.Invoke();

            // 进入睡眠/休眠（系统挂起，恢复后此调用返回）
            SetSuspendState(hib, false, false);
        }

        private void StopTimer()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer = null;
            }
        }
    }
}
