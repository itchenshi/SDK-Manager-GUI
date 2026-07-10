using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using SDK_Manager_GUI.Models;
using SDK_Manager_GUI.ViewModels;

namespace SDK_Manager_GUI.Services
{
    /// <summary>
    /// 全局后台任务管理器，管理 SDK 安装/下载任务的生命周期，
    /// 确保任务在后台运行，不受 UI 页面切换影响。
    /// </summary>
    public interface IBackgroundTaskManager
    {
        /// <summary>
        /// 启动安装任务（后台执行，不阻塞调用方）
        /// </summary>
        void StartInstall(SdkLanguage language, string version, bool systemLevel = false);

        /// <summary>
        /// 取消指定任务
        /// </summary>
        bool CancelTask(string taskId);

        /// <summary>
        /// 暂停指定任务（取消下载但保留缓存，恢复时可利用缓存跳过下载）
        /// </summary>
        bool PauseTask(string taskId);

        /// <summary>
        /// 恢复指定任务（重新启动安装流程，利用缓存跳过下载）
        /// </summary>
        void ResumeTask(SdkLanguage language, string version);

        /// <summary>
        /// 检查指定任务是否正在运行
        /// </summary>
        bool IsTaskRunning(string taskId);

        /// <summary>
        /// 获取指定任务的状态
        /// </summary>
        DownloadStatus GetTaskStatus(string taskId);

        /// <summary>
        /// 获取正在运行的任务数
        /// </summary>
        int RunningTaskCount { get; }

        /// <summary>
        /// 任务状态变更事件
        /// </summary>
        event Action<string, DownloadStatus> TaskStatusChanged;
    }

    public class BackgroundTaskManager : IBackgroundTaskManager
    {
        private readonly ISdkManagerService _sdkManagerService;
        private readonly ILanguageService _languageService;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _runningTasks = new ConcurrentDictionary<string, CancellationTokenSource>();
        private readonly ConcurrentDictionary<string, DownloadStatus> _taskStatuses = new ConcurrentDictionary<string, DownloadStatus>();
        private readonly ConcurrentDictionary<string, string> _taskMirrorNames = new ConcurrentDictionary<string, string>();

        private readonly ConcurrentDictionary<string, bool> _taskSystemLevel = new ConcurrentDictionary<string, bool>();

        // 保留已完成/暂停任务的安装级别，供恢复时使用
        private readonly ConcurrentDictionary<string, bool> _completedTaskSystemLevel = new ConcurrentDictionary<string, bool>();

        public int RunningTaskCount => _runningTasks.Count;
        public event Action<string, DownloadStatus> TaskStatusChanged;

        public BackgroundTaskManager(ISdkManagerService sdkManagerService, ILanguageService languageService)
        {
            _sdkManagerService = sdkManagerService;
            _languageService = languageService;
        }

        public void StartInstall(SdkLanguage language, string version, bool systemLevel = false)
        {
            var taskId = $"{language}_{version}";
            if (_runningTasks.ContainsKey(taskId))
                return; // 任务已在运行

            var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30));
            if (!_runningTasks.TryAdd(taskId, cts))
                return;

            _taskSystemLevel[taskId] = systemLevel;
            SetTaskStatus(taskId, DownloadStatus.Downloading);

            // 通知 UI 安装开始
            WeakMessenger.Send(new InstallStartedMessage
            {
                TaskId = taskId,
                Language = language.ToString(),
                Version = version
            });

            // 后台执行，不阻塞调用方
            _ = RunInstallAsync(language, version, taskId, cts, systemLevel);
        }

        private async Task RunInstallAsync(SdkLanguage language, string version, string taskId, CancellationTokenSource cts, bool systemLevel)
        {
            try
            {
                var progress = new Progress<InstallProgress>(p =>
                {
                    // 跟踪使用的镜像名称
                    if (!string.IsNullOrEmpty(p.MirrorName))
                        _taskMirrorNames[taskId] = p.MirrorName;

                    // 通知 SdkDetailViewModel 更新进度
                    WeakMessenger.Send(new InstallProgressMessage
                    {
                        TaskId = taskId,
                        Language = language.ToString(),
                        Version = version,
                        Percent = p.Percent,
                        Message = p.Message,
                        Speed = p.Speed,
                        DownloadedSize = p.DownloadedSize,
                        TotalSize = p.TotalSize,
                        RemainingTime = p.RemainingTime,
                        MirrorName = p.MirrorName
                    });

                    // 通知 DownloadListViewModel 更新下载进度
                    // 下载阶段进度 0-60%，映射为 0-100%
                    if (p.Percent < 70)
                    {
                        WeakMessenger.Send(new DownloadProgressMessage
                        {
                            TaskId = taskId,
                            Progress = Math.Min(p.Percent / 0.6, 100),
                            Speed = p.Speed,
                            DownloadedSize = p.DownloadedSize,
                            TotalSize = p.TotalSize,
                            RemainingTime = p.RemainingTime,
                            MirrorName = p.MirrorName
                        });
                    }
                    else
                    {
                        // 安装阶段，下载进度固定为 100%
                        WeakMessenger.Send(new DownloadProgressMessage
                        {
                            TaskId = taskId,
                            Progress = 100,
                            Speed = 0,
                            DownloadedSize = p.TotalSize,
                            TotalSize = p.TotalSize,
                            RemainingTime = TimeSpan.Zero,
                            MirrorName = p.MirrorName
                        });
                    }
                });

                await _sdkManagerService.InstallAsync(language, version, progress, cts.Token, systemLevel);

                // 获取使用的镜像名称
                _taskMirrorNames.TryGetValue(taskId, out var mirrorName);

                // 安装成功通知
                WeakMessenger.Send(new InstallCompletedMessage
                {
                    TaskId = taskId,
                    Language = language.ToString(),
                    Version = version,
                    IsSuccess = true,
                    MirrorName = mirrorName ?? "",
                    SystemLevel = systemLevel
                });

                SetTaskStatus(taskId, DownloadStatus.Completed);
            }
            catch (OperationCanceledException)
            {
                // 区分暂停和取消
                var currentStatus = GetTaskStatus(taskId);
                var isPaused = currentStatus == DownloadStatus.Paused;

                WeakMessenger.Send(new InstallCompletedMessage
                {
                    TaskId = taskId,
                    Language = language.ToString(),
                    Version = version,
                    IsSuccess = false,
                    ErrorMessage = isPaused ? _languageService.GetString("Dialog_InstallPaused") : _languageService.GetString("Dialog_InstallCancelled")
                });

                if (!isPaused)
                {
                    SetTaskStatus(taskId, DownloadStatus.Cancelled);
                }
            }
            catch (Exception ex)
            {
                WeakMessenger.Send(new InstallCompletedMessage
                {
                    TaskId = taskId,
                    Language = language.ToString(),
                    Version = version,
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                });

                WeakMessenger.Send(new DownloadCompletedMessage
                {
                    TaskId = taskId,
                    Language = language.ToString(),
                    Version = version
                });

                SetTaskStatus(taskId, DownloadStatus.Failed);
            }
            finally
            {
                // 保留安装级别供恢复时使用
                if (_taskSystemLevel.TryRemove(taskId, out var savedLevel))
                    _completedTaskSystemLevel[taskId] = savedLevel;

                _runningTasks.TryRemove(taskId, out _);
                _taskMirrorNames.TryRemove(taskId, out _);
                cts.Dispose();
            }
        }

        public bool CancelTask(string taskId)
        {
            if (_runningTasks.TryGetValue(taskId, out var cts))
            {
                SetTaskStatus(taskId, DownloadStatus.Cancelled);
                cts.Cancel();
                return true;
            }
            return false;
        }

        public bool PauseTask(string taskId)
        {
            if (_runningTasks.TryGetValue(taskId, out var cts))
            {
                // 标记为暂停状态，然后取消当前任务（保留缓存文件）
                SetTaskStatus(taskId, DownloadStatus.Paused);
                cts.Cancel();
                return true;
            }
            return false;
        }

        public void ResumeTask(SdkLanguage language, string version)
        {
            var taskId = $"{language}_{version}";
            // 如果任务正在运行，不能恢复
            if (_runningTasks.ContainsKey(taskId))
                return;

            // 获取之前的安装级别（优先从已完成记录，其次从运行中记录）
            var wasSystemLevel = (_completedTaskSystemLevel.TryGetValue(taskId, out var sl1) && sl1)
                || (_taskSystemLevel.TryGetValue(taskId, out var sl2) && sl2);

            // 清除旧状态，重新启动安装（InstallAsync 会检查缓存跳过下载）
            _taskStatuses.TryRemove(taskId, out _);
            _completedTaskSystemLevel.TryRemove(taskId, out _);
            StartInstall(language, version, wasSystemLevel);
        }

        public bool IsTaskRunning(string taskId)
        {
            return _runningTasks.ContainsKey(taskId);
        }

        public DownloadStatus GetTaskStatus(string taskId)
        {
            return _taskStatuses.TryGetValue(taskId, out var status) ? status : DownloadStatus.Pending;
        }

        private void SetTaskStatus(string taskId, DownloadStatus status)
        {
            _taskStatuses[taskId] = status;
            TaskStatusChanged?.Invoke(taskId, status);
        }
    }
}
