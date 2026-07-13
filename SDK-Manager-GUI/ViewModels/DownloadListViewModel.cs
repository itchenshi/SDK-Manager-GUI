using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using SDK_Manager_GUI.Models;
using SDK_Manager_GUI.Services;

namespace SDK_Manager_GUI.ViewModels
{
    public class DownloadListViewModel : ViewModelBase
    {
        private ObservableCollection<DownloadTaskItem> _activeTasks;
        public ObservableCollection<DownloadTaskItem> ActiveTasks
        {
            get => _activeTasks;
            set => SetProperty(ref _activeTasks, value);
        }

        private ObservableCollection<DownloadTaskItem> _completedTasks;
        public ObservableCollection<DownloadTaskItem> CompletedTasks
        {
            get => _completedTasks;
            set => SetProperty(ref _completedTasks, value);
        }

        private ObservableCollection<DownloadTaskItem> _allTasks;
        public ObservableCollection<DownloadTaskItem> AllTasks
        {
            get => _allTasks;
            set => SetProperty(ref _allTasks, value);
        }

        private DownloadTaskItem _selectedTask;
        public DownloadTaskItem SelectedTask
        {
            get => _selectedTask;
            set => SetProperty(ref _selectedTask, value);
        }

        private int _activeCount;
        public int ActiveCount
        {
            get => _activeCount;
            set => SetProperty(ref _activeCount, value);
        }

        private long _totalDownloadSpeed;
        public long TotalDownloadSpeed
        {
            get => _totalDownloadSpeed;
            set => SetProperty(ref _totalDownloadSpeed, value);
        }

        private string _totalSpeedDisplay;
        public string TotalSpeedDisplay
        {
            get => _totalSpeedDisplay;
            set => SetProperty(ref _totalSpeedDisplay, value);
        }

        private string _taskSummary;
        public string TaskSummary
        {
            get => _taskSummary;
            set => SetProperty(ref _taskSummary, value);
        }

        private int _successCount;
        public int SuccessCount
        {
            get => _successCount;
            set => SetProperty(ref _successCount, value);
        }

        private int _failCount;
        public int FailCount
        {
            get => _failCount;
            set => SetProperty(ref _failCount, value);
        }

        private bool _hasActiveTasks;
        public bool HasActiveTasks
        {
            get => _hasActiveTasks;
            set => SetProperty(ref _hasActiveTasks, value);
        }

        private bool _hasCompletedTasks;
        public bool HasCompletedTasks
        {
            get => _hasCompletedTasks;
            set => SetProperty(ref _hasCompletedTasks, value);
        }

        private bool _hasAnyTasks;
        public bool HasAnyTasks
        {
            get => _hasAnyTasks;
            set => SetProperty(ref _hasAnyTasks, value);
        }

        private string _statusFilter;
        public string StatusFilter
        {
            get => _statusFilter;
            set { if (SetProperty(ref _statusFilter, value)) RebuildAllTasks(); }
        }

        private ObservableCollection<string> _statusFilterOptions;
        public ObservableCollection<string> StatusFilterOptions
        {
            get => _statusFilterOptions;
            set => SetProperty(ref _statusFilterOptions, value);
        }

        private readonly ISdkManagerService _sdkManagerService;
        private readonly IBackgroundTaskManager _backgroundTaskManager;
        private readonly IDialogService _dialogService;
        private readonly INavigationService _navigationService;
        private readonly ILanguageService _languageService;

        public ICommand PauseDownloadCommand { get; }
        public ICommand ResumeDownloadCommand { get; }
        public ICommand CancelDownloadCommand { get; }
        public ICommand RetryDownloadCommand { get; }
        public ICommand ClearCompletedCommand { get; }
        public ICommand DeleteTaskCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand OpenDirectoryCommand { get; }
        public ICommand OpenDefaultDirectoryCommand { get; }

        public DownloadListViewModel(ISdkManagerService sdkManagerService, IBackgroundTaskManager backgroundTaskManager, IDialogService dialogService, INavigationService navigationService, ILanguageService languageService)
        {
            _sdkManagerService = sdkManagerService;
            _backgroundTaskManager = backgroundTaskManager;
            _dialogService = dialogService;
            _navigationService = navigationService;
            _languageService = languageService;
            _activeTasks = new ObservableCollection<DownloadTaskItem>();
            _completedTasks = new ObservableCollection<DownloadTaskItem>();
            _allTasks = new ObservableCollection<DownloadTaskItem>();

            // 初始化筛选选项（使用本地化文本）
            UpdateFilterOptions();
            _statusFilter = _languageService.GetString("Download_FilterAll");

            PauseDownloadCommand = new RelayCommand<string>(taskId => PauseDownload(taskId));
            ResumeDownloadCommand = new RelayCommand<string>(taskId => ResumeDownload(taskId));
            CancelDownloadCommand = new RelayCommand<string>(taskId => CancelDownload(taskId));
            RetryDownloadCommand = new RelayCommand<string>(taskId => RetryDownload(taskId));
            ClearCompletedCommand = new RelayCommand(async () => await ClearCompletedAsync());
            DeleteTaskCommand = new RelayCommand<DownloadTaskItem>(item => DeleteTask(item));
            RefreshCommand = new RelayCommand(() => LoadFromCache());
            OpenDirectoryCommand = new RelayCommand<DownloadTaskItem>(item => OpenDirectory(item));
            OpenDefaultDirectoryCommand = new RelayCommand(() => OpenDefaultDirectory());

            // 监听下载进度和完成消息
            WeakMessenger.Register<DownloadProgressMessage>(this, OnDownloadProgress);
            WeakMessenger.Register<DownloadCompletedMessage>(this, OnDownloadCompleted);
            WeakMessenger.Register<InstallStartedMessage>(this, OnInstallStarted);
            WeakMessenger.Register<InstallCompletedMessage>(this, OnInstallCompleted);
            WeakMessenger.Register<SdkStatusChangedMessage>(this, m => LoadFromCache());

            // 初始加载缓存中的任务
            LoadFromCache();

            // 监听语言变更
            _languageService.LanguageChanged += OnLanguageChanged;
        }

        private void OnDownloadProgress(DownloadProgressMessage msg)
        {
            var task = ActiveTasks.FirstOrDefault(t => t.TaskId == msg.TaskId);
            if (task != null)
            {
                task.Progress = msg.Progress;
                task.Speed = msg.Speed;
                task.DownloadedSize = msg.DownloadedSize;
                task.TotalSize = msg.TotalSize;
                task.RemainingTime = msg.RemainingTime;
                task.Status = DownloadStatus.Downloading;
                task.StatusDisplay = GetStatusText(DownloadStatus.Downloading);
                task.RemainingTimeDisplay = FormatRemainingTime(msg.RemainingTime);

                if (!string.IsNullOrEmpty(msg.MirrorName) && (task.MirrorName == _languageService.GetString("Download_AutoSelect") || string.IsNullOrEmpty(task.MirrorName)))
                {
                    task.MirrorName = msg.MirrorName;
                }
            }

            UpdateSummary();
        }

        private void OnInstallStarted(InstallStartedMessage msg)
        {
            var taskId = $"{msg.Language}_{msg.Version}";
            if (ActiveTasks.Any(t => t.TaskId == taskId)) return;

            ActiveTasks.Insert(0, new DownloadTaskItem
            {
                TaskId = taskId,
                SdkName = msg.Language,
                Version = msg.Version,
                Status = DownloadStatus.Downloading,
                StatusDisplay = GetStatusText(DownloadStatus.Downloading),
                Progress = 0,
                MirrorName = msg.MirrorName ?? _languageService.GetString("Download_AutoSelect"),
                SaveDirectory = GetCacheDirectory(msg.Language)
            });
            RebuildAllTasks();
            UpdateSummary();
        }

        private void OnInstallCompleted(InstallCompletedMessage msg)
        {
            var taskId = msg.TaskId;

            if (msg.IsSuccess)
            {
                var task = ActiveTasks.FirstOrDefault(t => t.TaskId == taskId);
                if (task != null)
                {
                    ActiveTasks.Remove(task);
                    task.Status = DownloadStatus.Completed;
                    task.StatusDisplay = GetStatusText(DownloadStatus.Completed);
                    task.Progress = 100;
                    task.Speed = 0;
                    task.RemainingTime = TimeSpan.Zero;
                    task.RemainingTimeDisplay = "";
                    CompletedTasks.Insert(0, task);
                }
                else
                {
                    if (!CompletedTasks.Any(t => t.TaskId == taskId))
                    {
                        CompletedTasks.Insert(0, new DownloadTaskItem
                        {
                            TaskId = taskId,
                            SdkName = msg.Language,
                            Version = msg.Version,
                            Status = DownloadStatus.Completed,
                            StatusDisplay = GetStatusText(DownloadStatus.Completed),
                            Progress = 100,
                            MirrorName = msg.MirrorName ?? ""
                        });
                    }
                }
            }
            else if (msg.ErrorMessage != _languageService.GetString("Download_Paused"))
            {
                var task = ActiveTasks.FirstOrDefault(t => t.TaskId == taskId);
                if (task != null)
                {
                    ActiveTasks.Remove(task);
                    task.Status = DownloadStatus.Failed;
                    task.StatusDisplay = GetStatusText(DownloadStatus.Failed);
                    task.ErrorMessage = msg.ErrorMessage;
                    task.Speed = 0;
                    task.RemainingTimeDisplay = "";
                    CompletedTasks.Insert(0, task);
                }
                else
                {
                    if (!CompletedTasks.Any(t => t.TaskId == taskId))
                    {
                        CompletedTasks.Insert(0, new DownloadTaskItem
                        {
                            TaskId = taskId,
                            SdkName = msg.Language,
                            Version = msg.Version,
                            Status = DownloadStatus.Failed,
                            StatusDisplay = GetStatusText(DownloadStatus.Failed),
                            ErrorMessage = msg.ErrorMessage
                        });
                    }
                }
            }

            RebuildAllTasks();
            UpdateSummary();
        }

        private void OnDownloadCompleted(DownloadCompletedMessage msg)
        {
            var task = ActiveTasks.FirstOrDefault(t => t.TaskId == msg.TaskId);
            if (task != null)
            {
                ActiveTasks.Remove(task);
                task.Status = DownloadStatus.Completed;
                task.StatusDisplay = GetStatusText(DownloadStatus.Completed);
                task.Progress = 100;
                task.Speed = 0;
                task.RemainingTime = TimeSpan.Zero;
                task.RemainingTimeDisplay = "";
                CompletedTasks.Insert(0, task);
            }
            RebuildAllTasks();
            UpdateSummary();
        }

        /// <summary>
        /// 添加下载任务到活跃列表
        /// </summary>
        public void AddTask(DownloadTaskItem task)
        {
            ActiveTasks.Insert(0, task);
            RebuildAllTasks();
            UpdateSummary();
        }

        /// <summary>
        /// 从缓存目录加载已完成的下载记录
        /// </summary>
        private async void LoadFromCache()
        {
            try
            {
                var cacheItems = await _sdkManagerService.GetDownloadCacheAsync();
                foreach (var item in cacheItems)
                {
                    var taskId = $"{item.Language}_{item.Version}";
                    if (ActiveTasks.Any(t => t.TaskId == taskId)) continue;
                    if (CompletedTasks.Any(t => t.TaskId == taskId)) continue;

                    CompletedTasks.Add(new DownloadTaskItem
                    {
                        TaskId = taskId,
                        SdkName = item.Language,
                        Version = item.Version,
                        Status = DownloadStatus.Completed,
                        StatusDisplay = GetStatusText(DownloadStatus.Completed),
                        Progress = 100,
                        TotalSize = item.FileSize,
                        MirrorName = _languageService.GetString("Download_LocalCache")
                    });
                }
            }
            catch { }

            RebuildAllTasks();
            UpdateSummary();
        }

        private void PauseDownload(string taskId)
        {
            var task = ActiveTasks.FirstOrDefault(t => t.TaskId == taskId);
            if (task != null && task.Status == DownloadStatus.Downloading)
            {
                if (task.SdkName == "Maven") return;

                _backgroundTaskManager.PauseTask(taskId);
                task.Status = DownloadStatus.Paused;
                task.StatusDisplay = GetStatusText(DownloadStatus.Paused);
                task.Speed = 0;
                task.RemainingTimeDisplay = "";
                RebuildAllTasks();
            }
        }

        private void ResumeDownload(string taskId)
        {
            var task = ActiveTasks.FirstOrDefault(t => t.TaskId == taskId);
            if (task != null && task.Status == DownloadStatus.Paused)
            {
                if (task.SdkName == "Maven")
                {
                    ActiveTasks.Remove(task);
                    task.Status = DownloadStatus.Failed;
                    task.StatusDisplay = GetStatusText(DownloadStatus.Failed);
                    task.ErrorMessage = _languageService.GetString("Download_MavenNoResume");
                    task.Speed = 0;
                    task.RemainingTimeDisplay = "";
                    CompletedTasks.Insert(0, task);
                }
                else if (task.SdkName != null && task.Version != null
                    && Enum.TryParse<SdkLanguage>(task.SdkName, out var language))
                {
                    ActiveTasks.Remove(task);
                    _backgroundTaskManager.ResumeTask(language, task.Version);
                }
                else
                {
                    task.Status = DownloadStatus.Downloading;
                    task.StatusDisplay = GetStatusText(DownloadStatus.Downloading);
                }
                RebuildAllTasks();
            }
        }

        private void CancelDownload(string taskId)
        {
            var task = ActiveTasks.FirstOrDefault(t => t.TaskId == taskId);
            if (task == null) return;

            // Maven 不通过 BackgroundTaskManager 管理，不能调用其 CancelTask
            if (task.SdkName != "Maven")
            {
                _backgroundTaskManager.CancelTask(taskId);
            }

            ActiveTasks.Remove(task);
            task.Status = DownloadStatus.Cancelled;
            task.StatusDisplay = GetStatusText(DownloadStatus.Cancelled);
            task.Speed = 0;
            task.RemainingTimeDisplay = "";
            CompletedTasks.Insert(0, task);
            RebuildAllTasks();
            UpdateSummary();
        }

        private void RetryDownload(string taskId)
        {
            var task = ActiveTasks.FirstOrDefault(t => t.TaskId == taskId)
                       ?? CompletedTasks.FirstOrDefault(t => t.TaskId == taskId);
            if (task == null) return;

            if (task.Status != DownloadStatus.Failed && task.Status != DownloadStatus.Cancelled) return;

            if (task.SdkName == "Maven")
            {
                // Maven 不通过 BackgroundTaskManager 管理，导航到 Maven 管理页面重新安装
                ActiveTasks.Remove(task);
                CompletedTasks.Remove(task);
                RebuildAllTasks();
                _navigationService.NavigateTo<MavenDetailViewModel>();
            }
            else if (task.SdkName != null && task.Version != null
                && Enum.TryParse<SdkLanguage>(task.SdkName, out var language))
            {
                ActiveTasks.Remove(task);
                CompletedTasks.Remove(task);
                RebuildAllTasks();

                _backgroundTaskManager.StartInstall(language, task.Version);
            }
        }

        private async Task ClearCompletedAsync()
        {
            var toRemove = CompletedTasks.Where(t => t.Status == DownloadStatus.Completed || t.Status == DownloadStatus.Cancelled).ToList();
            if (toRemove.Count == 0) return;

            var confirm = await _dialogService.ShowConfirmAsync(_languageService.GetString("Download_ClearConfirm"), _languageService.GetString("Download_ClearConfirmMsg"));
            if (!confirm) return;

            foreach (var task in toRemove)
            {
                DeleteCacheForTask(task);
                CompletedTasks.Remove(task);
            }
            RebuildAllTasks();
            UpdateSummary();
        }

        private void DeleteTask(DownloadTaskItem item)
        {
            if (item == null) return;
            DeleteCacheForTask(item);
            ActiveTasks.Remove(item);
            CompletedTasks.Remove(item);
            RebuildAllTasks();
            UpdateSummary();
        }

        /// <summary>
        /// 删除任务对应的缓存文件
        /// </summary>
        private void DeleteCacheForTask(DownloadTaskItem task)
        {
            try
            {
                if (task.SdkName != null && task.Version != null)
                {
                    var cacheDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", task.SdkName);
                    if (!System.IO.Directory.Exists(cacheDir)) return;

                    if (task.SdkName == "Maven")
                    {
                        // Maven 缓存文件名格式：apache-maven-{version}-bin.zip
                        var cacheFile = System.IO.Path.Combine(cacheDir, $"apache-maven-{task.Version}-bin.zip");
                        if (System.IO.File.Exists(cacheFile))
                            System.IO.File.Delete(cacheFile);
                    }
                    else
                    {
                        // 所有 SDK 统一使用 .zip 格式缓存
                        var cacheFile = System.IO.Path.Combine(cacheDir, $"{task.SdkName}_{task.Version}.zip");
                        if (System.IO.File.Exists(cacheFile))
                            System.IO.File.Delete(cacheFile);
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// 重建统一列表：活跃任务在前，已完成任务在后，应用筛选
        /// </summary>
        private void RebuildAllTasks()
        {
            var active = ActiveTasks.AsEnumerable();
            var completed = CompletedTasks.AsEnumerable();

            // 应用筛选
            var filterDownloading = _languageService.GetString("Download_FilterDownloading");
            var filterPaused = _languageService.GetString("Download_FilterPaused");
            var filterCompleted = _languageService.GetString("Download_FilterCompleted");
            var filterFailed = _languageService.GetString("Download_FilterFailed");
            var filterCancelled = _languageService.GetString("Download_FilterCancelled");

            switch (StatusFilter)
            {
                case string s when s == filterDownloading:
                    active = active.Where(t => t.Status == DownloadStatus.Downloading);
                    completed = completed.Where(t => t.Status == DownloadStatus.Downloading);
                    break;
                case string s when s == filterPaused:
                    active = active.Where(t => t.Status == DownloadStatus.Paused);
                    completed = completed.Where(t => t.Status == DownloadStatus.Paused);
                    break;
                case string s when s == filterCompleted:
                    active = active.Where(t => t.Status == DownloadStatus.Completed);
                    completed = completed.Where(t => t.Status == DownloadStatus.Completed);
                    break;
                case string s when s == filterFailed:
                    active = active.Where(t => t.Status == DownloadStatus.Failed);
                    completed = completed.Where(t => t.Status == DownloadStatus.Failed);
                    break;
                case string s when s == filterCancelled:
                    active = active.Where(t => t.Status == DownloadStatus.Cancelled);
                    completed = completed.Where(t => t.Status == DownloadStatus.Cancelled);
                    break;
            }

            var all = active.Concat(completed).ToList();
            AllTasks = new ObservableCollection<DownloadTaskItem>(all);
            HasCompletedTasks = CompletedTasks.Count > 0;
            HasAnyTasks = AllTasks.Count > 0;
        }

        private void UpdateSummary()
        {
            ActiveCount = ActiveTasks.Count;
            HasActiveTasks = ActiveTasks.Count > 0;
            HasCompletedTasks = CompletedTasks.Count > 0;
            HasAnyTasks = ActiveTasks.Count > 0 || CompletedTasks.Count > 0;
            TotalDownloadSpeed = ActiveTasks.Where(t => t.Status == DownloadStatus.Downloading).Sum(t => t.Speed);
            TotalSpeedDisplay = FormatSpeed(TotalDownloadSpeed);

            SuccessCount = CompletedTasks.Count(t => t.Status == DownloadStatus.Completed);
            FailCount = CompletedTasks.Count(t => t.Status == DownloadStatus.Failed);

            var parts = new System.Collections.Generic.List<string>();
            if (ActiveCount > 0)
                parts.Add($"{_languageService.GetString("Download_Active")} {ActiveCount}");
            if (SuccessCount > 0)
                parts.Add($"{_languageService.GetString("Download_Success")} {SuccessCount}");
            if (FailCount > 0)
                parts.Add($"{_languageService.GetString("Download_Fail")} {FailCount}");
            if (!string.IsNullOrEmpty(TotalSpeedDisplay))
                parts.Add($"{_languageService.GetString("Download_TotalSpeed")} {TotalSpeedDisplay}");

            TaskSummary = parts.Count > 0 ? string.Join("  |  ", parts) : _languageService.GetString("Download_NoTasksShort");
        }

        private static string FormatSpeed(long bytesPerSecond)
        {
            if (bytesPerSecond <= 0) return "";
            if (bytesPerSecond < 1024) return $"{bytesPerSecond} B/s";
            if (bytesPerSecond < 1024 * 1024) return $"{bytesPerSecond / 1024.0:F1} KB/s";
            return $"{bytesPerSecond / (1024.0 * 1024):F1} MB/s";
        }

        private static string GetCacheDirectory(string language)
        {
            return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", language ?? "");
        }

        private void OpenDirectory(DownloadTaskItem item)
        {
            if (item == null) return;
            try
            {
                var dir = item.SaveDirectory;
                if (string.IsNullOrEmpty(dir) || !System.IO.Directory.Exists(dir))
                {
                    dir = GetCacheDirectory(item.SdkName);
                }
                if (System.IO.Directory.Exists(dir))
                {
                    System.Diagnostics.Process.Start("explorer.exe", dir);
                }
                else
                {
                    _dialogService.ShowInfoAsync(_languageService.GetString("Download_Tip"), $"{_languageService.GetString("Download_DirNotExist")}: {dir}");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowErrorAsync(_languageService.GetString("Download_OpenDirFailed"), ex.Message);
            }
        }

        private void OpenDefaultDirectory()
        {
            try
            {
                var cacheDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache");
                if (!System.IO.Directory.Exists(cacheDir))
                    System.IO.Directory.CreateDirectory(cacheDir);
                System.Diagnostics.Process.Start("explorer.exe", cacheDir);
            }
            catch (Exception ex)
            {
                _dialogService.ShowErrorAsync(_languageService.GetString("Download_OpenDirFailed"), ex.Message);
            }
        }

        private void UpdateFilterOptions()
        {
            var currentFilter = _statusFilter;
            StatusFilterOptions = new ObservableCollection<string>
            {
                _languageService.GetString("Download_FilterAll"),
                _languageService.GetString("Download_FilterDownloading"),
                _languageService.GetString("Download_FilterPaused"),
                _languageService.GetString("Download_FilterCompleted"),
                _languageService.GetString("Download_FilterFailed"),
                _languageService.GetString("Download_FilterCancelled")
            };
            // 恢复之前选中的筛选（按索引匹配）
            if (!string.IsNullOrEmpty(currentFilter))
            {
                var oldIndex = new[] { "Download_FilterAll", "Download_FilterDownloading", "Download_FilterPaused", "Download_FilterCompleted", "Download_FilterFailed", "Download_FilterCancelled" }
                    .Select(k => _languageService.GetString(k)).ToList().IndexOf(currentFilter);
                if (oldIndex >= 0 && oldIndex < StatusFilterOptions.Count)
                    _statusFilter = StatusFilterOptions[oldIndex];
                else
                    _statusFilter = StatusFilterOptions[0];
            }
            else
            {
                _statusFilter = StatusFilterOptions[0];
            }
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            UpdateFilterOptions();
            UpdateSummary();
            RefreshAllTaskDisplays();
            RebuildAllTasks();
        }

        private void RefreshAllTaskDisplays()
        {
            foreach (var task in ActiveTasks.Concat(CompletedTasks))
            {
                task.StatusDisplay = GetStatusText(task.Status);
                task.RemainingTimeDisplay = task.Status == DownloadStatus.Downloading
                    ? FormatRemainingTime(task.RemainingTime)
                    : "";
            }
        }

        private string GetStatusText(DownloadStatus status)
        {
            switch (status)
            {
                case DownloadStatus.Pending: return _languageService.GetString("Status_Pending");
                case DownloadStatus.Downloading: return _languageService.GetString("Status_Downloading");
                case DownloadStatus.Paused: return _languageService.GetString("Status_Paused");
                case DownloadStatus.Completed: return _languageService.GetString("Status_Completed");
                case DownloadStatus.Failed: return _languageService.GetString("Status_Failed");
                case DownloadStatus.Cancelled: return _languageService.GetString("Status_Cancelled");
                default: return status.ToString();
            }
        }

        private string FormatRemainingTime(TimeSpan rt)
        {
            if (rt <= TimeSpan.Zero) return "";
            var sec = _languageService.GetString("Common_Seconds");
            var min = _languageService.GetString("Common_Minutes");
            var hour = _languageService.GetString("Common_Hours");
            if (rt.TotalMinutes < 1) return $"{rt.Seconds}{sec}";
            if (rt.TotalHours < 1) return $"{rt.Minutes}{min}{rt.Seconds}{sec}";
            return $"{(int)rt.TotalHours}{hour}{rt.Minutes}{min}";
        }
    }
}
