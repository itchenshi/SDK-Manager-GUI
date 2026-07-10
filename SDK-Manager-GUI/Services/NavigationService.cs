using System;
using Microsoft.Extensions.DependencyInjection;
using SDK_Manager_GUI.Models;
using SDK_Manager_GUI.ViewModels;

namespace SDK_Manager_GUI.Services
{
    public interface INavigationService
    {
        ViewModelBase CurrentPage { get; }
        void NavigateTo<TViewModel>() where TViewModel : ViewModelBase;
        void NavigateTo<TViewModel>(object parameter) where TViewModel : ViewModelBase;
        bool CanGoBack { get; }
        void GoBack();
        event Action CurrentPageChanged;
    }

    public class NavigationService : INavigationService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly System.Collections.Generic.Stack<ViewModelBase> _navigationStack = new System.Collections.Generic.Stack<ViewModelBase>();
        private readonly System.Collections.Generic.Dictionary<string, ViewModelBase> _viewModelCache = new System.Collections.Generic.Dictionary<string, ViewModelBase>();
        private ViewModelBase _currentPage;
        private const int MaxStackSize = 20;

        public ViewModelBase CurrentPage => _currentPage;

        public bool CanGoBack => _navigationStack.Count > 1;

        public event Action CurrentPageChanged;

        public NavigationService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
        {
            var cacheKey = typeof(TViewModel).FullName;
            var viewModel = GetOrCreateViewModel<TViewModel>(cacheKey);

            // 自动初始化支持异步初始化的 ViewModel
            if (viewModel is DashboardViewModel dashboard)
            {
                _ = dashboard.InitializeAsync();
            }
            else if (viewModel is SettingsViewModel settings)
            {
                settings.LoadSettingsCommand.Execute(null);
            }
            else if (viewModel is MirrorConfigViewModel mirrors)
            {
                mirrors.LoadMirrorsCommand.Execute(null);
            }
            else if (viewModel is DownloadListViewModel downloads)
            {
                downloads.RefreshCommand.Execute(null);
            }
            else if (viewModel is LogViewModel logs)
            {
                logs.LoadLogsByDate(DateTime.Today);
            }
            else if (viewModel is MavenDetailViewModel mavenDetail)
            {
                mavenDetail.OnNavigatedTo();
            }

            NavigateToViewModel(viewModel);
        }

        public void NavigateTo<TViewModel>(object parameter) where TViewModel : ViewModelBase
        {
            // 为 SdkDetailViewModel 按语言创建独立缓存
            string cacheKey;
            if (typeof(TViewModel) == typeof(SdkDetailViewModel) && parameter is string language)
            {
                cacheKey = $"{typeof(SdkDetailViewModel).FullName}_{language}";
            }
            else
            {
                cacheKey = typeof(TViewModel).FullName;
            }

            var viewModel = GetOrCreateViewModel<TViewModel>(cacheKey);

            if (viewModel is SdkDetailViewModel sdkDetail)
            {
                sdkDetail.OnNavigatedTo(parameter);
            }
            else if (viewModel is MavenDetailViewModel mavenDetail)
            {
                mavenDetail.OnNavigatedTo();
            }

            NavigateToViewModel(viewModel);
        }

        private TViewModel GetOrCreateViewModel<TViewModel>(string cacheKey) where TViewModel : ViewModelBase
        {
            if (_viewModelCache.TryGetValue(cacheKey, out var cached))
                return (TViewModel)cached;

            var viewModel = _serviceProvider.GetRequiredService<TViewModel>();
            _viewModelCache[cacheKey] = viewModel;
            return viewModel;
        }

        private void NavigateToViewModel(ViewModelBase viewModel)
        {
            // 如果导航到当前页面，不做任何操作
            if (_currentPage == viewModel)
                return;

            if (_currentPage != null)
            {
                _navigationStack.Push(_currentPage);
            }

            // 限制导航栈深度，防止内存泄漏
            while (_navigationStack.Count > MaxStackSize)
            {
                var removed = _navigationStack.Pop();
                // 不 Dispose 缓存的 ViewModel，因为可能还会导航回来
            }

            _currentPage = viewModel;
            CurrentPageChanged?.Invoke();
        }

        public void GoBack()
        {
            if (!CanGoBack) return;

            _navigationStack.Pop();
            _currentPage = _navigationStack.Peek();
            CurrentPageChanged?.Invoke();
        }
    }
}
