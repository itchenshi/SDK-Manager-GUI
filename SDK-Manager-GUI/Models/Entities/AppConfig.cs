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

        // ===== Python Embeddable 安装选项 =====

        /// <summary>
        /// 安装 pip（默认开启）
        /// </summary>
        public bool PythonInstallPip { get; set; } = true;

        /// <summary>
        /// 启用 site-packages 机制（默认开启）
        /// </summary>
        public bool PythonEnableSitePackages { get; set; } = true;

        /// <summary>
        /// 补全 Tcl/Tk (tkinter) 支持（默认关闭，需下载完整安装包提取文件）
        /// </summary>
        public bool PythonInstallTclTk { get; set; } = false;

        /// <summary>
        /// 补全 IDLE / 文档（默认关闭，依赖 Tcl/Tk）
        /// </summary>
        public bool PythonInstallIdle { get; set; } = false;

        /// <summary>
        /// 注册到 Windows 注册表（默认关闭）
        /// </summary>
        public bool PythonRegisterRegistry { get; set; } = false;

        /// <summary>
        /// 关联 .py 文件（默认关闭）
        /// </summary>
        public bool PythonAssociateFiles { get; set; } = false;
    }
}
