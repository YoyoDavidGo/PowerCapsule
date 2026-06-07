using System;

namespace PowerCapsule.Utils
{
    public static class TimeHelper
    {
        public static string FormatRemainingTime(int totalSeconds)
        {
            if (totalSeconds <= 0) return "00:00";
            var ts = TimeSpan.FromSeconds(totalSeconds);
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
            return $"{ts.Minutes:D2}:{ts.Seconds:D2}";
        }

        public static string FormatPreventSleepRemaining(int totalMinutes)
        {
            if (totalMinutes <= 0) return "";
            if (totalMinutes >= 60)
                return $"{totalMinutes / 60}小时{(totalMinutes % 60 > 0 ? $"{totalMinutes % 60}分" : "")}";
            return $"{totalMinutes}分钟";
        }

        public static int CalculateSecondsToTime(DateTime targetTime)
        {
            var now = DateTime.Now;
            var target = new DateTime(now.Year, now.Month, now.Day,
                targetTime.Hour, targetTime.Minute, 0);

            if (target <= now)
                target = target.AddDays(1);

            return (int)(target - now).TotalSeconds;
        }

        public static DateTime CalculateExecutionTime(int countdownSeconds)
        {
            return DateTime.Now.AddSeconds(countdownSeconds);
        }
    }
}
