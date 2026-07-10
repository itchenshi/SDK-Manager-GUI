using System;

namespace SDK_Manager_GUI.Models
{
    public class AppConfig
    {
        public string DefaultInstallPath { get; set; } = @"C:\SDK-Manager";

        public int MaxConcurrentDownloads { get; set; } = 3;
        public int MaxRetryCount { get; set; } = 3;

        /// <summary>
        /// 启动时是否自动清理过期日志
        /// </summary>
        public bool AutoCleanLogs { get; set; } = true;

        /// <summary>
        /// 日志保存天数，超过此天数的日志将被自动清理
        /// </summary>
        public int LogKeepDays { get; set; } = 30;

        /// <summary>
        /// 界面语言（zh-CN, zh-TW, en）
        /// </summary>
        public string Language { get; set; } = "zh-CN";
    }
}
