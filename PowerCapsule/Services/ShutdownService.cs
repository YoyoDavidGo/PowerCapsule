using System;
using PowerCapsule.Utils;

namespace PowerCapsule.Services
{
    public class ShutdownService
    {
        public bool IsShutdownScheduled { get; private set; }
        public DateTime? ScheduledTime { get; private set; }
        public int TotalSeconds { get; private set; }

        public void ScheduleFixedTime(DateTime targetTime)
        {
            var now = DateTime.Now;
            if (targetTime <= now)
                targetTime = targetTime.AddDays(1);

            var seconds = (int)(targetTime - now).TotalSeconds;
            if (seconds <= 0)
                throw new InvalidOperationException("关机时间已过");

            InternalSchedule(seconds);
        }

        public void ScheduleCountdown(int totalSeconds)
        {
            if (totalSeconds <= 0)
                throw new ArgumentException("倒计时必须大于0秒");

            InternalSchedule(totalSeconds);
        }

        private void InternalSchedule(int seconds)
        {
            // 先取消已有任务
            CancelShutdown();

            var result = ProcessHelper.RunCommand("shutdown", $"/s /t {seconds}");
            if (!result.Success)
                throw new InvalidOperationException("关机任务创建失败，请检查系统权限或安全软件限制。");

            IsShutdownScheduled = true;
            TotalSeconds = seconds;
            ScheduledTime = DateTime.Now.AddSeconds(seconds);
        }

        public void CancelShutdown()
        {
            ProcessHelper.RunCommand("shutdown", "/a");
            IsShutdownScheduled = false;
            ScheduledTime = null;
            TotalSeconds = 0;
        }

        public int GetRemainingSeconds()
        {
            if (!IsShutdownScheduled || ScheduledTime == null)
                return 0;

            var remaining = (int)(ScheduledTime.Value - DateTime.Now).TotalSeconds;
            return Math.Max(0, remaining);
        }
    }
}
