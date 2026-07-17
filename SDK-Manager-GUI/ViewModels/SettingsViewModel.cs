using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using SDK_Manager_GUI.Services;

namespace SDK_Manager_GUI.ViewModels
{
    public class LanguageItem : ViewModelBase
    {
        public string Code { get; }
        public string DisplayName { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public LanguageItem(string code, string displayName)
        {
            Code = code;
            DisplayName = displayName;
        }
    }

    public class SettingsViewModel : ViewModelBase
    {
        private string _defaultInstallPath;
        public string DefaultInstallPath
        {
            get => _defaultInstallPath;
            set => SetProperty(ref _defaultInstallPath, value);
        }

        private int _maxConcurrentDownloads;
        public int MaxConcurrentDownloads
        {
            get => _maxConcurrentDownloads;
            set => SetProperty(ref _maxConcurrentDownloads, value);
        }

        private int _maxRetryCount;
        public int MaxRetryCount
        {
            get => _maxRetryCount;
            set => SetProperty(ref _maxRetryCount, value);
        }

        // ===== Python Embeddable 安装选项 =====
        private bool _pythonInstallPip = true;
        public bool PythonInstallPip
        {
            get => _pythonInstallPip;
            set => SetProperty(ref _pythonInstallPip, value);
        }

        private bool _pythonEnableSitePackages = true;
        public bool PythonEnableSitePackages
        {
            get => _pythonEnableSitePackages;
            set => SetProperty(ref _pythonEnableSitePackages, value);
        }

        private bool _pythonInstallTclTk = false;
        public bool PythonInstallTclTk
        {
            get => _pythonInstallTclTk;
            set => SetProperty(ref _pythonInstallTclTk, value);
        }

        private bool _pythonInstallIdle = false;
        public bool PythonInstallIdle
        {
            get => _pythonInstallIdle;
            set => SetProperty(ref _pythonInstallIdle, value);
        }

        private bool _pythonRegisterRegistry = false;
        public bool PythonRegisterRegistry
        {
            get => _pythonRegisterRegistry;
            set => SetProperty(ref _pythonRegisterRegistry, value);
        }

        private bool _pythonAssociateFiles = false;
        public bool PythonAssociateFiles
        {
            get => _pythonAssociateFiles;
            set => SetProperty(ref _pythonAssociateFiles, value);
        }

        private readonly IConfigService _configService;
        private readonly IDialogService _dialogService;
        private readonly ILogService _logService;
        private readonly ILanguageService _languageService;

        public ObservableCollection<LanguageItem> Languages { get; } = new ObservableCollection<LanguageItem>
        {
            new LanguageItem("zh-CN", "简体中文"),
            new LanguageItem("zh-TW", "繁體中文"),
            new LanguageItem("en", "English")
        };

        private LanguageItem _selectedLanguage;
        public LanguageItem SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                var prev = _selectedLanguage;
                if (SetProperty(ref _selectedLanguage, value) && value != null)
                {
                    if (prev != null) prev.IsSelected = false;
                    value.IsSelected = true;
                    _languageService.SwitchLanguage(value.Code);
                }
            }
        }

        public ICommand LoadSettingsCommand { get; }
        public ICommand SaveSettingsCommand { get; }
        public ICommand BrowseInstallPathCommand { get; }
        public ICommand ResetSettingsCommand { get; }
        public ICommand SelectLanguageCommand { get; }

        public SettingsViewModel(IConfigService configService, IDialogService dialogService, ILogService logService, ILanguageService languageService)
        {
            _configService = configService;
            _dialogService = dialogService;
            _logService = logService;
            _languageService = languageService;
            _selectedLanguage = Languages.FirstOrDefault(l => l.Code == _languageService.CurrentLanguage) ?? Languages[0];
            _selectedLanguage.IsSelected = true;

            LoadSettingsCommand = new RelayCommand(async () => await LoadSettingsAsync());
            SaveSettingsCommand = new RelayCommand(async () => await SaveSettingsAsync());
            BrowseInstallPathCommand = new RelayCommand(async () => await BrowseInstallPathAsync());
            ResetSettingsCommand = new RelayCommand(async () => await ResetSettingsAsync());
            SelectLanguageCommand = new RelayCommand<LanguageItem>(item => { if (item != null) SelectedLanguage = item; });
        }

        private async Task LoadSettingsAsync()
        {
            var config = await _configService.GetConfigAsync();
            DefaultInstallPath = config.DefaultInstallPath;
            MaxConcurrentDownloads = config.MaxConcurrentDownloads;
            MaxRetryCount = config.MaxRetryCount;
            PythonInstallPip = config.PythonInstallPip;
            PythonEnableSitePackages = config.PythonEnableSitePackages;
            PythonInstallTclTk = config.PythonInstallTclTk;
            PythonInstallIdle = config.PythonInstallIdle;
            PythonRegisterRegistry = config.PythonRegisterRegistry;
            PythonAssociateFiles = config.PythonAssociateFiles;
        }

        private async Task SaveSettingsAsync()
        {
            // 输入验证
            if (MaxConcurrentDownloads < 1) MaxConcurrentDownloads = 1;
            if (MaxConcurrentDownloads > 10) MaxConcurrentDownloads = 10;
            if (MaxRetryCount < 0) MaxRetryCount = 0;
            if (MaxRetryCount > 10) MaxRetryCount = 10;

            var config = await _configService.GetConfigAsync();
            config.DefaultInstallPath = DefaultInstallPath;
            config.MaxConcurrentDownloads = MaxConcurrentDownloads;
            config.MaxRetryCount = MaxRetryCount;
            config.PythonInstallPip = PythonInstallPip;
            config.PythonEnableSitePackages = PythonEnableSitePackages;
            config.PythonInstallTclTk = PythonInstallTclTk;
            config.PythonInstallIdle = PythonInstallIdle;
            config.PythonRegisterRegistry = PythonRegisterRegistry;
            config.PythonAssociateFiles = PythonAssociateFiles;

            await _configService.SaveConfigAsync(config);

            _logService.Info($"{_languageService.GetString("Dialog_SettingsSaved")} - {_languageService.GetString("Settings_InstallPath")}: {DefaultInstallPath}");

            await _dialogService.ShowInfoAsync(_languageService.GetString("Dialog_SaveSuccess"), _languageService.GetString("Dialog_SettingsSaved"));
        }

        private async Task BrowseInstallPathAsync()
        {
            var path = await _dialogService.ShowFolderBrowserDialogAsync(_languageService.GetString("Settings_SelectInstallPath"));
            if (!string.IsNullOrEmpty(path))
                DefaultInstallPath = path;
        }

        private async Task ResetSettingsAsync()
        {
            var confirm = await _dialogService.ShowConfirmAsync(_languageService.GetString("Settings_ResetConfirm"), _languageService.GetString("Settings_ResetConfirmMsg"));
            if (!confirm) return;

            await _configService.ResetConfigAsync();
            await LoadSettingsAsync();

            _logService.Info(_languageService.GetString("Settings_ResetDefault"));
        }
    }
}
