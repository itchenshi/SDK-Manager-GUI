using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SDK_Manager_GUI.Services;
using SDK_Manager_GUI.ViewModels;

namespace SDK_Manager_GUI
{
    public partial class App : Application
    {
        private ServiceProvider _serviceProvider;
        private Mutex _singleInstanceMutex;
        private const string MutexName = "SDK_Manager_GUI_SingleInstance_Mutex";
        private const string PipeName = "SDK_Manager_GUI_SingleInstance_Pipe";

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 单实例检测
            _singleInstanceMutex = new Mutex(true, MutexName, out bool createdNew);
            if (!createdNew)
            {
                // 已有实例运行，通知已有实例前置窗口
                NotifyExistingInstance();
                Shutdown();
                return;
            }

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                MessageBox.Show($"Unhandled exception: {ex?.Message}\n\n{ex?.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show($"Dispatcher exception: {args.Exception.Message}\n\n{args.Exception.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            try
            {
                ConfigureDependencies();

                // 启动时自动清理过期日志
                try
                {
                    var configService = _serviceProvider.GetRequiredService<IConfigService>();
                    var logService = _serviceProvider.GetRequiredService<ILogService>();
                    var config = configService.GetConfigSync();
                    if (config.AutoCleanLogs && config.LogKeepDays > 0)
                    {
                        logService.CleanOldLogs(config.LogKeepDays);
                    }
                }
                catch { }

                var mainWindow = new MainWindow
                {
                    DataContext = _serviceProvider.GetRequiredService<MainWindowViewModel>()
                };

                var navigationService = _serviceProvider.GetRequiredService<INavigationService>();
                navigationService.NavigateTo<DashboardViewModel>();

                mainWindow.Show();

                // 启动时后台自动测试所有镜像源，不可达的自动禁用
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var mirrorProvider = _serviceProvider.GetRequiredService<IMirrorProvider>();
                        await mirrorProvider.TestAndDisableUnreachableMirrorsAsync();

                        var mavenService = _serviceProvider.GetRequiredService<IMavenService>();
                        await mavenService.TestAndDisableUnreachableMirrorsAsync();
                    }
                    catch { }
                });

                // 启动命名管道服务器，监听后续实例的通知
                StartPipeServer();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Startup error: {ex.Message}\n\n{ex.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 通知已有实例将窗口前置
        /// </summary>
        private void NotifyExistingInstance()
        {
            try
            {
                using (var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                {
                    client.Connect(3000); // 3秒超时
                    using (var writer = new StreamWriter(client) { AutoFlush = true })
                    {
                        writer.Write("ACTIVATE");
                    }
                }
            }
            catch
            {
                // 如果无法连接到已有实例，忽略
            }
        }

        /// <summary>
        /// 启动命名管道服务器，监听后续实例的激活请求
        /// </summary>
        private void StartPipeServer()
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                while (true)
                {
                    try
                    {
                        using (var server = new NamedPipeServerStream(PipeName, PipeDirection.In))
                        {
                            server.WaitForConnection();
                            using (var reader = new StreamReader(server))
                            {
                                var msg = reader.ReadToEnd();
                                if (msg == "ACTIVATE")
                                {
                                    // 在 UI 线程上激活窗口
                                    Dispatcher.BeginInvoke(new Action(() =>
                                    {
                                        var window = Current.MainWindow;
                                        if (window != null)
                                        {
                                            if (window.WindowState == WindowState.Minimized)
                                                window.WindowState = WindowState.Normal;

                                            var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                                            ShowWindow(handle, SW_RESTORE);
                                            SetForegroundWindow(handle);
                                        }
                                    }));
                                }
                            }
                        }
                    }
                    catch
                    {
                        // 管道异常，继续等待
                    }
                }
            });
        }

        private void ConfigureDependencies()
        {
            var services = new ServiceCollection();

            // Services - Singleton
            services.AddSingleton<IDownloadEngine, DownloadEngine>();
            services.AddSingleton<IEnvironmentManager, EnvironmentManager>();
            services.AddSingleton<IMirrorProvider, MirrorProvider>();
            services.AddSingleton<IPackageManagerMirrorService, PackageManagerMirrorService>();
            services.AddSingleton<IMavenService, MavenService>();
            services.AddSingleton<IConfigService, ConfigService>();
            services.AddSingleton<ILogService, LogService>();
            services.AddSingleton<ILanguageService, LanguageService>();

            // SDK Providers - Singleton
            services.AddSingleton<ISdkProvider, NodeJsSdkProvider>();
            services.AddSingleton<ISdkProvider, JavaSdkProvider>();
            services.AddSingleton<ISdkProvider, PythonSdkProvider>();

            // Core Service - Singleton
            services.AddSingleton<ISdkManagerService, SdkManagerService>();
            services.AddSingleton<IBackgroundTaskManager, BackgroundTaskManager>();

            // Navigation & Dialog - Singleton
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<IDialogService, DialogService>();

            // ViewModels - Singleton (需要全局消息监听的 ViewModel 必须为 Singleton)
            services.AddSingleton<MainWindowViewModel>();
            services.AddSingleton<DownloadListViewModel>();
            // ViewModels - Singleton (NavigationService 会缓存实例)
            services.AddSingleton<DashboardViewModel>();
            services.AddSingleton<MirrorConfigViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<AboutViewModel>();
            services.AddSingleton<LogViewModel>();
            services.AddSingleton<MavenDetailViewModel>();
            // ViewModels - Transient (NavigationService 按语言参数缓存独立实例)
            services.AddTransient<SdkDetailViewModel>();

            _serviceProvider = services.BuildServiceProvider();

            // Initialize language service
            var languageService = _serviceProvider.GetRequiredService<ILanguageService>();
            languageService.InitializeLanguage();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
            _serviceProvider?.Dispose();
            base.OnExit(e);
        }
    }
}
