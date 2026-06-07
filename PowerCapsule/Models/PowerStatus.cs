namespace PowerCapsule.Models
{
    public enum CapsuleDisplayMode
    {
        Idle,
        PreventSleep,
        TimedShutdown,
        CountdownShutdown,
        TimedWake,
        CriticalCountdown
    }

    public enum ShutdownMode
    {
        FixedTime,
        Countdown
    }

    public enum DurationMode
    {
        Always,
        Minutes30,
        Hours1,
        Hours2,
        Hours5,
        Hours10,
        Days1,
        Days2,
        Custom
    }

    public enum DateMode
    {
        Today,
        Tomorrow
    }

    public class PowerStatusInfo
    {
        public CapsuleDisplayMode DisplayMode { get; set; } = CapsuleDisplayMode.Idle;
        public string MainText { get; set; } = "空闲";
        public string SubText { get; set; } = "点击设置";
        public bool ShowGreenDot { get; set; }

        public bool IsPreventSleepActive { get; set; }
        public bool IsShutdownScheduled { get; set; }
        public bool IsWakeScheduled { get; set; }
        public string ShutdownTime { get; set; }
        public string WakeTime { get; set; }
        public int ShutdownRemainingSeconds { get; set; }
        public int PreventSleepRemainingMinutes { get; set; }
    }
}
