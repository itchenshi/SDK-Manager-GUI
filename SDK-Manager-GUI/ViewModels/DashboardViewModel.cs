using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using SDK_Manager_GUI.Models;
using SDK_Manager_GUI.Services;

namespace SDK_Manager_GUI.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        private ObservableCollection<SdkStatusItem> _sdkStatusItems;
        public ObservableCollection<SdkStatusItem> SdkStatusItems
        {
            get => _sdkStatusItems;
            set => SetProperty(ref _sdkStatusItems, value);
        }

        private bool _isRefreshing;
        public bool IsRefreshing
        {
            get => _isRefreshing;
            set => SetProperty(ref _isRefreshing, value);
        }

        private readonly ISdkManagerService _sdkManagerService;
        private readonly INavigationService _navigationService;
        private readonly IPackageManagerMirrorService _packageManagerMirrorService;
        private readonly IMavenService _mavenService;
        private readonly ILanguageService _languageService;

        public ICommand NavigateToSdkDetailCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand CopyPathCommand { get; }
        public ICommand OpenPathCommand { get; }

        public DashboardViewModel(ISdkManagerService sdkManagerService, INavigationService navigationService, IPackageManagerMirrorService packageManagerMirrorService, IMavenService mavenService, ILanguageService languageService)
        {
            _sdkManagerService = sdkManagerService;
            _navigationService = navigationService;
            _packageManagerMirrorService = packageManagerMirrorService;
            _mavenService = mavenService;
            _languageService = languageService;
            _sdkStatusItems = new ObservableCollection<SdkStatusItem>();

            NavigateToSdkDetailCommand = new RelayCommand<string>(language => NavigateToSdkDetail(language));
            RefreshCommand = new RelayCommand(async () => await RefreshAsync(), () => !IsRefreshing);
            CopyPathCommand = new RelayCommand<string>(path => { if (!string.IsNullOrEmpty(path)) Clipboard.SetText(path); });
            OpenPathCommand = new RelayCommand<string>(path =>
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
            });

            // 监听全局 SDK 状态变更消息，增量同步
            WeakMessenger.Register<SdkStatusChangedMessage>(this, OnSdkStatusChanged);

            // 监听语言变更，刷新显示文本
            _languageService.LanguageChanged += OnLanguageChanged;
        }

        /// <summary>
        /// 首次加载时自动刷新
        /// </summary>
        public async Task InitializeAsync()
        {
            await RefreshAsync();
        }

        private async void OnSdkStatusChanged(SdkStatusChangedMessage msg)
        {
            // 增量更新对应语言的状态，而非全量刷新
            var existing = SdkStatusItems.FirstOrDefault(s => s.Language == msg.Language);
            if (existing != null)
            {
                await UpdateSingleStatusAsync(existing, msg.Language);
            }
            // 同时更新 Maven 卡片
            var mavenItem = SdkStatusItems.FirstOrDefault(s => s.Language == "Maven");
            if (mavenItem != null)
            {
                await UpdateMavenStatusAsync(mavenItem);
            }
        }

        private async Task UpdateSingleStatusAsync(SdkStatusItem item, string language)
        {
            try
            {
                if (!Enum.TryParse<SdkLanguage>(language, out var sdkLanguage)) return;

                var detection = await _sdkManagerService.DetectSdkAsync(sdkLanguage);
                var active = await _sdkManagerService.GetActiveVersionAsync(sdkLanguage);

                item.CurrentVersion = active?.Version ?? _languageService.GetString("Common_NotInstalled");
                item.IsActive = detection.IsInstalled;
                item.InstallPath = detection.InstallPath ?? "";
                item.IsInstalled = detection.IsInstalled;
                item.IsManaged = detection.IsManaged;
                item.DetectedVersion = detection.DetectedVersion;

                // 同步用户级/系统级信息
                item.UserLevelVersion = !string.IsNullOrEmpty(detection.UserLevelVersion) ? detection.UserLevelVersion : _languageService.GetString("Common_NotInstalled");
                item.UserLevelPath = detection.UserLevelPath ?? "";
                item.SystemLevelVersion = !string.IsNullOrEmpty(detection.SystemLevelVersion) ? detection.SystemLevelVersion : _languageService.GetString("Common_NotInstalled");
                item.SystemLevelPath = detection.SystemLevelPath ?? "";

                // 优先从缓存读取镜像源信息
                item.PackageManagerMirror = _packageManagerMirrorService.GetCachedMirror(sdkLanguage) ?? _languageService.GetString("Common_NotDetected");
            }
            catch { }
        }

        private async Task UpdateMavenStatusAsync(SdkStatusItem item)
        {
            try
            {
                var mavenDetection = await _mavenService.DetectMavenAsync();
                var mavenVer = mavenDetection.IsUserLevelInstalled ? mavenDetection.UserLevelVersion
                    : mavenDetection.IsSystemLevelInstalled ? mavenDetection.SystemLevelVersion
                    : null;
                var isInstalled = mavenDetection.IsUserLevelInstalled || mavenDetection.IsSystemLevelInstalled;

                item.CurrentVersion = !string.IsNullOrEmpty(mavenVer) ? mavenVer : _languageService.GetString("Common_NotInstalled");
                item.IsActive = isInstalled;
                item.IsInstalled = isInstalled;
                item.IsManaged = isInstalled;
                item.DetectedVersion = mavenVer;
                item.UserLevelVersion = mavenDetection.IsUserLevelInstalled ? mavenDetection.UserLevelVersion : _languageService.GetString("Common_NotInstalled");
                item.UserLevelPath = mavenDetection.UserLevelPath ?? "";
                item.SystemLevelVersion = mavenDetection.IsSystemLevelInstalled ? mavenDetection.SystemLevelVersion : _languageService.GetString("Common_NotInstalled");
                item.SystemLevelPath = mavenDetection.SystemLevelPath ?? "";

                // 获取 Maven 镜像源信息（优先从缓存读取）
                item.MavenMirrorUrl = _packageManagerMirrorService.GetCachedMirror(SdkLanguage.Java) ?? _languageService.GetString("Common_NotConfigured");
            }
            catch
            {
                item.CurrentVersion = _languageService.GetString("Common_NotInstalled");
                item.UserLevelVersion = _languageService.GetString("Common_NotInstalled");
                item.SystemLevelVersion = _languageService.GetString("Common_NotInstalled");
                item.MavenMirrorUrl = _languageService.GetString("Common_NotConfigured");
            }
        }

        private async Task RefreshAsync()
        {
            if (IsRefreshing) return;

            IsRefreshing = true;
            try
            {
                // 先预加载镜像源缓存，避免后续重复获取
                await _packageManagerMirrorService.PreloadMirrorsAsync();

                if (SdkStatusItems.Count == 0)
                {
                    // 首次加载：并行检测所有 SDK 状态，提升加载速度
                    var nodeJsTask = _sdkManagerService.DetectSdkAsync(SdkLanguage.NodeJs);
                    var javaTask = _sdkManagerService.DetectSdkAsync(SdkLanguage.Java);
                    var pythonTask = _sdkManagerService.DetectSdkAsync(SdkLanguage.Python);
                    var mavenTask = _mavenService.DetectMavenAsync();

                    await Task.WhenAll(nodeJsTask, javaTask, pythonTask, mavenTask);

                    var nodeJsDetection = await nodeJsTask;
                    var javaDetection = await javaTask;
                    var pythonDetection = await pythonTask;
                    var mavenDetection = await mavenTask;

                    // 并行获取活跃版本
                    var nodeJsActiveTask = _sdkManagerService.GetActiveVersionAsync(SdkLanguage.NodeJs);
                    var javaActiveTask = _sdkManagerService.GetActiveVersionAsync(SdkLanguage.Java);
                    var pythonActiveTask = _sdkManagerService.GetActiveVersionAsync(SdkLanguage.Python);

                    await Task.WhenAll(nodeJsActiveTask, javaActiveTask, pythonActiveTask);

                    var nodeJsActive = await nodeJsActiveTask;
                    var javaActive = await javaActiveTask;
                    var pythonActive = await pythonActiveTask;

                    // 添加 NodeJs 卡片
                    SdkStatusItems.Add(new SdkStatusItem
                    {
                        Language = SdkLanguage.NodeJs.ToString(),
                        CurrentVersion = nodeJsActive?.Version ?? _languageService.GetString("Common_NotInstalled"),
                        IsActive = nodeJsDetection.IsInstalled,
                        InstallPath = nodeJsDetection.InstallPath ?? "",
                        Icon = SdkLanguage.NodeJs.ToString(),
                        IsInstalled = nodeJsDetection.IsInstalled,
                        IsManaged = nodeJsDetection.IsManaged,
                        DetectedVersion = nodeJsDetection.DetectedVersion,
                        UserLevelVersion = !string.IsNullOrEmpty(nodeJsDetection.UserLevelVersion) ? nodeJsDetection.UserLevelVersion : _languageService.GetString("Common_NotInstalled"),
                        UserLevelPath = nodeJsDetection.UserLevelPath ?? "",
                        SystemLevelVersion = !string.IsNullOrEmpty(nodeJsDetection.SystemLevelVersion) ? nodeJsDetection.SystemLevelVersion : _languageService.GetString("Common_NotInstalled"),
                        SystemLevelPath = nodeJsDetection.SystemLevelPath ?? ""
                    });

                    // 添加 Java 卡片
                    SdkStatusItems.Add(new SdkStatusItem
                    {
                        Language = SdkLanguage.Java.ToString(),
                        CurrentVersion = javaActive?.Version ?? _languageService.GetString("Common_NotInstalled"),
                        IsActive = javaDetection.IsInstalled,
                        InstallPath = javaDetection.InstallPath ?? "",
                        Icon = SdkLanguage.Java.ToString(),
                        IsInstalled = javaDetection.IsInstalled,
                        IsManaged = javaDetection.IsManaged,
                        DetectedVersion = javaDetection.DetectedVersion,
                        UserLevelVersion = !string.IsNullOrEmpty(javaDetection.UserLevelVersion) ? javaDetection.UserLevelVersion : _languageService.GetString("Common_NotInstalled"),
                        UserLevelPath = javaDetection.UserLevelPath ?? "",
                        SystemLevelVersion = !string.IsNullOrEmpty(javaDetection.SystemLevelVersion) ? javaDetection.SystemLevelVersion : _languageService.GetString("Common_NotInstalled"),
                        SystemLevelPath = javaDetection.SystemLevelPath ?? ""
                    });

                    // 添加 Maven 卡片
                    var mavenVer = mavenDetection.IsUserLevelInstalled ? mavenDetection.UserLevelVersion
                        : mavenDetection.IsSystemLevelInstalled ? mavenDetection.SystemLevelVersion
                        : null;
                    var isMavenInstalled = mavenDetection.IsUserLevelInstalled || mavenDetection.IsSystemLevelInstalled;
                    SdkStatusItems.Add(new SdkStatusItem
                    {
                        Language = "Maven",
                        Icon = "Maven",
                        InstallPath = "",
                        CurrentVersion = !string.IsNullOrEmpty(mavenVer) ? mavenVer : _languageService.GetString("Common_NotInstalled"),
                        IsActive = isMavenInstalled,
                        IsInstalled = isMavenInstalled,
                        IsManaged = isMavenInstalled,
                        DetectedVersion = mavenVer,
                        UserLevelVersion = mavenDetection.IsUserLevelInstalled ? mavenDetection.UserLevelVersion : _languageService.GetString("Common_NotInstalled"),
                        UserLevelPath = mavenDetection.UserLevelPath ?? "",
                        SystemLevelVersion = mavenDetection.IsSystemLevelInstalled ? mavenDetection.SystemLevelVersion : _languageService.GetString("Common_NotInstalled"),
                        SystemLevelPath = mavenDetection.SystemLevelPath ?? "",
                        MavenMirrorUrl = _packageManagerMirrorService.GetCachedMirror(SdkLanguage.Java) ?? _languageService.GetString("Common_NotConfigured")
                    });

                    // 添加 Python 卡片
                    SdkStatusItems.Add(new SdkStatusItem
                    {
                        Language = SdkLanguage.Python.ToString(),
                        CurrentVersion = pythonActive?.Version ?? _languageService.GetString("Common_NotInstalled"),
                        IsActive = pythonDetection.IsInstalled,
                        InstallPath = pythonDetection.InstallPath ?? "",
                        Icon = SdkLanguage.Python.ToString(),
                        IsInstalled = pythonDetection.IsInstalled,
                        IsManaged = pythonDetection.IsManaged,
                        DetectedVersion = pythonDetection.DetectedVersion,
                        UserLevelVersion = !string.IsNullOrEmpty(pythonDetection.UserLevelVersion) ? pythonDetection.UserLevelVersion : _languageService.GetString("Common_NotInstalled"),
                        UserLevelPath = pythonDetection.UserLevelPath ?? "",
                        SystemLevelVersion = !string.IsNullOrEmpty(pythonDetection.SystemLevelVersion) ? pythonDetection.SystemLevelVersion : _languageService.GetString("Common_NotInstalled"),
                        SystemLevelPath = pythonDetection.SystemLevelPath ?? ""
                    });
                }
                else
                {
                    // 增量更新
                    foreach (var item in SdkStatusItems)
                    {
                        if (item.Language == "Maven")
                        {
                            await UpdateMavenStatusAsync(item);
                        }
                        else
                        {
                            await UpdateSingleStatusAsync(item, item.Language);
                        }
                    }
                }

                // 从缓存更新 UI 显示（镜像源已在开头预加载）
                foreach (var item in SdkStatusItems)
                {
                    if (Enum.TryParse<SdkLanguage>(item.Language, out var sdkLang))
                    {
                        item.PackageManagerMirror = _packageManagerMirrorService.GetCachedMirror(sdkLang) ?? _languageService.GetString("Common_NotDetected");
                    }
                }
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            // 语言变更时刷新所有 SDK 状态显示文本
            foreach (var item in SdkStatusItems)
            {
                if (item.Language == "Maven")
                {
                    UpdateMavenStatusAsync(item).ConfigureAwait(false);
                }
                else
                {
                    UpdateSingleStatusAsync(item, item.Language).ConfigureAwait(false);
                }
            }
        }

        private void NavigateToSdkDetail(string language)
        {
            // 通知主窗口更新导航栏选中状态
            WeakMessenger.Send(new NavigateMessage { Target = language });
            if (language == "Maven")
            {
                _navigationService.NavigateTo<MavenDetailViewModel>();
            }
            else
            {
                _navigationService.NavigateTo<SdkDetailViewModel>(language);
            }
        }
    }
}
