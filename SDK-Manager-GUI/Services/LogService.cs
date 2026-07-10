using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SDK_Manager_GUI.Services
{
    public interface ILogService
    {
        void Info(string message);
        void Warn(string message);
        void Error(string message, Exception ex = null);
        void Debug(string message);
        IReadOnlyList<LogEntry> GetRecentLogs(int count = 200);
        IReadOnlyList<LogEntry> GetLogsByDate(DateTime date);
        string GetLogFilePath();
        string GetLogDirectory();
        void ClearLogs();
        List<DateTime> GetAvailableLogDates();
        void CleanOldLogs(int keepDays);
    }

    public class LogEntry
    {
        public DateTime Time { get; set; }
        public string Level { get; set; }
        public string Message { get; set; }

        public string DisplayText => $"[{Time:HH:mm:ss}] [{Level}] {Message}";
    }

    public class LogService : ILogService
    {
        private readonly string _logDir;
        private string _logFile;
        private DateTime _logFileDate;
        private readonly ConcurrentQueue<LogEntry> _memoryLogs = new ConcurrentQueue<LogEntry>();
        private readonly object _fileLock = new object();
        private const int MaxMemoryLogs = 500;

        public LogService()
        {
            _logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            if (!Directory.Exists(_logDir))
                Directory.CreateDirectory(_logDir);

            _logFileDate = DateTime.Now.Date;
            _logFile = Path.Combine(_logDir, $"log_{DateTime.Now:yyyyMMdd}.txt");
        }

        /// <summary>
        /// 确保日志文件路径与当前日期一致，跨天后自动切换到新文件
        /// </summary>
        private void EnsureLogFileDate()
        {
            var today = DateTime.Now.Date;
            if (today != _logFileDate)
            {
                lock (_fileLock)
                {
                    if (today != _logFileDate)
                    {
                        _logFileDate = today;
                        _logFile = Path.Combine(_logDir, $"log_{today:yyyyMMdd}.txt");
                    }
                }
            }
        }

        public void Info(string message) => WriteLog("INFO", message);
        public void Warn(string message) => WriteLog("WARN", message);
        public void Error(string message, Exception ex = null)
        {
            var msg = ex != null ? $"{message}: {ex.Message}\n{ex.StackTrace}" : message;
            WriteLog("ERROR", msg);
        }
        public void Debug(string message) => WriteLog("DEBUG", message);

        private void WriteLog(string level, string message)
        {
            var entry = new LogEntry
            {
                Time = DateTime.Now,
                Level = level,
                Message = message
            };

            _memoryLogs.Enqueue(entry);
            while (_memoryLogs.Count > MaxMemoryLogs)
                _memoryLogs.TryDequeue(out _);

            // 异步写入文件
            try
            {
                EnsureLogFileDate();
                lock (_fileLock)
                {
                    File.AppendAllText(_logFile, $"{entry.Time:yyyy-MM-dd HH:mm:ss} [{level}] {message}\n");
                }
            }
            catch { }
        }

        public IReadOnlyList<LogEntry> GetRecentLogs(int count = 200)
        {
            return _memoryLogs.Skip(Math.Max(0, _memoryLogs.Count - count)).ToList();
        }

        public string GetLogFilePath() => _logFile;
        public string GetLogDirectory() => _logDir;

        public void ClearLogs()
        {
            while (_memoryLogs.TryDequeue(out _)) { }
        }

        /// <summary>
        /// 按日期加载日志文件
        /// </summary>
        public IReadOnlyList<LogEntry> GetLogsByDate(DateTime date)
        {
            var logFile = Path.Combine(_logDir, $"log_{date:yyyyMMdd}.txt");
            if (!File.Exists(logFile))
                return new List<LogEntry>();

            try
            {
                var entries = new List<LogEntry>();
                var lines = File.ReadAllLines(logFile);
                foreach (var line in lines)
                {
                    var entry = ParseLogLine(line);
                    if (entry != null)
                        entries.Add(entry);
                }
                return entries;
            }
            catch
            {
                return new List<LogEntry>();
            }
        }

        /// <summary>
        /// 获取日志目录中所有可用的日志日期
        /// </summary>
        public List<DateTime> GetAvailableLogDates()
        {
            var dates = new List<DateTime>();
            try
            {
                if (!Directory.Exists(_logDir))
                    return dates;

                foreach (var file in Directory.GetFiles(_logDir, "log_*.txt"))
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    // 文件名格式: log_20260609
                    var dateStr = fileName.Substring(4); // 去掉 "log_"
                    if (DateTime.TryParseExact(dateStr, "yyyyMMdd", null,
                        System.Globalization.DateTimeStyles.None, out var date))
                    {
                        dates.Add(date);
                    }
                }
            }
            catch { }

            dates.Sort();
            dates.Reverse(); // 最新的日期在前
            return dates;
        }

        /// <summary>
        /// 清理超过指定天数的日志文件
        /// </summary>
        public void CleanOldLogs(int keepDays)
        {
            try
            {
                if (!Directory.Exists(_logDir) || keepDays <= 0) return;

                var cutoff = DateTime.Now.AddDays(-keepDays);
                foreach (var file in Directory.GetFiles(_logDir, "log_*.txt"))
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.LastWriteTime < cutoff)
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// 解析日志行：格式 "2026-06-09 15:30:00 [INFO] message"
        /// </summary>
        private static LogEntry ParseLogLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return null;

            try
            {
                // 格式: 2026-06-09 15:30:00 [INFO] message
                var dateEnd = line.IndexOf(' ');
                if (dateEnd < 0) return null;
                var timeEnd = line.IndexOf(' ', dateEnd + 1);
                if (timeEnd < 0) return null;

                var dateStr = line.Substring(0, timeEnd);
                if (!DateTime.TryParse(dateStr, out var time)) return null;

                var levelStart = line.IndexOf('[', timeEnd);
                var levelEnd = line.IndexOf(']', levelStart);
                if (levelStart < 0 || levelEnd < 0) return null;

                var level = line.Substring(levelStart + 1, levelEnd - levelStart - 1);
                var message = line.Substring(levelEnd + 1).TrimStart();

                return new LogEntry { Time = time, Level = level, Message = message };
            }
            catch
            {
                return null;
            }
        }
    }
}
