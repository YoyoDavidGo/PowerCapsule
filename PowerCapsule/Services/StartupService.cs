using Microsoft.Win32;
using System;
using System.Reflection;

namespace PowerCapsule.Services
{
    public class StartupService
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "PowerCapsule";

        public bool IsStartupEnabled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath))
            {
                var value = key?.GetValue(AppName) as string;
                return !string.IsNullOrEmpty(value);
            }
        }

        public void SetStartup(bool enable)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true))
            {
                if (enable)
                {
                    var exePath = Assembly.GetEntryAssembly()?.Location
                        ?? System.Windows.Forms.Application.ExecutablePath;
                    key?.SetValue(AppName, $"\"{exePath}\"");
                }
                else
                {
                    key?.DeleteValue(AppName, throwOnMissingValue: false);
                }
            }
        }
    }
}
