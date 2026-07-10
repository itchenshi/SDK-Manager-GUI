using System;

namespace SDK_Manager_GUI.Models
{
    public class InstallProgress
    {
        public double Percent { get; set; }
        public string Message { get; set; }
        public long Speed { get; set; }
        public long DownloadedSize { get; set; }
        public long TotalSize { get; set; }
        public TimeSpan RemainingTime { get; set; }
        public string MirrorName { get; set; }
    }
}
