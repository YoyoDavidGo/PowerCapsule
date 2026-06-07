using System;
using System.IO;
using Newtonsoft.Json;
using PowerCapsule.Models;

namespace PowerCapsule.Services
{
    public class ConfigService
    {
        private readonly string _configDir;
        private readonly string _configPath;
        private readonly string _backupPath;

        public ConfigService()
        {
            _configDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PowerCapsule");
            _configPath = Path.Combine(_configDir, "config.json");
            _backupPath = Path.Combine(_configDir, "config.bak.json");
        }

        public AppConfig Load()
        {
            try
            {
                if (!File.Exists(_configPath))
                {
                    var defaults = new AppConfig();
                    Save(defaults);
                    return defaults;
                }

                var json = File.ReadAllText(_configPath);
                return JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
            }
            catch
            {
                // 配置损坏时备份并恢复默认
                try
                {
                    if (File.Exists(_configPath))
                    {
                        File.Copy(_configPath, _backupPath, overwrite: true);
                    }
                }
                catch { }

                var defaults = new AppConfig();
                Save(defaults);
                return defaults;
            }
        }

        public void Save(AppConfig config)
        {
            try
            {
                if (!Directory.Exists(_configDir))
                    Directory.CreateDirectory(_configDir);

                var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(_configPath, json);
            }
            catch { }
        }
    }
}
