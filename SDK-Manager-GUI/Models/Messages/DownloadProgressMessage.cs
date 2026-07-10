using System;
using SDK_Manager_GUI.ViewModels;

namespace SDK_Manager_GUI.Models
{
    public sealed class DownloadProgressMessage : IMessage
    {
        public string TaskId { get; set; }
        public double Progress { get; set; }
        public long Speed { get; set; }
        public long DownloadedSize { get; set; }
        public long TotalSize { get; set; }
        public TimeSpan RemainingTime { get; set; }
        public string MirrorName { get; set; }
    }
}
