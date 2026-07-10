using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SDK_Manager_GUI.Models;

namespace SDK_Manager_GUI.Models
{
    public class DownloadTaskItem : INotifyPropertyChanged
    {
        private string _taskId;
        public string TaskId
        {
            get => _taskId;
            set { _taskId = value; OnPropertyChanged(); }
        }

        private string _sdkName;
        public string SdkName
        {
            get => _sdkName;
            set { _sdkName = value; OnPropertyChanged(); }
        }

        private string _version;
        public string Version
        {
            get => _version;
            set { _version = value; OnPropertyChanged(); }
        }

        private DownloadStatus _status;
        public DownloadStatus Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusDisplay)); }
        }

        private double _progress;
        public double Progress
        {
            get => _progress;
            set { _progress = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProgressDisplay)); }
        }

        private long _speed;
        public long Speed
        {
            get => _speed;
            set { _speed = value; OnPropertyChanged(); OnPropertyChanged(nameof(SpeedDisplay)); }
        }

        private long _downloadedSize;
        public long DownloadedSize
        {
            get => _downloadedSize;
            set { _downloadedSize = value; OnPropertyChanged(); OnPropertyChanged(nameof(DownloadedSizeDisplay)); }
        }

        private long _totalSize;
        public long TotalSize
        {
            get => _totalSize;
            set { _totalSize = value; OnPropertyChanged(); OnPropertyChanged(nameof(TotalSizeDisplay)); }
        }

        private TimeSpan _remainingTime;
        public TimeSpan RemainingTime
        {
            get => _remainingTime;
            set { _remainingTime = value; OnPropertyChanged(); OnPropertyChanged(nameof(RemainingTimeDisplay)); }
        }

        private string _mirrorName;
        public string MirrorName
        {
            get => _mirrorName;
            set { _mirrorName = value; OnPropertyChanged(); }
        }

        private string _errorMessage;
        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        private string _saveDirectory;
        public string SaveDirectory
        {
            get => _saveDirectory;
            set { _saveDirectory = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 是否支持暂停操作（Maven 不支持暂停恢复）
        /// </summary>
        public bool CanPause => SdkName != "Maven";

        public string StatusDisplay
        {
            get
            {
                switch (Status)
                {
                    case DownloadStatus.Pending: return "Pending";
                    case DownloadStatus.Downloading: return "Downloading";
                    case DownloadStatus.Paused: return "Paused";
                    case DownloadStatus.Completed: return "Completed";
                    case DownloadStatus.Failed: return "Failed";
                    case DownloadStatus.Cancelled: return "Cancelled";
                    default: return Status.ToString();
                }
            }
        }

        public string ProgressDisplay => TotalSize > 0
            ? $"{FormatSize(DownloadedSize)} / {FormatSize(TotalSize)} ({Progress:F0}%)"
            : $"{Progress:F0}%";

        public string SpeedDisplay
        {
            get
            {
                if (Speed <= 0) return "";
                if (Speed < 1024) return $"{Speed} B/s";
                if (Speed < 1024 * 1024) return $"{Speed / 1024.0:F1} KB/s";
                return $"{Speed / (1024.0 * 1024):F1} MB/s";
            }
        }

        public string CompletedInfo
        {
            get
            {
                if (Status == DownloadStatus.Completed)
                {
                    var sizeStr = FormatSize(TotalSize);
                    var mirrorStr = string.IsNullOrEmpty(MirrorName) ? "" : $" | {MirrorName}";
                    return string.IsNullOrEmpty(sizeStr) ? $"Completed{mirrorStr}" : $"{sizeStr}{mirrorStr}";
                }
                return "";
            }
        }

        public string TotalSizeDisplay => FormatSize(TotalSize);

        public string DownloadedSizeDisplay => FormatSize(DownloadedSize);

        public string RemainingTimeDisplay
        {
            get
            {
                if (RemainingTime <= TimeSpan.Zero || Speed <= 0) return "";
                if (RemainingTime.TotalMinutes < 1) return $"{RemainingTime.Seconds}秒";
                if (RemainingTime.TotalHours < 1) return $"{RemainingTime.Minutes}分{RemainingTime.Seconds}秒";
                return $"{(int)RemainingTime.TotalHours}时{RemainingTime.Minutes}分";
            }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes <= 0) return "";
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
