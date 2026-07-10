using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SDK_Manager_GUI.Models;
using System.IO;

namespace SDK_Manager_GUI.Services
{
    public class ConfigService : IConfigService
    {
        private readonly string _configPath;
        private AppConfig _cachedConfig;
        private readonly object _lock = new object();

        public ConfigService()
        {
            string appDataPath = null;
            var candidates = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SDK-Manager-GUI"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SDK-Manager-GUI"),
            };

            foreach (var candidate in candidates)
            {
                try
                {
                    if (!Directory.Exists(candidate))
                        Directory.CreateDirectory(candidate);
                    var testFile = Path.Combine(candidate, ".write_test");
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);
                    appDataPath = candidate;
                    break;
                }
                catch
                {
                    continue;
                }
            }

            if (appDataPath == null)
                appDataPath = AppDomain.CurrentDomain.BaseDirectory;

            _configPath = Path.Combine(appDataPath, "config.json");
        }

        public async Task<AppConfig> GetConfigAsync()
        {
            lock (_lock)
            {
                if (_cachedConfig != null)
                    return _cachedConfig;
            }

            if (File.Exists(_configPath))
            {
                var json = await Task.Run(() =>
                {
                    lock (_lock)
                    {
                        return File.ReadAllText(_configPath);
                    }
                });
                var config = JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
                lock (_lock)
                {
                    _cachedConfig = config;
                }
                return config;
            }

            var newConfig = new AppConfig();
            lock (_lock)
            {
                _cachedConfig = newConfig;
            }
            return newConfig;
        }

        public async Task SaveConfigAsync(AppConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            lock (_lock)
            {
                _cachedConfig = config;
            }

            var json = JsonConvert.SerializeObject(config, Formatting.Indented);
            await Task.Run(() =>
            {
                lock (_lock)
                {
                    var dir = Path.GetDirectoryName(_configPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    File.WriteAllText(_configPath, json);
                }
            });
        }

        public AppConfig GetConfigSync()
        {
            lock (_lock)
            {
                if (_cachedConfig != null)
                    return _cachedConfig;
            }

            try
            {
                if (File.Exists(_configPath))
                {
                    string json;
                    lock (_lock)
                    {
                        json = File.ReadAllText(_configPath);
                    }
                    var config = JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
                    lock (_lock)
                    {
                        _cachedConfig = config;
                    }
                    return config;
                }
            }
            catch { }

            var newConfig = new AppConfig();
            lock (_lock)
            {
                _cachedConfig = newConfig;
            }
            return newConfig;
        }

        public async Task ResetConfigAsync()
        {
            var newConfig = new AppConfig();
            lock (_lock)
            {
                _cachedConfig = newConfig;
            }
            var json = JsonConvert.SerializeObject(newConfig, Formatting.Indented);
            await Task.Run(() =>
            {
                lock (_lock)
                {
                    File.WriteAllText(_configPath, json);
                }
            });
        }
    }
}
