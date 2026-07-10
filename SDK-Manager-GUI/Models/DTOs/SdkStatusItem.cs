using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SDK_Manager_GUI.Models
{
    public class SdkStatusItem : INotifyPropertyChanged
    {
        private string _language;
        public string Language
        {
            get => _language;
            set { _language = value; OnPropertyChanged(); }
        }

        private string _currentVersion;
        public string CurrentVersion
        {
            get => _currentVersion;
            set { _currentVersion = value; OnPropertyChanged(); OnPropertyChanged(nameof(VersionDisplay)); }
        }

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); }
        }

        private string _installPath;
        public string InstallPath
        {
            get => _installPath;
            set { _installPath = value; OnPropertyChanged(); }
        }

        private string _icon;
        public string Icon
        {
            get => _icon;
            set { _icon = value; OnPropertyChanged(); }
        }

        private bool _isInstalled;
        public bool IsInstalled
        {
            get => _isInstalled;
            set { _isInstalled = value; OnPropertyChanged(); OnPropertyChanged(nameof(VersionDisplay)); OnPropertyChanged(nameof(StatusDisplay)); }
        }

        private bool _isManaged;
        public bool IsManaged
        {
            get => _isManaged;
            set { _isManaged = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusDisplay)); }
        }

        private string _detectedVersion;
        public string DetectedVersion
        {
            get => _detectedVersion;
            set { _detectedVersion = value; OnPropertyChanged(); OnPropertyChanged(nameof(VersionDisplay)); }
        }

        // 用户级信息
        private string _userLevelVersion;
        public string UserLevelVersion
        {
            get => _userLevelVersion;
            set { _userLevelVersion = value; OnPropertyChanged(); }
        }

        private string _userLevelPath;
        public string UserLevelPath
        {
            get => _userLevelPath;
            set { _userLevelPath = value; OnPropertyChanged(); }
        }

        // 系统级信息
        private string _systemLevelVersion;
        public string SystemLevelVersion
        {
            get => _systemLevelVersion;
            set { _systemLevelVersion = value; OnPropertyChanged(); }
        }

        private string _systemLevelPath;
        public string SystemLevelPath
        {
            get => _systemLevelPath;
            set { _systemLevelPath = value; OnPropertyChanged(); }
        }

        // 包管理器镜像源
        private string _packageManagerMirror;
        public string PackageManagerMirror
        {
            get => _packageManagerMirror;
            set { _packageManagerMirror = value; OnPropertyChanged(); }
        }

        // Maven 版本信息（仅 Java）
        private string _mavenVersion;
        public string MavenVersion
        {
            get => _mavenVersion;
            set { _mavenVersion = value; OnPropertyChanged(); }
        }

        private string _mavenPath;
        public string MavenPath
        {
            get => _mavenPath;
            set { _mavenPath = value; OnPropertyChanged(); }
        }

        // Maven 镜像源信息（Maven 卡片专用）
        private string _mavenMirrorUrl;
        public string MavenMirrorUrl
        {
            get => _mavenMirrorUrl;
            set { _mavenMirrorUrl = value; OnPropertyChanged(); }
        }

        public string VersionDisplay => IsInstalled ? (DetectedVersion ?? CurrentVersion ?? "N/A") : "N/A";
        public string StatusDisplay => IsManaged ? "Managed" : (IsInstalled ? "System" : "N/A");

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
