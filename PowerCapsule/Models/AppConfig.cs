using Newtonsoft.Json;

namespace PowerCapsule.Models
{
    public class AppConfig
    {
        [JsonProperty("lastTab")]
        public string LastTab { get; set; } = "Shutdown";

        [JsonProperty("capsulePosition")]
        public CapsulePosition CapsulePosition { get; set; } = new CapsulePosition();

        [JsonProperty("autoCollapse")]
        public bool AutoCollapse { get; set; } = true;

        [JsonProperty("opacity")]
        public double Opacity { get; set; } = 0.95;

        [JsonProperty("startWithWindows")]
        public bool StartWithWindows { get; set; } = false;

        [JsonProperty("showCapsuleOnStartup")]
        public bool ShowCapsuleOnStartup { get; set; } = true;

        [JsonProperty("shutdownReminderSeconds")]
        public int ShutdownReminderSeconds { get; set; } = 60;

        [JsonProperty("language")]
        public string Language { get; set; } = "zh";

        [JsonProperty("preventSleep")]
        public PreventSleepConfig PreventSleep { get; set; } = new PreventSleepConfig();

        [JsonProperty("wake")]
        public WakeConfig Wake { get; set; } = new WakeConfig();

        [JsonProperty("ui")]
        public UIConfig UI { get; set; } = new UIConfig();
    }

    public class CapsulePosition
    {
        // -1 表示首次启动 / 未设置 — 由 CapsuleWindow.Window_Loaded 计算合理默认位置
        [JsonProperty("x")]
        public double X { get; set; } = -1;

        [JsonProperty("y")]
        public double Y { get; set; } = -1;
    }

    public class PreventSleepConfig
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = false;

        [JsonProperty("keepDisplayOn")]
        public bool KeepDisplayOn { get; set; } = false;

        [JsonProperty("durationMode")]
        public string DurationMode { get; set; } = "Always";

        [JsonProperty("customMinutes")]
        public int CustomMinutes { get; set; } = 0;
    }

    public class WakeConfig
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = false;

        [JsonProperty("time")]
        public string Time { get; set; } = "08:00";

        [JsonProperty("dateMode")]
        public string DateMode { get; set; } = "Tomorrow";
    }

    public class UIConfig
    {
        [JsonProperty("theme")]
        public string Theme { get; set; } = "Light";

        [JsonProperty("panelWidth")]
        public int PanelWidth { get; set; } = 390;
    }
}
