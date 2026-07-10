using System;
using System.Reflection;
using SDK_Manager_GUI.Services;

namespace SDK_Manager_GUI.ViewModels
{
    public class AboutViewModel : ViewModelBase
    {
        private string _appName = "SDK Manager GUI";
        public string AppName
        {
            get => _appName;
            set => SetProperty(ref _appName, value);
        }

        private string _version;
        public string Version
        {
            get => _version;
            set => SetProperty(ref _version, value);
        }

        private string _description;
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        private string _copyright;
        public string Copyright
        {
            get => _copyright;
            set => SetProperty(ref _copyright, value);
        }

        private readonly ILanguageService _languageService;

        public AboutViewModel(ILanguageService languageService)
        {
            _languageService = languageService;
            _version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
            UpdateTexts();
            _languageService.LanguageChanged += (s, e) => UpdateTexts();
        }

        private void UpdateTexts()
        {
            Description = _languageService.GetString("About_Description");
            Copyright = string.Format(_languageService.GetString("About_Copyright"), DateTime.Now.Year);
        }
    }
}
