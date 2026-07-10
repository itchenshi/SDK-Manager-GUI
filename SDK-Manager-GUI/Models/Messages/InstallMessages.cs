using System;
using SDK_Manager_GUI.ViewModels;

namespace SDK_Manager_GUI.Models
{
    /// <summary>
    /// 安装任务开始消息
    /// </summary>
    public sealed class InstallStartedMessage : IMessage
    {
        public string TaskId { get; set; }
        public string Language { get; set; }
        public string Version { get; set; }
        public string MirrorName { get; set; }
    }

    /// <summary>
    /// 安装进度消息（由 BackgroundTaskManager 发送，供 SdkDetailViewModel 监听）
    /// </summary>
    public sealed class InstallProgressMessage : IMessage
    {
        public string TaskId { get; set; }
        public string Language { get; set; }
        public string Version { get; set; }
        public double Percent { get; set; }
        public string Message { get; set; }
        public long Speed { get; set; }
        public long DownloadedSize { get; set; }
        public long TotalSize { get; set; }
        public TimeSpan RemainingTime { get; set; }
        public string MirrorName { get; set; }
    }

    /// <summary>
    /// 安装完成消息（由 BackgroundTaskManager 发送，供 SdkDetailViewModel 监听）
    /// </summary>
    public sealed class InstallCompletedMessage : IMessage
    {
        public string TaskId { get; set; }
        public string Language { get; set; }
        public string Version { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public string MirrorName { get; set; }
        public bool SystemLevel { get; set; }
    }
}
