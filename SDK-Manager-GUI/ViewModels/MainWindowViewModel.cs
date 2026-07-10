using System.Collections.ObjectModel;
using System.Windows.Input;
using SDK_Manager_GUI.Models;
using SDK_Manager_GUI.Services;

namespace SDK_Manager_GUI.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private ViewModelBase _currentPage;
        public ViewModelBase CurrentPage
        {
            get => _currentPage;
            set => SetProperty(ref _currentPage, value);
        }

        private string _statusBarText;
        public string StatusBarText
        {
            get => _statusBarText;
            set => SetProperty(ref _statusBarText, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private NavigationItem _selectedNavigationItem;
        public NavigationItem SelectedNavigationItem
        {
            get => _selectedNavigationItem;
            set
            {
                if (SetProperty(ref _selectedNavigationItem, value) && value != null)
                {
                    NavigateByKey(value.Key);
                }
            }
        }

        private readonly INavigationService _navigationService;
        private readonly DownloadListViewModel _downloadListViewModel; // 预先创建，确保消息监听在启动时注册
        private readonly ILanguageService _languageService;

        public bool HasActiveDownloads => _downloadListViewModel?.HasActiveTasks ?? false;

        public ObservableCollection<NavigationItem> NavigationItems { get; }

        public ICommand NavigateCommand { get; }

        public MainWindowViewModel(INavigationService navigationService, DownloadListViewModel downloadListViewModel, ILanguageService languageService)
        {
            _navigationService = navigationService;
            _downloadListViewModel = downloadListViewModel; // 预创建，注册 WeakMessenger 消息监听
            _languageService = languageService;

            NavigationItems = new ObservableCollection<NavigationItem>
            {
                new NavigationItem { Key = "Dashboard", DisplayName = _languageService.GetString("Nav_Dashboard"), Icon = "Home" },
                new NavigationItem { Key = "NodeJs", DisplayName = "Node.js", Icon = "NodeJs" },
                new NavigationItem { Key = "Java", DisplayName = "Java", Icon = "Java" },
                new NavigationItem { Key = "Maven", DisplayName = "Maven", Icon = "Maven" },
                new NavigationItem { Key = "Python", DisplayName = "Python", Icon = "Python" },
                new NavigationItem { Key = "Downloads", DisplayName = _languageService.GetString("Nav_Downloads"), Icon = "Download" },
                new NavigationItem { Key = "Logs", DisplayName = _languageService.GetString("Nav_Logs"), Icon = "Log" },
                new NavigationItem { Key = "Settings", DisplayName = _languageService.GetString("Nav_Settings"), Icon = "Settings" },
                new NavigationItem { Key = "About", DisplayName = _languageService.GetString("Nav_About"), Icon = "About" },
            };

            NavigateCommand = new RelayCommand<string>(key => NavigateByKey(key));

            _navigationService.CurrentPageChanged += OnCurrentPageChanged;

            _statusBarText = _languageService.GetString("Main_Ready");

            _languageService.LanguageChanged += (s, e) => OnLanguageChanged();

            WeakMessenger.Register<NotificationMessage>(this, m =>
            {
                StatusBarText = $"[{m.Type}] {m.Title}: {m.Content}";
            });

            WeakMessenger.Register<NavigateMessage>(this, m =>
            {
                if (!string.IsNullOrEmpty(m.Target))
                {
                    foreach (var item in NavigationItems)
                        item.IsSelected = item.Key == m.Target;
                }
            });
        }

        private void OnCurrentPageChanged()
        {
            CurrentPage = _navigationService.CurrentPage as ViewModelBase;
        }

        private void OnLanguageChanged()
        {
            StatusBarText = _languageService.GetString("Main_Ready");

            // Update navigation item display names
            var nameMap = new System.Collections.Generic.Dictionary<string, string>
            {
                { "Dashboard", _languageService.GetString("Nav_Dashboard") },
                { "Downloads", _languageService.GetString("Nav_Downloads") },
                { "Logs", _languageService.GetString("Nav_Logs") },
                { "Settings", _languageService.GetString("Nav_Settings") },
                { "About", _languageService.GetString("Nav_About") }
            };

            foreach (var item in NavigationItems)
            {
                if (nameMap.TryGetValue(item.Key, out var name))
                    item.DisplayName = name;
            }
        }

        private void NavigateByKey(string key)
        {
            // 更新导航项选中状态
            foreach (var item in NavigationItems)
                item.IsSelected = item.Key == key;

            switch (key)
            {
                case "Dashboard": NavigateToDashboard(); break;
                case "NodeJs": NavigateToSdkDetail("NodeJs"); break;
                case "Java": NavigateToSdkDetail("Java"); break;
                case "Maven": NavigateToMavenDetail(); break;
                case "Python": NavigateToSdkDetail("Python"); break;
                case "Downloads": NavigateToDownloads(); break;
                case "Logs": NavigateToLogs(); break;
                case "Settings": NavigateToSettings(); break;
                case "About": NavigateToAbout(); break;
            }
        }

        private void NavigateToDashboard() => _navigationService.NavigateTo<DashboardViewModel>();
        private void NavigateToSdkDetail(string language) => _navigationService.NavigateTo<SdkDetailViewModel>(language);
        private void NavigateToMavenDetail() => _navigationService.NavigateTo<MavenDetailViewModel>();
        private void NavigateToDownloads() => _navigationService.NavigateTo<DownloadListViewModel>();
        private void NavigateToLogs() => _navigationService.NavigateTo<LogViewModel>();
        private void NavigateToSettings() => _navigationService.NavigateTo<SettingsViewModel>();
        private void NavigateToAbout() => _navigationService.NavigateTo<AboutViewModel>();
    }
}
