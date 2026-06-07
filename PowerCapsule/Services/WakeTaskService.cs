using System;
using System.Globalization;
using PowerCapsule.Utils;

namespace PowerCapsule.Services
{
    public class WakeTaskService
    {
        private const string TaskName = "PowerCapsuleWakeTask";

        public bool IsWakeScheduled { get; private set; }
        public DateTime? WakeTime { get; private set; }

        public void ScheduleWake(DateTime wakeTime)
        {
            DeleteWakeTask();

            var timeStr = wakeTime.ToString("HH:mm");
            // schtasks 的 /sd 日期格式必须匹配当前用户区域设置的短日期格式，
            // 硬编码 yyyy/MM/dd 在 en-US 等区域会被拒绝导致任务创建失败
            var dateStr = wakeTime.ToString(CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern,
                CultureInfo.CurrentCulture);

            var createArgs =
                $"/create /tn \"{TaskName}\" " +
                $"/tr \"cmd /c exit\" " +
                $"/sc once " +
                $"/st {timeStr} " +
                $"/sd {dateStr} " +
                "/f";

            var result = ProcessHelper.RunCommand("schtasks", createArgs);

            if (!result.Success)
                throw new InvalidOperationException("定时唤醒任务创建失败，请检查任务计划程序是否可用。");

            var wakeArgs = $"/change /tn \"{TaskName}\" /settings /waketorun:on";
            ProcessHelper.RunCommand("schtasks", wakeArgs);

            IsWakeScheduled = true;
            WakeTime = wakeTime;
        }

        public void DeleteWakeTask()
        {
            var result = ProcessHelper.RunCommand("schtasks", $"/delete /tn \"{TaskName}\" /f");
            IsWakeScheduled = false;
            WakeTime = null;
        }

        public bool CheckWakeTaskExists()
        {
            var result = ProcessHelper.RunCommand("schtasks", $"/query /tn \"{TaskName}\"");
            return result.Success;
        }
    }
}
