using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using SDK_Manager_GUI.Models;

namespace SDK_Manager_GUI.Services
{
    public class LanguageService : ILanguageService
    {
        private string _currentLanguage = "zh-CN";
        private readonly IConfigService _configService;

        public string CurrentLanguage => _currentLanguage;

        public event EventHandler LanguageChanged;

        public LanguageService(IConfigService configService)
        {
            _configService = configService;
            LoadSavedLanguage();
        }

        private void LoadSavedLanguage()
        {
            try
            {
                var config = _configService.GetConfigSync();
                if (!string.IsNullOrEmpty(config.Language))
                {
                    _currentLanguage = config.Language;
                }
                else
                {
                    // First launch: detect system UI language
                    _currentLanguage = DetectSystemLanguage();
                    // Persist the detected language
                    config.Language = _currentLanguage;
                    _ = _configService.SaveConfigAsync(config);
                }
            }
            catch
            {
                _currentLanguage = DetectSystemLanguage();
            }
        }

        private string DetectSystemLanguage()
        {
            var culture = CultureInfo.CurrentUICulture;
            var langCode = culture.Name; // e.g. "zh-CN", "zh-TW", "zh-HK", "en-US", "ja-JP"

            if (langCode.StartsWith("zh"))
            {
                // zh-TW, zh-HK, zh-MO → Traditional Chinese
                if (langCode == "zh-TW" || langCode == "zh-HK" || langCode == "zh-MO")
                    return "zh-TW";
                // zh-CN, zh-SG and other zh variants → Simplified Chinese
                return "zh-CN";
            }

            // All other languages default to English
            return "en";
        }

        public string GetString(string key)
        {
            try
            {
                var value = Application.Current.FindResource(key);
                return value as string ?? key;
            }
            catch
            {
                return key;
            }
        }

        public void SwitchLanguage(string languageCode)
        {
            SwitchLanguageInternal(languageCode, force: false);
        }

        public void SwitchLanguage(string languageCode, bool force)
        {
            SwitchLanguageInternal(languageCode, force);
        }

        private void SwitchLanguageInternal(string languageCode, bool force)
        {
            if (!force && _currentLanguage == languageCode) return;

            _currentLanguage = languageCode;

            // Replace the language ResourceDictionary
            var dictionaries = Application.Current.Resources.MergedDictionaries;
            var langDict = dictionaries.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Languages/"));
            if (langDict != null)
            {
                dictionaries.Remove(langDict);
            }

            var newDict = new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/Languages/{languageCode}.xaml", UriKind.Absolute)
            };
            dictionaries.Add(newDict);

            // Persist language preference
            try
            {
                var config = _configService.GetConfigSync();
                config.Language = languageCode;
                _ = _configService.SaveConfigAsync(config);
            }
            catch { }

            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }

        public void InitializeLanguage()
        {
            // Always apply the current language (which may be detected from system)
            SwitchLanguage(_currentLanguage, force: true);
        }
    }
}
