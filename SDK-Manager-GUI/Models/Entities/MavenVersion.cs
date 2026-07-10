using System;

namespace SDK_Manager_GUI.Models
{
    public class MavenVersion
    {
        public string Version { get; set; }
        public string DownloadUrl { get; set; }
    }

    public class MavenDetectionResult
    {
        /// <summary>
        /// 用户级是否已安装
        /// </summary>
        public bool IsUserLevelInstalled { get; set; }

        /// <summary>
        /// 系统级是否已安装
        /// </summary>
        public bool IsSystemLevelInstalled { get; set; }

        /// <summary>
        /// 用户级 Maven 版本
        /// </summary>
        public string UserLevelVersion { get; set; }

        /// <summary>
        /// 系统级 Maven 版本
        /// </summary>
        public string SystemLevelVersion { get; set; }

        /// <summary>
        /// 用户级 Maven 安装路径
        /// </summary>
        public string UserLevelPath { get; set; }

        /// <summary>
        /// 系统级 Maven 安装路径
        /// </summary>
        public string SystemLevelPath { get; set; }

        /// <summary>
        /// 是否通过 PATH 检测到 Maven（非本工具安装）
        /// </summary>
        public bool IsInstalledViaPath { get; set; }

        /// <summary>
        /// PATH 中检测到的 mvn 位置
        /// </summary>
        public string PathMvnLocation { get; set; }

        /// <summary>
        /// PATH 中检测到的 Maven 版本
        /// </summary>
        public string PathMvnVersion { get; set; }
    }

    /// <summary>
    /// Maven 下载镜像源
    /// </summary>
    public class MavenDownloadMirror
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string BaseUrl { get; set; }
        public bool IsEnabled { get; set; } = true;
        public bool IsDefault { get; set; }
        /// <summary>
        /// 是否为预置镜像源（不可删除）
        /// </summary>
        public bool IsPreset { get; set; }
        public long? Latency { get; set; }
        public bool? LastSuccess { get; set; }
        public DateTime? LastUsedTime { get; set; }
    }
}
