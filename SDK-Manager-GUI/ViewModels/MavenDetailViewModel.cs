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
    public class MavenDetailViewModel : ViewModelBase
    {
        private readonly IMavenService _mavenService;
        private readonly IDialogService _dialogService;
        private readonly IPackageManagerMirrorService _packageManagerMirrorService;
        private readonly ILanguageService _languageService;

        private const string NOT_INSTALLED_MARKER = "NOT_INSTALLED_MARKER";

        // Maven 版本列表
        private ObservableCollection<MavenVersionItem> _mavenVersions;
        public ObservableCollection<MavenVersionItem> MavenVersions
        {
            get => _mavenVersions;
            set => SetProperty(ref _mavenVersions, value);
        }

        private MavenVersionItem _selectedMavenVersion;
        public MavenVersionItem SelectedMavenVersion
        {
            get => _selectedMavenVersion;
            set => SetProperty(ref _selectedMavenVersion, value);
        }

        private bool _isMavenLoading;
        public bool IsMavenLoading
        {
            get => _isMavenLoading;
            set => SetProperty(ref _isMavenLoading, value);
        }

        private bool _isMavenInstalling;
        public bool IsMavenInstalling
        {
            get => _isMavenInstalling;
            set => SetProperty(ref _isMavenInstalling, value);
        }

        // Maven 已安装状态
        private string _mavenUserVersion;
        public string MavenUserVersion
        {
            get => _mavenUserVersion;
            set
            {
                if (SetProperty(ref _mavenUserVersion, value))
                {
                    OnPropertyChanged(nameof(HasMavenUserLevel));
                    OnPropertyChanged(nameof(MavenUserVersionDisplay));
                }
            }
        }

        public string MavenUserVersionDisplay => MavenUserVersion == NOT_INSTALLED_MARKER
            ? _languageService.GetString("Common_NotInstalled")
            : MavenUserVersion ?? "";

        private string _mavenUserPath;
        public string MavenUserPath
        {
            get => _mavenUserPath;
            set => SetProperty(ref _mavenUserPath, value);
        }

        private string _mavenSystemVersion;
        public string MavenSystemVersion
        {
            get => _mavenSystemVersion;
            set
            {
                if (SetProperty(ref _mavenSystemVersion, value))
                {
                    OnPropertyChanged(nameof(HasMavenSystemLevel));
                    OnPropertyChanged(nameof(MavenSystemVersionDisplay));
                }
            }
        }

        public string MavenSystemVersionDisplay => MavenSystemVersion == NOT_INSTALLED_MARKER
            ? _languageService.GetString("Common_NotInstalled")
            : MavenSystemVersion ?? "";

        private string _mavenSystemPath;
        public string MavenSystemPath
        {
            get => _mavenSystemPath;
            set => SetProperty(ref _mavenSystemPath, value);
        }

        public bool HasMavenUserLevel => !string.IsNullOrEmpty(MavenUserVersion) && MavenUserVersion != NOT_INSTALLED_MARKER;
        public bool HasMavenSystemLevel => !string.IsNullOrEmpty(MavenSystemVersion) && MavenSystemVersion != NOT_INSTALLED_MARKER;

        private string _mavenSearchText;
        public string MavenSearchText
        {
            get => _mavenSearchText;
            set { if (SetProperty(ref _mavenSearchText, value)) FilterMavenVersions(); }
        }

        private List<MavenVersionItem> _allMavenVersions;
        private List<MavenVersionItem> AllMavenVersions
        {
            set { _allMavenVersions = value; FilterMavenVersions(); }
        }

        private void FilterMavenVersions()
        {
            if (_allMavenVersions == null) return;
            var filtered = string.IsNullOrWhiteSpace(_mavenSearchText)
                ? _allMavenVersions
                : _allMavenVersions.Where(v => v.Version.IndexOf(_mavenSearchText, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            MavenVersions = new ObservableCollection<MavenVersionItem>(filtered);
        }

        private double _mavenProgress;
        public double MavenProgress
        {
            get => _mavenProgress;
            set => SetProperty(ref _mavenProgress, value);
        }

        private string _mavenProgressMessage;
        public string MavenProgressMessage
        {
            get => _mavenProgressMessage;
            set => SetProperty(ref _mavenProgressMessage, value);
        }

        // Maven 下载镜像源
        private ObservableCollection<MavenDownloadMirror> _mavenDownloadMirrors;
        public ObservableCollection<MavenDownloadMirror> MavenDownloadMirrors
        {
            get => _mavenDownloadMirrors;
            set => SetProperty(ref _mavenDownloadMirrors, value);
        }

        private MavenDownloadMirror _selectedMavenDownloadMirror;
        public MavenDownloadMirror SelectedMavenDownloadMirror
        {
            get => _selectedMavenDownloadMirror;
            set => SetProperty(ref _selectedMavenDownloadMirror, value);
        }

        private string _newMavenMirrorName;
        public string NewMavenMirrorName
        {
            get => _newMavenMirrorName;
            set => SetProperty(ref _newMavenMirrorName, value);
        }

        private string _newMavenMirrorUrl;
        public string NewMavenMirrorUrl
        {
            get => _newMavenMirrorUrl;
            set => SetProperty(ref _newMavenMirrorUrl, value);
        }

        private string _editingMavenMirrorId;
        private bool _editingMavenMirrorIsPreset;
        private bool _editingMavenMirrorIsEnabled;
        private bool _isEditingMavenMirror;
        public bool IsEditingMavenMirror
        {
            get => _isEditingMavenMirror;
            set { if (SetProperty(ref _isEditingMavenMirror, value)) OnPropertyChanged(nameof(MavenMirrorSaveButtonText)); }
        }

        public string MavenMirrorSaveButtonText => IsEditingMavenMirror
            ? _languageService.GetString("Common_SaveChanges")
            : _languageService.GetString("Common_Add");

        public bool IsRunningAsAdmin => EnvironmentManager.IsRunningAsAdmin();

        // Maven 包镜像源（settings.xml）
        private string _currentMavenMirror;
        public string CurrentMavenMirror
        {
            get => _currentMavenMirror;
            set => SetProperty(ref _currentMavenMirror, value);
        }

        private string _customMavenMirrorUrl;
        public string CustomMavenMirrorUrl
        {
            get => _customMavenMirrorUrl;
            set => SetProperty(ref _customMavenMirrorUrl, value);
        }

        private ObservableCollection<PresetMirrorItem> _presetMavenMirrors;
        public ObservableCollection<PresetMirrorItem> PresetMavenMirrors
        {
            get => _presetMavenMirrors;
            set => SetProperty(ref _presetMavenMirrors, value);
        }

        // Local Repository properties
        private string _currentLocalRepo;
        public string CurrentLocalRepo
        {
            get => _currentLocalRepo;
            set => SetProperty(ref _currentLocalRepo, value);
        }

        private bool _isLocalRepoDefault;
        public bool IsLocalRepoDefault
        {
            get => _isLocalRepoDefault;
            set => SetProperty(ref _isLocalRepoDefault, value);
        }

        private string _newLocalRepoPath;
        public string NewLocalRepoPath
        {
            get => _newLocalRepoPath;
            set => SetProperty(ref _newLocalRepoPath, value);
        }

        public ICommand BrowseLocalRepoCommand { get; }
        public ICommand ApplyLocalRepoCommand { get; }

        // Description tab texts
        public string MavenInstallStepsText => _languageService.GetString("Desc_Maven_InstallSteps");
        public string MavenUninstallStepsText => _languageService.GetString("Desc_Maven_UninstallSteps");
        public string MavenCommonCommandsText => _languageService.GetString("Desc_Maven_CommonCommands");
        public string MavenDepMirrorSetupText => _languageService.GetString("Desc_Maven_DepMirrorSetup");
        public string MavenDepMirrorDescText => _languageService.GetString("Maven_DepMirrorDesc");

        public ICommand LoadMavenVersionsCommand { get; }
        public ICommand InstallMavenCommand { get; }
        public ICommand InstallMavenSystemCommand { get; }
        public ICommand UninstallMavenUserCommand { get; }
        public ICommand UninstallMavenSystemCommand { get; }
        public ICommand RefreshMavenStatusCommand { get; }
        public ICommand LoadMavenDownloadMirrorsCommand { get; }
        public ICommand AddMavenDownloadMirrorCommand { get; }
        public ICommand EditMavenDownloadMirrorCommand { get; }
        public ICommand CancelEditMavenMirrorCommand { get; }
        public ICommand RemoveMavenDownloadMirrorCommand { get; }
        public ICommand ToggleMavenDownloadMirrorCommand { get; }
        public ICommand TestMavenDownloadMirrorLatencyCommand { get; }
        public ICommand CopyPathCommand { get; }
        public ICommand OpenPathCommand { get; }
        public ICommand DetectMavenMirrorCommand { get; }
        public ICommand SetMavenMirrorCommand { get; }
        public ICommand ApplyCustomMavenMirrorCommand { get; }

        public MavenDetailViewModel(IMavenService mavenService, IDialogService dialogService, IPackageManagerMirrorService packageManagerMirrorService, ILanguageService languageService)
        {
            _mavenService = mavenService;
            _dialogService = dialogService;
            _packageManagerMirrorService = packageManagerMirrorService;
            _languageService = languageService;
            _mavenVersions = new ObservableCollection<MavenVersionItem>();
            _mavenDownloadMirrors = new ObservableCollection<MavenDownloadMirror>();
            _presetMavenMirrors = new ObservableCollection<PresetMirrorItem>();

            LoadMavenVersionsCommand = new RelayCommand(async () => await LoadMavenVersionsAsync(), () => !IsMavenLoading);
            InstallMavenCommand = new RelayCommand<MavenVersionItem>(async item => await InstallMavenAsync(item, false), item => item != null && !IsMavenInstalling);
            InstallMavenSystemCommand = new RelayCommand<MavenVersionItem>(async item => await InstallMavenAsync(item, true), item => item != null && !IsMavenInstalling);
            UninstallMavenUserCommand = new RelayCommand<MavenVersionItem>(async _ => await UninstallMavenAsync(false), _ => HasMavenUserLevel && !IsMavenInstalling);
            UninstallMavenSystemCommand = new RelayCommand<MavenVersionItem>(async _ => await UninstallMavenAsync(true), _ => HasMavenSystemLevel && !IsMavenInstalling);
            RefreshMavenStatusCommand = new RelayCommand(async () => await RefreshMavenStatusAsync(), () => !IsMavenInstalling);
            LoadMavenDownloadMirrorsCommand = new RelayCommand(async () => await LoadMavenDownloadMirrorsAsync());
            AddMavenDownloadMirrorCommand = new RelayCommand(async () => await AddMavenDownloadMirrorAsync(), () => !string.IsNullOrEmpty(NewMavenMirrorName) && !string.IsNullOrEmpty(NewMavenMirrorUrl));
            EditMavenDownloadMirrorCommand = new RelayCommand<MavenDownloadMirror>(m => ShowEditMavenMirrorMode(m), m => m != null);
            CancelEditMavenMirrorCommand = new RelayCommand(() => CancelEditMavenMirror());
            RemoveMavenDownloadMirrorCommand = new RelayCommand<MavenDownloadMirror>(async m => await RemoveMavenDownloadMirrorAsync(m), m => m != null && !m.IsDefault);
            ToggleMavenDownloadMirrorCommand = new RelayCommand<MavenDownloadMirror>(async m => await ToggleMavenDownloadMirrorAsync(m), m => m != null);
            TestMavenDownloadMirrorLatencyCommand = new RelayCommand(async () => await TestMavenDownloadMirrorLatencyAsync(), () => MavenDownloadMirrors?.Count > 0);
            CopyPathCommand = new RelayCommand<string>(path => CopyPath(path));
            OpenPathCommand = new RelayCommand<string>(path => OpenPath(path));
            DetectMavenMirrorCommand = new RelayCommand(async () => await DetectMavenMirrorAsync());
            SetMavenMirrorCommand = new RelayCommand<PresetMirrorItem>(async m => await SetMavenMirrorAsync(m?.Url), m => m != null);
            ApplyCustomMavenMirrorCommand = new RelayCommand(async () => await SetMavenMirrorAsync(CustomMavenMirrorUrl), () => !string.IsNullOrEmpty(CustomMavenMirrorUrl));
            BrowseLocalRepoCommand = new RelayCommand(BrowseLocalRepo);
            ApplyLocalRepoCommand = new RelayCommand(ApplyLocalRepo);

            WeakMessenger.Register<SdkStatusChangedMessage>(this, OnSdkStatusChanged);

            _languageService.LanguageChanged += OnLanguageChanged;
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            OnPropertyChanged(nameof(MavenUserVersionDisplay));
            OnPropertyChanged(nameof(MavenSystemVersionDisplay));
            OnPropertyChanged(nameof(HasMavenUserLevel));
            OnPropertyChanged(nameof(HasMavenSystemLevel));
            OnPropertyChanged(nameof(MavenMirrorSaveButtonText));
            OnPropertyChanged(nameof(MavenInstallStepsText));
            OnPropertyChanged(nameof(MavenUninstallStepsText));
            OnPropertyChanged(nameof(MavenCommonCommandsText));
            OnPropertyChanged(nameof(MavenDepMirrorSetupText));
            OnPropertyChanged(nameof(MavenDepMirrorDescText));
            OnPropertyChanged(nameof(IsLocalRepoDefault));
        }

        private void OnSdkStatusChanged(SdkStatusChangedMessage msg)
        {
            if (msg.Language == "Maven")
            {
                _ = RefreshMavenStatusAsync();
            }
        }

        public void OnNavigatedTo()
        {
            _ = RefreshMavenStatusAsync();
            _ = LoadMavenDownloadMirrorsAsync();
            _ = LoadMavenMirrorDataAsync();
            RefreshLocalRepo();
            if (_allMavenVersions == null || _allMavenVersions.Count == 0)
            {
                _ = LoadMavenVersionsAsync();
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

        #region Maven 管理

        private async Task LoadMavenVersionsAsync()
        {
            IsMavenLoading = true;
            try
            {
                var versions = await _mavenService.GetAvailableVersionsAsync();
                var detection = await _mavenService.DetectMavenAsync();

                var activeVersion = detection.IsUserLevelInstalled ? detection.UserLevelVersion
                    : detection.IsSystemLevelInstalled ? detection.SystemLevelVersion
                    : null;

                var items = new List<MavenVersionItem>();
                foreach (var v in versions)
                {
                    var hasCache = await _mavenService.HasCacheAsync(v.Version);
                    var isUser = detection.IsUserLevelInstalled && detection.UserLevelVersion == v.Version;
                    var isSystem = detection.IsSystemLevelInstalled && detection.SystemLevelVersion == v.Version;
                    items.Add(new MavenVersionItem
                    {
                        Version = v.Version,
                        DownloadUrl = v.DownloadUrl,
                        HasCache = hasCache,
                        IsUserLevelInstalled = isUser,
                        IsSystemLevelInstalled = isSystem,
                        IsActive = activeVersion == v.Version
                    });
                }
                AllMavenVersions = items;
            }
            finally
            {
                IsMavenLoading = false;
            }
        }

        private async Task RefreshMavenStatusAsync()
        {
            try
            {
                var detection = await _mavenService.DetectMavenAsync();
                MavenUserVersion = detection.IsUserLevelInstalled ? (detection.UserLevelVersion ?? _languageService.GetString("Common_Installed")) : NOT_INSTALLED_MARKER;
                MavenUserPath = detection.UserLevelPath ?? "";
                MavenSystemVersion = detection.IsSystemLevelInstalled ? (detection.SystemLevelVersion ?? _languageService.GetString("Common_Installed")) : NOT_INSTALLED_MARKER;
                MavenSystemPath = detection.SystemLevelPath ?? "";
            }
            catch
            {
                MavenUserVersion = NOT_INSTALLED_MARKER;
                MavenSystemVersion = NOT_INSTALLED_MARKER;
            }
        }

        private async Task InstallMavenAsync(MavenVersionItem item, bool systemLevel)
        {
            if (item == null) return;

            if (systemLevel && !EnvironmentManager.IsRunningAsAdmin())
            {
                await _dialogService.ShowErrorAsync(
                    _languageService.GetString("Dialog_InsufficientPermission"),
                    _languageService.GetString("Dialog_SystemInstallNeedAdmin"));
                return;
            }

            var levelText = systemLevel ? _languageService.GetString("Common_SystemLevel") : _languageService.GetString("Common_UserLevel");
            var confirmMsg = string.Format(_languageService.GetString("Dialog_InstallConfirmMsg"), levelText, "Maven", item.Version);
            var confirm = await _dialogService.ShowConfirmAsync(
                _languageService.GetString("Dialog_InstallConfirm"),
                confirmMsg);
            if (!confirm) return;

            IsMavenInstalling = true;
            MavenProgress = 0;
            MavenProgressMessage = _languageService.GetString("Dialog_PreparingInstall");

            var taskId = $"Maven_{item.Version}";

            // 通知下载管理器：安装开始
            WeakMessenger.Send(new InstallStartedMessage { Language = "Maven", Version = item.Version });

            try
            {
                var progress = new Progress<InstallProgress>(p =>
                {
                    MavenProgress = p.Percent;
                    MavenProgressMessage = p.Message;

                    // 通知下载管理器：下载进度
                    if (p.Percent < 60)
                    {
                        WeakMessenger.Send(new DownloadProgressMessage
                        {
                            TaskId = taskId,
                            Progress = p.Percent,
                            Speed = 0
                        });
                    }
                });

                await _mavenService.InstallMavenAsync(item.Version, item.DownloadUrl, progress, systemLevel);

                await RefreshMavenStatusAsync();
                await LoadMavenVersionsAsync();
                await DetectMavenMirrorAsync();

                // 通知下载管理器：安装完成
                WeakMessenger.Send(new InstallCompletedMessage
                {
                    TaskId = taskId,
                    Language = "Maven",
                    Version = item.Version,
                    IsSuccess = true,
                    SystemLevel = systemLevel
                });

                // 通知仪表盘刷新
                WeakMessenger.Send(new SdkStatusChangedMessage { Language = "Maven", Action = "Install" });

                var successMsg = string.Format(_languageService.GetString("Dialog_InstallSuccessMsg"), "Maven", item.Version);
                await _dialogService.ShowInfoAsync(
                    _languageService.GetString("Dialog_InstallSuccess"),
                    successMsg);
            }
            catch (Exception ex)
            {
                // 通知下载管理器：安装失败
                WeakMessenger.Send(new InstallCompletedMessage
                {
                    TaskId = taskId,
                    Language = "Maven",
                    Version = item.Version,
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                });

                await _dialogService.ShowErrorAsync(_languageService.GetString("Dialog_InstallFailed"), ex.Message);
            }
            finally
            {
                IsMavenInstalling = false;
                MavenProgress = 0;
                MavenProgressMessage = "";
            }
        }

        private async Task UninstallMavenAsync(bool systemLevel)
        {
            var levelText = systemLevel ? _languageService.GetString("Common_SystemLevel") : _languageService.GetString("Common_UserLevel");

            if (systemLevel && !EnvironmentManager.IsRunningAsAdmin())
            {
                await _dialogService.ShowErrorAsync(
                    _languageService.GetString("Dialog_InsufficientPermission"),
                    _languageService.GetString("Dialog_SystemUninstallNeedAdmin"));
                return;
            }

            var confirmMsg = string.Format(_languageService.GetString("Dialog_UninstallConfirmMsg"), levelText, "Maven", "", levelText);
            var confirm = await _dialogService.ShowConfirmAsync(
                _languageService.GetString("Dialog_UninstallConfirm"),
                confirmMsg);
            if (!confirm) return;

            IsMavenInstalling = true;
            MavenProgressMessage = string.Format(_languageService.GetString("Dialog_Uninstalling"), "Maven", "", levelText);

            try
            {
                await _mavenService.UninstallMavenAsync(systemLevel);
                await RefreshMavenStatusAsync();
                await DetectMavenMirrorAsync();

                // 通知仪表盘刷新
                WeakMessenger.Send(new SdkStatusChangedMessage { Language = "Maven", Action = "Uninstall" });

                var successMsg = string.Format(_languageService.GetString("Dialog_UninstallSuccessMsg"), "Maven", "", levelText);
                await _dialogService.ShowInfoAsync(
                    _languageService.GetString("Dialog_UninstallSuccess"),
                    successMsg);
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync(_languageService.GetString("Dialog_UninstallFailed"), ex.Message);
            }
            finally
            {
                IsMavenInstalling = false;
                MavenProgressMessage = "";
            }
        }

        #endregion

        #region Maven 下载镜像源管理

        private async Task LoadMavenDownloadMirrorsAsync()
        {
            var mirrors = await _mavenService.GetDownloadMirrorsAsync();
            MavenDownloadMirrors = new ObservableCollection<MavenDownloadMirror>(mirrors);
        }

        private async Task AddMavenDownloadMirrorAsync()
        {
            if (string.IsNullOrEmpty(NewMavenMirrorName) || string.IsNullOrEmpty(NewMavenMirrorUrl)) return;

            if (IsEditingMavenMirror && !string.IsNullOrEmpty(_editingMavenMirrorId))
            {
                // 编辑模式：更新已有镜像
                var mirror = new MavenDownloadMirror
                {
                    Id = _editingMavenMirrorId,
                    Name = NewMavenMirrorName,
                    BaseUrl = NewMavenMirrorUrl.TrimEnd('/'),
                    IsEnabled = _editingMavenMirrorIsEnabled,
                    IsDefault = false,
                    IsPreset = _editingMavenMirrorIsPreset
                };
                await _mavenService.UpdateDownloadMirrorAsync(mirror);
                IsEditingMavenMirror = false;
                _editingMavenMirrorId = null;
            }
            else
            {
                // 添加模式：新增镜像
                var mirror = new MavenDownloadMirror
                {
                    Id = $"custom-{Guid.NewGuid():N}".Substring(0, 16),
                    Name = NewMavenMirrorName,
                    BaseUrl = NewMavenMirrorUrl.TrimEnd('/'),
                    IsEnabled = true,
                    IsDefault = false
                };
                await _mavenService.AddDownloadMirrorAsync(mirror);
            }

            NewMavenMirrorName = "";
            NewMavenMirrorUrl = "";
            await LoadMavenDownloadMirrorsAsync();
        }

        private void ShowEditMavenMirrorMode(MavenDownloadMirror mirror)
        {
            if (mirror == null) return;
            _editingMavenMirrorId = mirror.Id;
            _editingMavenMirrorIsPreset = mirror.IsPreset;
            _editingMavenMirrorIsEnabled = mirror.IsEnabled;
            IsEditingMavenMirror = true;
            NewMavenMirrorName = mirror.Name;
            NewMavenMirrorUrl = mirror.BaseUrl;
        }

        private void CancelEditMavenMirror()
        {
            IsEditingMavenMirror = false;
            _editingMavenMirrorId = null;
            NewMavenMirrorName = "";
            NewMavenMirrorUrl = "";
        }

        private async Task RemoveMavenDownloadMirrorAsync(MavenDownloadMirror mirror)
        {
            if (mirror == null || mirror.IsDefault) return;
            await _mavenService.RemoveDownloadMirrorAsync(mirror.Id);
            await LoadMavenDownloadMirrorsAsync();
        }

        private async Task ToggleMavenDownloadMirrorAsync(MavenDownloadMirror mirror)
        {
            if (mirror == null) return;
            mirror.IsEnabled = !mirror.IsEnabled;
            await _mavenService.UpdateDownloadMirrorAsync(mirror);
            await LoadMavenDownloadMirrorsAsync();
        }

        private async Task TestMavenDownloadMirrorLatencyAsync()
        {
            if (MavenDownloadMirrors == null || MavenDownloadMirrors.Count == 0) return;
            foreach (var mirror in MavenDownloadMirrors.ToList())
            {
                try
                {
                    await _mavenService.TestDownloadMirrorLatencyAsync(mirror);
                }
                catch { }
            }
            await LoadMavenDownloadMirrorsAsync();
        }

        #endregion

        #region Maven 包镜像源管理（settings.xml）

        private async Task LoadMavenMirrorDataAsync()
        {
            var presets = _packageManagerMirrorService.GetPresetMirrors(SdkLanguage.Java);
            PresetMavenMirrors = new ObservableCollection<PresetMirrorItem>(presets);
            await DetectMavenMirrorAsync();
        }

        private async Task DetectMavenMirrorAsync()
        {
            try
            {
                CurrentMavenMirror = _packageManagerMirrorService.GetCachedMirror(SdkLanguage.Java)
                    ?? await _packageManagerMirrorService.GetCurrentMirrorAsync(SdkLanguage.Java);
            }
            catch
            {
                CurrentMavenMirror = _languageService.GetString("Common_NotDetected");
            }
            RefreshLocalRepo();
        }

        private async Task SetMavenMirrorAsync(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            try
            {
                var success = await _packageManagerMirrorService.SetMirrorAsync(SdkLanguage.Java, url);
                if (success)
                {
                    await DetectMavenMirrorAsync();
                    CustomMavenMirrorUrl = "";
                }
                else
                {
                    await _dialogService.ShowErrorAsync(
                        _languageService.GetString("Dialog_MirrorSetFailed"),
                        _languageService.GetString("Dialog_MirrorSetFailedMsg"));
                }
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync(_languageService.GetString("Dialog_MirrorSetFailed"), ex.Message);
            }
        }

        #endregion

        #region Maven 本地仓库管理

        private void BrowseLocalRepo()
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = _languageService.GetString("Maven_SelectRepoPath");
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    NewLocalRepoPath = dialog.SelectedPath;
                }
            }
        }

        private void ApplyLocalRepo()
        {
            if (string.IsNullOrWhiteSpace(NewLocalRepoPath)) return;

            var success = _packageManagerMirrorService.SetMavenLocalRepository(NewLocalRepoPath);
            if (success)
            {
                RefreshLocalRepo();
                _dialogService.ShowInfoAsync(
                    _languageService.GetString("Maven_RepoSetSuccess"),
                    string.Format(_languageService.GetString("Maven_RepoSetSuccessMsg"), NewLocalRepoPath)
                );
            }
            else
            {
                _dialogService.ShowInfoAsync(
                    _languageService.GetString("Maven_RepoSetFailed"),
                    _languageService.GetString("Maven_RepoSetFailedMsg")
                );
            }
        }

        public void RefreshLocalRepo()
        {
            var localRepo = _packageManagerMirrorService.GetMavenLocalRepository();
            CurrentLocalRepo = localRepo;

            // Check if it's the default path
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var defaultPath = System.IO.Path.Combine(userProfile, ".m2", "repository");
            IsLocalRepoDefault = string.Equals(localRepo, defaultPath, StringComparison.OrdinalIgnoreCase);
        }

        #endregion
    }
}
