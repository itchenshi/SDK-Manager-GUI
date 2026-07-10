using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SDK_Manager_GUI.Models
{
    public class SdkVersionItem : INotifyPropertyChanged
    {
        private string _version;
        public string Version
        {
            get => _version;
            set { _version = value; OnPropertyChanged(); }
        }

        private VersionCategory _category;
        public VersionCategory Category
        {
            get => _category;
            set { _category = value; OnPropertyChanged(); }
        }

        private bool _isInstalled;
        public bool IsInstalled
        {
            get => _isInstalled;
            set { _isInstalled = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
        }

        private bool _isUserLevelInstalled;
        /// <summary>
        /// 是否在用户级 PATH 中
        /// </summary>
        public bool IsUserLevelInstalled
        {
            get => _isUserLevelInstalled;
            set { _isUserLevelInstalled = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
        }

        private bool _isSystemLevelInstalled;
        /// <summary>
        /// 是否在系统级 PATH 中
        /// </summary>
        public bool IsSystemLevelInstalled
        {
            get => _isSystemLevelInstalled;
            set { _isSystemLevelInstalled = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
        }

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
        }

        private bool _hasCache;
        /// <summary>
        /// 是否有本地缓存（已下载但未安装）
        /// </summary>
        public bool HasCache
        {
            get => _hasCache;
            set { _hasCache = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
        }

        private DateTime? _releaseDate;
        public DateTime? ReleaseDate
        {
            get => _releaseDate;
            set { _releaseDate = value; OnPropertyChanged(); }
        }

        private long? _fileSize;
        public long? FileSize
        {
            get => _fileSize;
            set { _fileSize = value; OnPropertyChanged(); OnPropertyChanged(nameof(FileSizeDisplay)); }
        }

        private string _downloadUrl;
        public string DownloadUrl
        {
            get => _downloadUrl;
            set { _downloadUrl = value; OnPropertyChanged(); }
        }

        public string StatusText
        {
            get
            {
                if (IsActive)
                {
                    if (IsUserLevelInstalled && IsSystemLevelInstalled) return "Active(User+System)";
                    if (IsSystemLevelInstalled) return "Active(System)";
                    if (IsUserLevelInstalled) return "Active(User)";
                    return "Active";
                }
                if (IsUserLevelInstalled && IsSystemLevelInstalled) return "Installed(User+System)";
                if (IsSystemLevelInstalled) return "Installed(System)";
                if (IsUserLevelInstalled) return "Installed(User)";
                if (IsInstalled) return "Installed";
                if (HasCache) return "Cached";
                return "NotInstalled";
            }
        }

        public string CategoryDisplay
        {
            get
            {
                switch (Category)
                {
                    case VersionCategory.LTS: return "LTS";
                    case VersionCategory.Current: return "Current";
                    case VersionCategory.PreRelease: return "PreRelease";
                    default: return "";
                }
            }
        }

        public string FileSizeDisplay
        {
            get
            {
                if (!FileSize.HasValue) return "";
                var size = FileSize.Value;
                if (size < 1024) return $"{size} B";
                if (size < 1024 * 1024) return $"{size / 1024.0:F1} KB";
                if (size < 1024 * 1024 * 1024) return $"{size / (1024.0 * 1024):F1} MB";
                return $"{size / (1024.0 * 1024 * 1024):F2} GB";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
