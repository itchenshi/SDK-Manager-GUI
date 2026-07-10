using System;

namespace SDK_Manager_GUI.Models
{
    public class DownloadTask
    {
        public string TaskId { get; set; }
        public SdkLanguage Language { get; set; }
        public string SdkName { get; set; }
        public string Version { get; set; }
        public string DownloadUrl { get; set; }
        public string TargetPath { get; set; }
        public DownloadStatus Status { get; set; }
        public double Progress { get; set; }
        public long DownloadedSize { get; set; }
        public long TotalSize { get; set; }
        public long Speed { get; set; }
        public string MirrorName { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? CompleteTime { get; set; }
        public string ErrorMessage { get; set; }
    }
}
