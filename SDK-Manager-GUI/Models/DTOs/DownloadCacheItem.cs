using System;

namespace SDK_Manager_GUI.Models
{
    public class DownloadCacheItem
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
        public string Language { get; set; }
        public string Version { get; set; }
        public DateTime CreateTime { get; set; }
        public string FileSizeDisplay
        {
            get
            {
                if (FileSize < 1024) return $"{FileSize} B";
                if (FileSize < 1024 * 1024) return $"{FileSize / 1024.0:F1} KB";
                if (FileSize < 1024 * 1024 * 1024) return $"{FileSize / (1024.0 * 1024):F1} MB";
                return $"{FileSize / (1024.0 * 1024 * 1024):F2} GB";
            }
        }
    }
}
