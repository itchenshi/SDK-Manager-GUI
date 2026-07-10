using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using SDK_Manager_GUI.Models;
using SDK_Manager_GUI.Services;

namespace SDK_Manager_GUI.ViewModels
{
    public class SdkDetailViewModel : ViewModelBase
    {
        private const string NOT_INSTALLED_MARKER = "NOT_INSTALLED_MARKER";

        private string _language;
        public string Language
        {
            get => _language;
            set => SetProperty(ref _language, value);
        }

        private string _currentVersion;
        public string CurrentVersion
        {
            get => _currentVersion;
            set => SetProperty(ref _currentVersion, value);
        }

        private string _installPath;
        public string InstallPath
        {
            get => _installPath;
            set => SetProperty(ref _installPath, value);
        }

        // 用户级SDK信息
        private string _userLevelVersion;
        public string UserLevelVersion
        {
            get => _userLevelVersion;
            set
            {
                if (SetProperty(ref _userLevelVersion, value))
                {
                    OnPropertyChanged(nameof(HasUserLevel));
                    OnPropertyChanged(nameof(UserLevelVersionDisplay));
                }
            }
        }

        public string UserLevelVersionDisplay => UserLevelVersion == NOT_INSTALLED_MARKER
            ? _languageService.GetString("Common_NotInstalled")
            : UserLevelVersion ?? "";

        private string _userLevelPath;
        public string UserLevelPath
        {
            get => _userLevelPath;
            set => SetProperty(ref _userLevelPath, value);
        }

        // 系统级SDK信息
        private string _systemLevelVersion;
        public string SystemLevelVersion
        {
            get => _systemLevelVersion;
            set
            {
                if (SetProperty(ref _systemLevelVersion, value))
                {
                    OnPropertyChanged(nameof(HasSystemLevel));
                    OnPropertyChanged(nameof(SystemLevelVersionDisplay));
                }
            }
        }

        public string SystemLevelVersionDisplay => SystemLevelVersion == NOT_INSTALLED_MARKER
            ? _languageService.GetString("Common_NotInstalled")
            : SystemLevelVersion ?? "";

        private string _systemLevelPath;
        public string SystemLevelPath
        {
            get => _systemLevelPath;
            set { if (SetProperty(ref _systemLevelPath, value)) OnPropertyChanged(nameof(HasSystemLevel)); }
        }

        /// <summary>
        /// 用户级是否已配置（有版本信息）
        /// </summary>
        public bool HasUserLevel => !string.IsNullOrEmpty(UserLevelVersion) && UserLevelVersion != NOT_INSTALLED_MARKER;

        /// <summary>
        /// 系统级是否已配置（有版本信息）
        /// </summary>
        public bool HasSystemLevel => !string.IsNullOrEmpty(SystemLevelVersion) && SystemLevelVersion != NOT_INSTALLED_MARKER;

        private ObservableCollection<SdkVersionItem> _versions;
        public ObservableCollection<SdkVersionItem> Versions
        {
            get => _versions;
            set => SetProperty(ref _versions, value);
        }

        private SdkVersionItem _selectedVersion;
        public SdkVersionItem SelectedVersion
        {
            get => _selectedVersion;
            set => SetProperty(ref _selectedVersion, value);
        }

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set { if (SetProperty(ref _searchText, value)) FilterVersions(); }
        }

        private string _categoryFilter;
        public string CategoryFilter
        {
            get => _categoryFilter;
            set { if (SetProperty(ref _categoryFilter, value)) FilterVersions(); }
        }

        public ObservableCollection<string> CategoryOptions { get; }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { if (SetProperty(ref _isLoading, value)) OnPropertyChanged(nameof(IsBusy)); }
        }

        private bool _isInstalling;
        public bool IsInstalling
        {
            get => _isInstalling;
            set { if (SetProperty(ref _isInstalling, value)) OnPropertyChanged(nameof(IsBusy)); }
        }

        public bool IsBusy => IsLoading || IsInstalling || IsUninstalling;

        private bool _isUninstalling;
        public bool IsUninstalling
        {
            get => _isUninstalling;
            set { if (SetProperty(ref _isUninstalling, value)) OnPropertyChanged(nameof(IsBusy)); }
        }

        private double _operationProgress;
        public double OperationProgress
        {
            get => _operationProgress;
            set => SetProperty(ref _operationProgress, value);
        }

        private string _progressMessage;
        public string ProgressMessage
        {
            get => _progressMessage;
            set => SetProperty(ref _progressMessage, value);
        }

        private readonly ISdkManagerService _sdkManagerService;
        private readonly IDialogService _dialogService;
        private readonly IBackgroundTaskManager _backgroundTaskManager;
        private readonly IMirrorProvider _mirrorProvider;
        private readonly IPackageManagerMirrorService _packageManagerMirrorService;
        private readonly ILanguageService _languageService;
        private ObservableCollection<SdkVersionItem> _allVersions;
        private bool _isLocalOperation;
        private bool _isLoadingVersions;

        public ICommand LoadVersionsCommand { get; }
        public ICommand InstallCommand { get; }
        public ICommand InstallSystemCommand { get; }
        public ICommand UninstallUserCommand { get; }
        public ICommand UninstallSystemCommand { get; }
        public ICommand UninstallUserLevelCommand { get; }
        public ICommand UninstallSystemLevelCommand { get; }
        public ICommand LoadMirrorsCommand { get; }
        public ICommand SaveMirrorCommand { get; }
        public ICommand EditMirrorCommand { get; }
        public ICommand CancelEditMirrorCommand { get; }
        public ICommand ToggleMirrorCommand { get; }
        public ICommand RemoveMirrorCommand { get; }
        public ICommand TestMirrorLatencyCommand { get; }
        public ICommand DetectPackageManagerMirrorCommand { get; }
        public ICommand SetPackageManagerMirrorCommand { get; }
        public ICommand ApplyCustomPackageManagerMirrorCommand { get; }
        public ICommand CopyPathCommand { get; }
        public ICommand OpenPathCommand { get; }

        /// <summary>
        /// 所有SDK都支持下载镜像源设置
        /// </summary>
        public bool SupportsMirrorConfig => true;

        /// <summary>
        /// 是否支持包管理器镜像源设置（Python=pip, NodeJs=npm）
        /// </summary>
        public bool SupportsPackageManagerMirror => Language == "Python" || Language == "NodeJs";

        /// <summary>
        /// 是否显示发布日期列（仅当版本数据中有发布日期时显示）
        /// </summary>
        public bool ShowReleaseDateColumn
        {
            get => _showReleaseDateColumn;
            set => SetProperty(ref _showReleaseDateColumn, value);
        }
        private bool _showReleaseDateColumn;

        /// <summary>
        /// 包管理器镜像源 Tab 标题
        /// </summary>
        public string PackageManagerMirrorTabTitle => Language switch
        {
            "Python" => _languageService.GetString("Sdk_PipMirrorTab"),
            "NodeJs" => _languageService.GetString("Sdk_NpmMirrorTab"),
            _ => _languageService.GetString("Sdk_MirrorConfig")
        };

        /// <summary>
        /// 包管理器镜像源说明
        /// </summary>
        public string PackageManagerMirrorDescription => Language switch
        {
            "Python" => _languageService.GetString("Sdk_PipMirrorDesc"),
            "NodeJs" => _languageService.GetString("Sdk_NpmMirrorDesc"),
            _ => ""
        };

        /// <summary>
        /// 包管理器镜像源区域标题
        /// </summary>
        public string PackageManagerMirrorSectionTitle => Language switch
        {
            "Python" => _languageService.GetString("Sdk_PipMirrorConfig"),
            "NodeJs" => _languageService.GetString("Sdk_NpmMirrorConfig"),
            _ => _languageService.GetString("Sdk_MirrorConfig")
        };

        /// <summary>
        /// 安装操作步骤
        /// </summary>
        public string InstallStepsText => Language switch
        {
            "Python" => _languageService.GetString("Desc_Python_InstallSteps"),
            "NodeJs" => _languageService.GetString("Desc_NodeJs_InstallSteps"),
            "Java" => _languageService.GetString("Desc_Java_InstallSteps"),
            _ => _languageService.GetString("Desc_Default_InstallSteps")
        };

        /// <summary>
        /// 安装相关命令
        /// </summary>
        public string InstallCommandsText => Language switch
        {
            "Python" => _languageService.GetString("Desc_Python_InstallCommands"),
            "NodeJs" => _languageService.GetString("Desc_NodeJs_InstallCommands"),
            "Java" => _languageService.GetString("Desc_Java_InstallCommands"),
            _ => ""
        };

        /// <summary>
        /// 卸载操作步骤
        /// </summary>
        public string UninstallStepsText => Language switch
        {
            "Python" => _languageService.GetString("Desc_Python_UninstallSteps"),
            "NodeJs" => _languageService.GetString("Desc_NodeJs_UninstallSteps"),
            "Java" => _languageService.GetString("Desc_Java_UninstallSteps"),
            _ => _languageService.GetString("Desc_Default_UninstallSteps")
        };

        /// <summary>
        /// 卸载相关命令
        /// </summary>
        public string UninstallCommandsText => Language switch
        {
            "Python" => _languageService.GetString("Desc_Python_UninstallCommands"),
            "NodeJs" => _languageService.GetString("Desc_NodeJs_UninstallCommands"),
            "Java" => _languageService.GetString("Desc_Java_UninstallCommands"),
            _ => ""
        };

        /// <summary>
        /// 包管理器镜像源设置说明（Python、NodeJs 使用）
        /// </summary>
        public string MirrorSetupText => Language switch
        {
            "Python" => _languageService.GetString("Desc_Python_MirrorSetup"),
            "NodeJs" => _languageService.GetString("Desc_NodeJs_MirrorSetup"),
            _ => ""
        };

        /// <summary>
        /// 镜像源设置标题（说明 Tab 中使用）
        /// </summary>
        public string MirrorSetupTitle => Language switch
        {
            "NodeJs" => _languageService.GetString("Sdk_NpmMirrorSetup"),
            "Python" => _languageService.GetString("Sdk_PipMirrorSetup"),
            _ => _languageService.GetString("Sdk_MirrorSetup")
        };

        private ObservableCollection<MirrorSource> _mirrors;
        public ObservableCollection<MirrorSource> Mirrors
        {
            get => _mirrors;
            set => SetProperty(ref _mirrors, value);
        }

        private MirrorSource _selectedMirror;
        public MirrorSource SelectedMirror
        {
            get => _selectedMirror;
            set => SetProperty(ref _selectedMirror, value);
        }

        private string _newMirrorName;
        public string NewMirrorName
        {
            get => _newMirrorName;
            set => SetProperty(ref _newMirrorName, value);
        }

        private string _newMirrorUrl;
        public string NewMirrorUrl
        {
            get => _newMirrorUrl;
            set => SetProperty(ref _newMirrorUrl, value);
        }

        private string _editingMirrorId;
        private bool _editingMirrorIsPreset;
        private bool _editingMirrorIsEnabled;
        private bool _isEditingMirror;
        public bool IsEditingMirror
        {
            get => _isEditingMirror;
            set => SetProperty(ref _isEditingMirror, value);
        }

        public string MirrorSaveButtonText => IsEditingMirror
            ? _languageService.GetString("Common_SaveChanges")
            : _languageService.GetString("Common_Add");

        private string _currentPackageManagerMirror;
        public string CurrentPackageManagerMirror
        {
            get => _currentPackageManagerMirror;
            set => SetProperty(ref _currentPackageManagerMirror, value);
        }

        private string _customPackageManagerMirrorUrl;
        public string CustomPackageManagerMirrorUrl
        {
            get => _customPackageManagerMirrorUrl;
            set => SetProperty(ref _customPackageManagerMirrorUrl, value);
        }

        private ObservableCollection<PresetMirrorItem> _presetPackageManagerMirrors;
        public ObservableCollection<PresetMirrorItem> PresetPackageManagerMirrors
        {
            get => _presetPackageManagerMirrors;
            set => SetProperty(ref _presetPackageManagerMirrors, value);
        }

        public bool IsRunningAsAdmin => EnvironmentManager.IsRunningAsAdmin();

        public SdkDetailViewModel(ISdkManagerService sdkManagerService, IDialogService dialogService, IBackgroundTaskManager backgroundTaskManager, IMirrorProvider mirrorProvider, IPackageManagerMirrorService packageManagerMirrorService, ILanguageService languageService)
        {
            _sdkManagerService = sdkManagerService;
            _dialogService = dialogService;
            _backgroundTaskManager = backgroundTaskManager;
            _mirrorProvider = mirrorProvider;
            _packageManagerMirrorService = packageManagerMirrorService;
            _languageService = languageService;
            _versions = new ObservableCollection<SdkVersionItem>();
            _mirrors = new ObservableCollection<MirrorSource>();
            _allVersions = new ObservableCollection<SdkVersionItem>();
            _presetPackageManagerMirrors = new ObservableCollection<PresetMirrorItem>();

            _categoryFilter = _languageService.GetString("Common_All");
            CategoryOptions = new ObservableCollection<string>
            {
                _languageService.GetString("Common_All"),
                "LTS",
                "Current",
                "PreRelease"
            };

            LoadVersionsCommand = new RelayCommand(async () => await LoadVersionsAsync(), () => !IsBusy);
            InstallCommand = new RelayCommand<SdkVersionItem>(async item => await InstallAsync(item, false), item => item != null && !IsBusy);
            InstallSystemCommand = new RelayCommand<SdkVersionItem>(async item => await InstallAsync(item, true), item => item != null && !IsBusy);
            UninstallUserCommand = new RelayCommand<SdkVersionItem>(async item => await UninstallAsync(item, false), item => item != null && item.IsUserLevelInstalled && !IsBusy);
            UninstallSystemCommand = new RelayCommand<SdkVersionItem>(async item => await UninstallAsync(item, true), item => item != null && item.IsSystemLevelInstalled && !IsBusy);
            UninstallUserLevelCommand = new RelayCommand(async () => await UninstallLevelAsync(false), () => HasUserLevel && !IsBusy);
            UninstallSystemLevelCommand = new RelayCommand(async () => await UninstallLevelAsync(true), () => HasSystemLevel && !IsBusy);
            LoadMirrorsCommand = new RelayCommand(async () => await LoadMirrorsAsync(), () => !IsBusy);
            SaveMirrorCommand = new RelayCommand(async () => await SaveMirrorAsync(), () => !string.IsNullOrEmpty(NewMirrorName) && !string.IsNullOrEmpty(NewMirrorUrl));
            EditMirrorCommand = new RelayCommand<MirrorSource>(m => ShowEditMirrorMode(m), m => m != null);
            CancelEditMirrorCommand = new RelayCommand(() => CancelEditMirror());
            ToggleMirrorCommand = new RelayCommand<MirrorSource>(async m => await ToggleMirrorAsync(m), m => m != null);
            RemoveMirrorCommand = new RelayCommand<MirrorSource>(async m => await RemoveMirrorAsync(m), m => m != null && !m.IsDefault);
            TestMirrorLatencyCommand = new RelayCommand(async () => await TestMirrorLatencyAsync(), () => Mirrors?.Count > 0);
            DetectPackageManagerMirrorCommand = new RelayCommand(async () => await DetectPackageManagerMirrorAsync(), () => SupportsPackageManagerMirror);
            SetPackageManagerMirrorCommand = new RelayCommand<PresetMirrorItem>(async m => await SetPackageManagerMirrorAsync(m?.Url), m => m != null);
            ApplyCustomPackageManagerMirrorCommand = new RelayCommand(async () => await SetPackageManagerMirrorAsync(CustomPackageManagerMirrorUrl), () => SupportsPackageManagerMirror && !string.IsNullOrEmpty(CustomPackageManagerMirrorUrl));
            CopyPathCommand = new RelayCommand<string>(path => CopyPath(path));
            OpenPathCommand = new RelayCommand<string>(path => OpenPath(path));

            WeakMessenger.Register<SdkStatusChangedMessage>(this, OnSdkStatusChanged);
            WeakMessenger.Register<InstallProgressMessage>(this, OnInstallProgress);
            WeakMessenger.Register<InstallCompletedMessage>(this, OnInstallCompleted);

            _languageService.LanguageChanged += (s, e) => OnLanguageChanged();
        }

        private void OnLanguageChanged()
        {
            OnPropertyChanged(nameof(PackageManagerMirrorTabTitle));
            OnPropertyChanged(nameof(PackageManagerMirrorDescription));
            OnPropertyChanged(nameof(PackageManagerMirrorSectionTitle));
            OnPropertyChanged(nameof(InstallStepsText));
            OnPropertyChanged(nameof(InstallCommandsText));
            OnPropertyChanged(nameof(UninstallStepsText));
            OnPropertyChanged(nameof(UninstallCommandsText));
            OnPropertyChanged(nameof(MirrorSetupText));
            OnPropertyChanged(nameof(MirrorSetupTitle));
            OnPropertyChanged(nameof(MirrorSaveButtonText));

            // Refresh category options
            var allText = _languageService.GetString("Common_All");
            CategoryOptions.Clear();
            CategoryOptions.Add(allText);
            CategoryOptions.Add("LTS");
            CategoryOptions.Add("Current");
            CategoryOptions.Add("PreRelease");

            // Reset category filter to "All"
            _categoryFilter = allText;
            OnPropertyChanged(nameof(CategoryFilter));

            // Refresh status texts
            OnPropertyChanged(nameof(HasUserLevel));
            OnPropertyChanged(nameof(HasSystemLevel));
        }

        private void OnInstallProgress(InstallProgressMessage msg)
        {
            if (msg.Language != Language) return;
            OperationProgress = msg.Percent;

            var parts = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(msg.Message))
                parts.Add(msg.Message);
            if (msg.Speed > 0)
            {
                var speedStr = msg.Speed < 1024 ? $"{msg.Speed} B/s"
                    : msg.Speed < 1024 * 1024 ? $"{msg.Speed / 1024.0:F1} KB/s"
                    : $"{msg.Speed / (1024.0 * 1024):F1} MB/s";
                parts.Add(speedStr);
            }
            if (msg.RemainingTime > TimeSpan.Zero && msg.Speed > 0)
            {
                var rt = msg.RemainingTime;
                var rtStr = rt.TotalMinutes < 1 ? $"{rt.Seconds}{_languageService.GetString("Common_Seconds")}"
                    : rt.TotalHours < 1 ? $"{rt.Minutes}{_languageService.GetString("Common_Minutes")}{rt.Seconds}{_languageService.GetString("Common_Seconds")}"
                    : $"{(int)rt.TotalHours}{_languageService.GetString("Common_Hours")}{rt.Minutes}{_languageService.GetString("Common_Minutes")}";
                parts.Add($"{_languageService.GetString("Common_Remaining")} {rtStr}");
            }
            ProgressMessage = string.Join(" | ", parts);
        }

        private async void OnInstallCompleted(InstallCompletedMessage msg)
        {
            if (msg.Language != Language) return;

            var isPaused = msg.ErrorMessage == _languageService.GetString("Dialog_InstallPaused");
            if (isPaused)
            {
                ProgressMessage = _languageService.GetString("Dialog_InstallPausedMsg");
                return;
            }

            IsInstalling = false;
            _isLocalOperation = false;
            OperationProgress = 0;
            ProgressMessage = "";

            if (msg.IsSuccess)
            {
                var item = _allVersions.FirstOrDefault(v => NormalizeVersion(v.Version) == NormalizeVersion(msg.Version));
                if (item != null)
                {
                    if (msg.SystemLevel)
                        item.IsSystemLevelInstalled = true;
                    else
                        item.IsUserLevelInstalled = true;
                    item.IsInstalled = item.IsUserLevelInstalled || item.IsSystemLevelInstalled;
                    item.IsActive = true;
                    foreach (var v in _allVersions)
                    {
                        if (NormalizeVersion(v.Version) != NormalizeVersion(msg.Version))
                            v.IsActive = false;
                    }
                }
                CurrentVersion = NormalizeVersion(msg.Version);

                // 刷新级别信息
                await LoadLevelInfoAsync();

                // 安装完成后刷新包管理器镜像源（Node.js 安装会配置 npm，需要同步）
                if (SupportsPackageManagerMirror)
                {
                    _ = DetectPackageManagerMirrorAsync();
                }

                await _dialogService.ShowInfoAsync(
                    _languageService.GetString("Dialog_InstallSuccess"),
                    string.Format(_languageService.GetString("Dialog_InstallSuccessMsg"), Language, msg.Version));
            }
            else
            {
                await _dialogService.ShowErrorAsync(
                    _languageService.GetString("Dialog_InstallFailed"),
                    msg.ErrorMessage ?? _languageService.GetString("Dialog_UnknownError"));
                await LoadVersionsAsync();
            }
        }

        private static void CopyPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            Clipboard.SetText(path);
        }

        private static void OpenPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                if (System.IO.Directory.Exists(path))
                    System.Diagnostics.Process.Start("explorer.exe", path);
                else if (System.IO.File.Exists(path))
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
            }
            catch { }
        }

        private void OnSdkStatusChanged(SdkStatusChangedMessage msg)
        {
            if (_isLocalOperation) return;
            if (msg.Language != Language) return;
            // 防止并发加载：如果正在加载中，跳过本次触发
            if (_isLoadingVersions) return;
            _ = LoadVersionsAsync();
        }

        public void OnNavigatedTo(object parameter)
        {
            if (parameter is string language)
            {
                if (Language != language)
                {
                    Language = language;
                    OnPropertyChanged(nameof(SupportsMirrorConfig));
                    OnPropertyChanged(nameof(SupportsPackageManagerMirror));
                    OnPropertyChanged(nameof(PackageManagerMirrorTabTitle));
                    OnPropertyChanged(nameof(PackageManagerMirrorDescription));
                    OnPropertyChanged(nameof(PackageManagerMirrorSectionTitle));
                    // 每个SDK有独立ViewModel实例，首次加载时获取版本列表
                    if (_allVersions.Count == 0)
                    {
                        LoadVersionsCommand.Execute(null);
                    }
                    else
                    {
                        // 已有缓存数据，仅刷新级别信息
                        _ = LoadLevelInfoAsync();
                    }
                    // 加载镜像源列表
                    _ = LoadMirrorsAsync();
                    // 加载包管理器镜像源
                    if (SupportsPackageManagerMirror)
                    {
                        LoadPackageManagerMirrorData();
                    }
                }
                else if (!IsBusy)
                {
                    // 同一SDK再次进入，刷新级别信息和镜像源
                    _ = LoadLevelInfoAsync();
                    if (SupportsPackageManagerMirror)
                    {
                        _ = DetectPackageManagerMirrorAsync();
                    }
                }
            }
        }

        private async Task LoadVersionsAsync()
        {
            if (string.IsNullOrEmpty(Language)) return;

            IsLoading = true;
            _isLoadingVersions = true;
            try
            {
                if (!Enum.TryParse<SdkLanguage>(Language, out var sdkLanguage)) return;

                // 保存当前语言，用于后续一致性检查
                var currentLanguage = Language;

                var detection = await _sdkManagerService.DetectSdkAsync(sdkLanguage);

                // 一致性检查：如果加载期间语言已切换，跳过本次结果
                if (Language != currentLanguage) return;

                if (detection.IsInstalled)
                {
                    CurrentVersion = detection.DetectedVersion ?? _languageService.GetString("Common_Unknown");
                }
                else
                {
                    CurrentVersion = _languageService.GetString("Common_NotInstalled");
                }
                InstallPath = detection.InstallPath ?? "";

                // 使用 DetectSdkAsync 返回的用户级/系统级信息
                UserLevelVersion = !string.IsNullOrEmpty(detection.UserLevelVersion) ? detection.UserLevelVersion : NOT_INSTALLED_MARKER;
                UserLevelPath = detection.UserLevelPath ?? "";
                SystemLevelVersion = !string.IsNullOrEmpty(detection.SystemLevelVersion) ? detection.SystemLevelVersion : NOT_INSTALLED_MARKER;
                SystemLevelPath = detection.SystemLevelPath ?? "";

                var installed = await _sdkManagerService.GetInstalledVersionsAsync(sdkLanguage);

                // 一致性检查
                if (Language != currentLanguage) return;

                var available = await _sdkManagerService.GetAvailableVersionsAsync(sdkLanguage);

                // 一致性检查
                if (Language != currentLanguage) return;

                _allVersions.Clear();

                // 并行检查缓存状态，提升加载速度
                var availableList = available.ToList();
                var installedVersions = installed.Select(i2 => NormalizeVersion(i2.Version)).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var cacheTasks = availableList.Select(v =>
                    installedVersions.Contains(NormalizeVersion(v.Version)) ? Task.FromResult(false) : _sdkManagerService.HasCacheAsync(sdkLanguage, v.Version)
                ).ToArray();
                var cacheResults = await Task.WhenAll(cacheTasks);

                for (int i = 0; i < availableList.Count; i++)
                {
                    // 检查是否仍是同一个SDK
                    if (Language != currentLanguage) return;

                    var v = availableList[i];
                    // 多版本支持：检查该版本是否在已安装列表中
                    var installedMatch = installed.FirstOrDefault(i2 =>
                        string.Equals(NormalizeVersion(i2.Version), NormalizeVersion(v.Version), StringComparison.OrdinalIgnoreCase));
                    var isInstalledVal = installedMatch != null;
                    var isActive = isInstalledVal && installedMatch.IsActive;
                    var isUserLevelInstalled = isInstalledVal && installedMatch.IsUserLevel;
                    var isSystemLevelInstalled = isInstalledVal && installedMatch.IsSystemLevel;

                    _allVersions.Add(new SdkVersionItem
                    {
                        Version = v.Version,
                        Category = v.Category,
                        IsInstalled = isInstalledVal,
                        IsActive = isActive,
                        IsUserLevelInstalled = isUserLevelInstalled,
                        IsSystemLevelInstalled = isSystemLevelInstalled,
                        HasCache = cacheResults[i],
                        ReleaseDate = v.ReleaseDate,
                        FileSize = v.FileSize,
                        DownloadUrl = v.DownloadUrl
                    });
                }

                // 最终一致性检查
                if (Language != currentLanguage) return;

                FilterVersions();

                // 检查版本列表中是否有发布日期数据
                ShowReleaseDateColumn = _allVersions.Any(v => v.ReleaseDate.HasValue);
            }
            finally
            {
                IsLoading = false;
                _isLoadingVersions = false;
            }
        }

        /// <summary>
        /// 从环境变量中读取用户级和系统级的SDK版本和路径信息
        /// </summary>
        private async Task LoadLevelInfoAsync()
        {
            if (string.IsNullOrEmpty(Language) || !Enum.TryParse<SdkLanguage>(Language, out var sdkLanguage)) return;

            var detection = await _sdkManagerService.DetectSdkAsync(sdkLanguage);

            UserLevelVersion = !string.IsNullOrEmpty(detection.UserLevelVersion) ? detection.UserLevelVersion : NOT_INSTALLED_MARKER;
            UserLevelPath = detection.UserLevelPath ?? "";
            SystemLevelVersion = !string.IsNullOrEmpty(detection.SystemLevelVersion) ? detection.SystemLevelVersion : NOT_INSTALLED_MARKER;
            SystemLevelPath = detection.SystemLevelPath ?? "";
        }

        private async Task LoadMirrorsAsync()
        {
            if (!Enum.TryParse<SdkLanguage>(Language, out var sdkLanguage)) return;

            var mirrors = await _mirrorProvider.GetMirrorsAsync(sdkLanguage);
            Mirrors = new ObservableCollection<MirrorSource>(mirrors);
        }

        private async Task SaveMirrorAsync()
        {
            if (string.IsNullOrEmpty(NewMirrorName) || string.IsNullOrEmpty(NewMirrorUrl)) return;
            if (!Enum.TryParse<SdkLanguage>(Language, out var sdkLanguage)) return;

            if (IsEditingMirror && !string.IsNullOrEmpty(_editingMirrorId))
            {
                // 编辑模式：更新已有镜像
                var mirror = new MirrorSource
                {
                    Id = _editingMirrorId,
                    Language = sdkLanguage,
                    Name = NewMirrorName,
                    BaseUrl = NewMirrorUrl.TrimEnd('/'),
                    IsEnabled = _editingMirrorIsEnabled,
                    Priority = 100,
                    IsDefault = false,
                    IsPreset = _editingMirrorIsPreset
                };
                await _mirrorProvider.UpdateMirrorAsync(mirror);
                IsEditingMirror = false;
                _editingMirrorId = null;
            }
            else
            {
                // 添加模式：新增镜像
                var mirror = new MirrorSource
                {
                    Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                    Language = sdkLanguage,
                    Name = NewMirrorName,
                    BaseUrl = NewMirrorUrl.TrimEnd('/'),
                    IsEnabled = true,
                    Priority = 100,
                    IsDefault = false
                };
                await _mirrorProvider.AddMirrorAsync(mirror);
            }

            NewMirrorName = "";
            NewMirrorUrl = "";
            await LoadMirrorsAsync();
        }

        private void ShowEditMirrorMode(MirrorSource mirror)
        {
            if (mirror == null) return;
            _editingMirrorId = mirror.Id;
            _editingMirrorIsPreset = mirror.IsPreset;
            _editingMirrorIsEnabled = mirror.IsEnabled;
            IsEditingMirror = true;
            NewMirrorName = mirror.Name;
            NewMirrorUrl = mirror.BaseUrl;
        }

        private void CancelEditMirror()
        {
            IsEditingMirror = false;
            _editingMirrorId = null;
            NewMirrorName = "";
            NewMirrorUrl = "";
        }

        private async Task ToggleMirrorAsync(MirrorSource mirror)
        {
            if (mirror == null) return;
            mirror.IsEnabled = !mirror.IsEnabled;
            await _mirrorProvider.UpdateMirrorAsync(mirror);
            await LoadMirrorsAsync();
        }

        private async Task RemoveMirrorAsync(MirrorSource mirror)
        {
            if (mirror == null || mirror.IsDefault) return;
            await _mirrorProvider.RemoveMirrorAsync(mirror.Id);
            await LoadMirrorsAsync();
        }

        private async Task TestMirrorLatencyAsync()
        {
            if (Mirrors == null || Mirrors.Count == 0) return;
            foreach (var mirror in Mirrors)
            {
                try
                {
                    await _mirrorProvider.TestMirrorLatencyAsync(mirror);
                }
                catch { }
            }
            await LoadMirrorsAsync();
        }

        private void LoadPackageManagerMirrorData()
        {
            if (!SupportsPackageManagerMirror) return;
            if (!Enum.TryParse<SdkLanguage>(Language, out var sdkLanguage)) return;

            var presets = _packageManagerMirrorService.GetPresetMirrors(sdkLanguage);
            PresetPackageManagerMirrors = new ObservableCollection<PresetMirrorItem>(presets);

            _ = DetectPackageManagerMirrorAsync();
        }

        private async Task DetectPackageManagerMirrorAsync()
        {
            if (!SupportsPackageManagerMirror) return;
            if (!Enum.TryParse<SdkLanguage>(Language, out var sdkLanguage)) return;

            // 优先从全局缓存读取，缓存不存在则实时获取
            CurrentPackageManagerMirror = _packageManagerMirrorService.GetCachedMirror(sdkLanguage)
                ?? await _packageManagerMirrorService.GetCurrentMirrorAsync(sdkLanguage);
        }

        private async Task SetPackageManagerMirrorAsync(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            if (!SupportsPackageManagerMirror) return;
            if (!Enum.TryParse<SdkLanguage>(Language, out var sdkLanguage)) return;

            var success = await _packageManagerMirrorService.SetMirrorAsync(sdkLanguage, url);
            if (success)
            {
                await _dialogService.ShowInfoAsync(
                    _languageService.GetString("Dialog_SetMirrorSuccess"),
                    string.Format(_languageService.GetString("Dialog_SetMirrorSuccessMsg"), url));
                await DetectPackageManagerMirrorAsync();
                CustomPackageManagerMirrorUrl = "";
            }
            else
            {
                await _dialogService.ShowErrorAsync(
                    _languageService.GetString("Dialog_SetMirrorFailed"),
                    _languageService.GetString("Dialog_SetMirrorFailedMsg"));
            }
        }

        private async Task InstallAsync(SdkVersionItem item, bool systemLevel)
        {
            if (item == null) return;

            if (systemLevel && !EnvironmentManager.IsRunningAsAdmin())
            {
                await _dialogService.ShowErrorAsync(
                    _languageService.GetString("Dialog_InsufficientPermissions"),
                    _languageService.GetString("Dialog_SystemInstallRequireAdmin"));
                return;
            }

            if (systemLevel && item.IsSystemLevelInstalled)
            {
                await _dialogService.ShowInfoAsync(
                    _languageService.GetString("Dialog_Tip"),
                    _languageService.GetString("Dialog_AlreadySystemInstalled"));
                return;
            }
            if (!systemLevel && item.IsUserLevelInstalled)
            {
                await _dialogService.ShowInfoAsync(
                    _languageService.GetString("Dialog_Tip"),
                    _languageService.GetString("Dialog_AlreadyUserInstalled"));
                return;
            }

            var hasOtherInstalled = _allVersions.Any(v => v.IsInstalled);
            var levelText = systemLevel
                ? _languageService.GetString("Common_SystemLevel")
                : _languageService.GetString("Common_UserLevel");
            var confirmMsg = hasOtherInstalled
                ? string.Format(_languageService.GetString("Dialog_InstallConfirmReplace"), levelText, Language, item.Version)
                : string.Format(_languageService.GetString("Dialog_InstallConfirm"), levelText, Language, item.Version);

            var confirm = await _dialogService.ShowConfirmAsync(
                _languageService.GetString("Dialog_InstallConfirmTitle"), confirmMsg);
            if (!confirm) return;

            if (!Enum.TryParse<SdkLanguage>(Language, out var sdkLanguage)) return;

            var taskId = $"{Language}_{item.Version}";
            if (_backgroundTaskManager.IsTaskRunning(taskId))
            {
                await _dialogService.ShowInfoAsync(
                    _languageService.GetString("Dialog_Tip"),
                    _languageService.GetString("Dialog_InstallingPleaseWait"));
                return;
            }

            IsInstalling = true;
            _isLocalOperation = true;
            OperationProgress = 0;
            ProgressMessage = _languageService.GetString("Dialog_PreparingInstall");

            _backgroundTaskManager.StartInstall(sdkLanguage, item.Version, systemLevel);
        }

        private async Task UninstallAsync(SdkVersionItem item, bool systemLevel)
        {
            if (item == null) return;
            if (systemLevel && !item.IsSystemLevelInstalled) return;
            if (!systemLevel && !item.IsUserLevelInstalled) return;

            if (systemLevel && !EnvironmentManager.IsRunningAsAdmin())
            {
                await _dialogService.ShowErrorAsync(
                    _languageService.GetString("Dialog_InsufficientPermissions"),
                    _languageService.GetString("Dialog_SystemUninstallRequireAdmin"));
                return;
            }

            var levelText = systemLevel
                ? _languageService.GetString("Common_SystemLevel")
                : _languageService.GetString("Common_UserLevel");
            var envLevelText = systemLevel
                ? _languageService.GetString("Common_SystemLevel")
                : _languageService.GetString("Common_UserLevel");
            var confirm = await _dialogService.ShowConfirmAsync(
                _languageService.GetString("Dialog_UninstallConfirmTitle"),
                string.Format(_languageService.GetString("Dialog_UninstallConfirmMsg"), levelText, Language, item.Version, envLevelText));
            if (!confirm) return;

            if (!Enum.TryParse<SdkLanguage>(Language, out var sdkLanguage)) return;

            IsUninstalling = true;
            ProgressMessage = string.Format(_languageService.GetString("Dialog_Uninstalling"), Language, item.Version, levelText);
            _isLocalOperation = true;
            try
            {
                var success = await _sdkManagerService.UninstallAsync(sdkLanguage, item.Version, systemLevel);
                if (success)
                {
                    // 更新卸载级别的状态
                    if (systemLevel)
                        item.IsSystemLevelInstalled = false;
                    else
                        item.IsUserLevelInstalled = false;
                    item.IsInstalled = item.IsUserLevelInstalled || item.IsSystemLevelInstalled;
                    item.IsActive = item.IsInstalled;

                    // 刷新级别信息
                    await LoadLevelInfoAsync();
                    // 重新加载版本列表以同步状态
                    await LoadVersionsAsync();

                    await _dialogService.ShowInfoAsync(
                        _languageService.GetString("Dialog_UninstallSuccess"),
                        string.Format(_languageService.GetString("Dialog_UninstallSuccessMsg"), Language, item.Version, levelText));
                }
                else
                {
                    await _dialogService.ShowErrorAsync(
                        _languageService.GetString("Dialog_UninstallFailed"),
                        string.Format(_languageService.GetString("Dialog_UninstallFailedNotFound"), Language, item.Version));
                    // 刷新版本列表
                    await LoadVersionsAsync();
                }
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync(
                    _languageService.GetString("Dialog_UninstallFailed"), ex.Message);
            }
            finally
            {
                IsUninstalling = false;
                ProgressMessage = "";
                _isLocalOperation = false;
            }
        }

        /// <summary>
        /// 从顶部卡片卸载用户级/系统级SDK
        /// </summary>
        private async Task UninstallLevelAsync(bool systemLevel)
        {
            var version = systemLevel ? SystemLevelVersion : UserLevelVersion;
            var path = systemLevel ? SystemLevelPath : UserLevelPath;
            if (string.IsNullOrEmpty(version) || version == NOT_INSTALLED_MARKER) return;

            if (systemLevel && !EnvironmentManager.IsRunningAsAdmin())
            {
                await _dialogService.ShowErrorAsync(
                    _languageService.GetString("Dialog_InsufficientPermissions"),
                    _languageService.GetString("Dialog_SystemUninstallRequireAdmin"));
                return;
            }

            var levelText = systemLevel
                ? _languageService.GetString("Common_SystemLevel")
                : _languageService.GetString("Common_UserLevel");
            var envLevelText = systemLevel
                ? _languageService.GetString("Common_SystemLevel")
                : _languageService.GetString("Common_UserLevel");
            var confirm = await _dialogService.ShowConfirmAsync(
                _languageService.GetString("Dialog_UninstallConfirmTitle"),
                string.Format(_languageService.GetString("Dialog_UninstallConfirmMsg"), levelText, Language, version, envLevelText));
            if (!confirm) return;

            if (!Enum.TryParse<SdkLanguage>(Language, out var sdkLanguage)) return;

            IsUninstalling = true;
            ProgressMessage = string.Format(_languageService.GetString("Dialog_Uninstalling"), Language, version, levelText);
            _isLocalOperation = true;
            try
            {
                // 从安装路径中提取版本目录名，避免版本号格式不匹配
                // 路径格式：{basePath}/{Language}/{versionDir}/
                string versionDirName = version;
                if (!string.IsNullOrEmpty(path))
                {
                    var trimmed = path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                    var dirName = System.IO.Path.GetFileName(trimmed);
                    if (!string.IsNullOrEmpty(dirName))
                        versionDirName = dirName;
                }

                var success = await _sdkManagerService.UninstallAsync(sdkLanguage, versionDirName, systemLevel);
                if (success)
                {
                    // 刷新级别信息
                    await LoadLevelInfoAsync();
                    // 刷新版本列表
                    await LoadVersionsAsync();

                    await _dialogService.ShowInfoAsync(
                        _languageService.GetString("Dialog_UninstallSuccess"),
                        string.Format(_languageService.GetString("Dialog_UninstallSuccessMsg"), Language, version, levelText));
                }
                else
                {
                    await _dialogService.ShowErrorAsync(
                        _languageService.GetString("Dialog_UninstallFailed"),
                        string.Format(_languageService.GetString("Dialog_UninstallFailedNotFound"), Language, version));
                    // 刷新版本列表
                    await LoadVersionsAsync();
                }
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync(
                    _languageService.GetString("Dialog_UninstallFailed"), ex.Message);
            }
            finally
            {
                IsUninstalling = false;
                ProgressMessage = "";
                _isLocalOperation = false;
            }
        }

        private void FilterVersions()
        {
            if (_allVersions == null) return;
            var filtered = _allVersions.AsEnumerable();

            var allText = _languageService.GetString("Common_All");
            if (CategoryFilter != allText)
            {
                if (Enum.TryParse<VersionCategory>(CategoryFilter, out var cat))
                    filtered = filtered.Where(v => v.Category == cat);
                else
                {
                    // Try matching by original enum name in case language changed
                    if (Enum.TryParse<VersionCategory>(_categoryFilter, out var catFallback))
                        filtered = filtered.Where(v => v.Category == catFallback);
                }
            }

            if (!string.IsNullOrWhiteSpace(SearchText))
                filtered = filtered.Where(v => v.Version.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0);

            var filteredList = filtered.ToList();

            // 复用现有集合，减少 UI 闪烁和内存分配
            Versions.Clear();
            foreach (var item in filteredList)
                Versions.Add(item);
        }

        private static string NormalizeVersion(string version)
        {
            if (string.IsNullOrEmpty(version)) return "";
            return version.TrimStart('v', 'V');
        }
    }
}
