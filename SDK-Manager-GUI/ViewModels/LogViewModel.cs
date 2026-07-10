using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using SDK_Manager_GUI.Services;

namespace SDK_Manager_GUI.ViewModels
{
    public class LogViewModel : ViewModelBase
    {
        private readonly ILogService _logService;
        private readonly IDialogService _dialogService;
        private readonly IConfigService _configService;
        private readonly ILanguageService _languageService;

        private ObservableCollection<LogEntry> _logs;
        public ObservableCollection<LogEntry> Logs
        {
            get => _logs;
            set => SetProperty(ref _logs, value);
        }

        private string _filterLevel;
        public string FilterLevel
        {
            get => _filterLevel;
            set { if (SetProperty(ref _filterLevel, value)) ApplyFilter(); }
        }

        public ObservableCollection<string> LevelOptions { get; }

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set { if (SetProperty(ref _searchText, value)) ApplyFilter(); }
        }

        private string _logInfo;
        public string LogInfo
        {
            get => _logInfo;
            set => SetProperty(ref _logInfo, value);
        }

        private DateTime _selectedDate = DateTime.Today;
        public DateTime SelectedDate
        {
            get => _selectedDate;
            set { if (SetProperty(ref _selectedDate, value)) LoadLogsByDate(value); }
        }

        private ObservableCollection<DateTime> _availableDates;
        public ObservableCollection<DateTime> AvailableDates
        {
            get => _availableDates;
            set => SetProperty(ref _availableDates, value);
        }

        // 日志管理设置
        private bool _autoCleanLogs;
        public bool AutoCleanLogs
        {
            get => _autoCleanLogs;
            set => SetProperty(ref _autoCleanLogs, value);
        }

        private int _logKeepDays;
        public int LogKeepDays
        {
            get => _logKeepDays;
            set => SetProperty(ref _logKeepDays, value);
        }

        public ICommand RefreshCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand OpenLogFolderCommand { get; }
        public ICommand SaveLogSettingsCommand { get; }

        private ObservableCollection<LogEntry> _allLogs;

        public LogViewModel(ILogService logService, IDialogService dialogService, IConfigService configService, ILanguageService languageService)
        {
            _logService = logService;
            _dialogService = dialogService;
            _configService = configService;
            _languageService = languageService;
            _logs = new ObservableCollection<LogEntry>();
            _allLogs = new ObservableCollection<LogEntry>();
            _availableDates = new ObservableCollection<DateTime>();

            _filterLevel = _languageService.GetString("Common_All");
            LevelOptions = new ObservableCollection<string>
            {
                _languageService.GetString("Common_All"), "INFO", "WARN", "ERROR", "DEBUG"
            };

            RefreshCommand = new RelayCommand(() => LoadLogsByDate(SelectedDate));
            ClearCommand = new RelayCommand(async () => await ClearLogsAsync());
            OpenLogFolderCommand = new RelayCommand(() => OpenLogFolder());
            SaveLogSettingsCommand = new RelayCommand(async () => await SaveLogSettingsAsync());

            // 加载可用日期
            LoadAvailableDates();

            // 自动加载当天日志
            LoadLogsByDate(DateTime.Today);

            // 加载日志设置
            LoadLogSettings();
        }

        private void LoadAvailableDates()
        {
            var dates = _logService.GetAvailableLogDates();
            AvailableDates = new ObservableCollection<DateTime>(dates);

            // 如果今天没有日志文件，把今天也加入可选日期（确保至少有一个日期可选）
            if (!dates.Contains(DateTime.Today))
            {
                AvailableDates.Insert(0, DateTime.Today);
            }

            // 如果没有任何日期（日志目录不存在），默认显示当天
            if (AvailableDates.Count == 0)
            {
                AvailableDates.Add(DateTime.Today);
            }

            // 设置选中日期为今天（如果存在）或第一个可用日期
            SelectedDate = AvailableDates.Contains(DateTime.Today) ? DateTime.Today : AvailableDates[0];
        }

        public void LoadLogsByDate(DateTime date)
        {
            var entries = _logService.GetLogsByDate(date);
            _allLogs = new ObservableCollection<LogEntry>(entries);
            ApplyFilter();

            var logDir = _logService.GetLogDirectory();
            var entryCount = entries.Count;
            LogInfo = string.Format(_languageService.GetString("Log_DirInfo"), logDir, date.ToString("yyyy-MM-dd"), entryCount);
        }

        private void ApplyFilter()
        {
            var filtered = _allLogs.AsEnumerable();

            if (FilterLevel != _languageService.GetString("Common_All"))
                filtered = filtered.Where(l => l.Level == FilterLevel);

            if (!string.IsNullOrWhiteSpace(SearchText))
                filtered = filtered.Where(l => l.Message.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0);

            Logs = new ObservableCollection<LogEntry>(filtered);
        }

        private async System.Threading.Tasks.Task ClearLogsAsync()
        {
            var confirm = await _dialogService.ShowConfirmAsync(_languageService.GetString("Dialog_ClearLogConfirm"), _languageService.GetString("Dialog_ClearLogConfirmMsg"));
            if (!confirm) return;

            _logService.ClearLogs();
            _allLogs.Clear();
            Logs.Clear();
        }

        private void OpenLogFolder()
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", _logService.GetLogDirectory());
            }
            catch { }
        }

        private void LoadLogSettings()
        {
            try
            {
                var config = _configService.GetConfigSync();
                AutoCleanLogs = config.AutoCleanLogs;
                LogKeepDays = config.LogKeepDays;
            }
            catch
            {
                AutoCleanLogs = true;
                LogKeepDays = 30;
            }
        }

        private async System.Threading.Tasks.Task SaveLogSettingsAsync()
        {
            try
            {
                var config = await _configService.GetConfigAsync();
                config.AutoCleanLogs = AutoCleanLogs;
                config.LogKeepDays = LogKeepDays > 0 ? LogKeepDays : 30;
                await _configService.SaveConfigAsync(config);

                await _dialogService.ShowInfoAsync(_languageService.GetString("Dialog_LogSettingsSaved"), _languageService.GetString("Dialog_LogSettingsSaved"));
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync(_languageService.GetString("Dialog_SaveFailed"), ex.Message);
            }
        }
    }
}
