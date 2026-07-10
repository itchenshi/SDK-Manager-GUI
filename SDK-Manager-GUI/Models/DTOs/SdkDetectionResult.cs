namespace SDK_Manager_GUI.Models
{
    public class SdkDetectionResult
    {
        /// <summary>
        /// SDK 语言类型
        /// </summary>
        public SdkLanguage Language { get; set; }

        /// <summary>
        /// 是否已安装
        /// </summary>
        public bool IsInstalled { get; set; }

        /// <summary>
        /// 通过命令行获取的版本号
        /// </summary>
        public string DetectedVersion { get; set; }

        /// <summary>
        /// 通过环境变量获取的安装路径
        /// </summary>
        public string InstallPath { get; set; }

        /// <summary>
        /// 通过配置管理的安装版本
        /// </summary>
        public string ManagedVersion { get; set; }

        /// <summary>
        /// 是否由本工具管理（配置中存在记录）
        /// </summary>
        public bool IsManaged { get; set; }

        /// <summary>
        /// 用户级环境变量路径
        /// </summary>
        public string UserLevelPath { get; set; }

        /// <summary>
        /// 用户级版本号
        /// </summary>
        public string UserLevelVersion { get; set; }

        /// <summary>
        /// 系统级环境变量路径
        /// </summary>
        public string SystemLevelPath { get; set; }

        /// <summary>
        /// 系统级版本号
        /// </summary>
        public string SystemLevelVersion { get; set; }
    }
}
