using System;

namespace SDK_Manager_GUI.Services
{
    public interface ILanguageService
    {
        string CurrentLanguage { get; }
        string GetString(string key);
        void SwitchLanguage(string languageCode);
        void InitializeLanguage();
        event EventHandler LanguageChanged;
    }
}
