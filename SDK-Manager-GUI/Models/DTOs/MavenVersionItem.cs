using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SDK_Manager_GUI.Models
{
    public class MavenVersionItem : INotifyPropertyChanged
    {
        private string _version;
        public string Version
        {
            get => _version;
            set { _version = value; OnPropertyChanged(); }
        }

        private string _downloadUrl;
        public string DownloadUrl
        {
            get => _downloadUrl;
            set { _downloadUrl = value; OnPropertyChanged(); }
        }

        private bool _hasCache;
        public bool HasCache
        {
            get => _hasCache;
            set { _hasCache = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
        }

        private bool _isUserLevelInstalled;
        public bool IsUserLevelInstalled
        {
            get => _isUserLevelInstalled;
            set { _isUserLevelInstalled = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
        }

        private bool _isSystemLevelInstalled;
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
                if (IsUserLevelInstalled) return "Installed(User)";
                if (IsSystemLevelInstalled) return "Installed(System)";
                if (HasCache) return "Cached";
                return "NotDownloaded";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
