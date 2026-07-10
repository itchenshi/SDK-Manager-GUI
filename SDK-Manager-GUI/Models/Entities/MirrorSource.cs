using System;

namespace SDK_Manager_GUI.Models
{
    public class MirrorSource
    {
        public string Id { get; set; }
        public SdkLanguage Language { get; set; }
        public string Name { get; set; }
        public string BaseUrl { get; set; }
        public bool IsEnabled { get; set; } = true;
        public int Priority { get; set; }
        public bool IsDefault { get; set; }
        /// <summary>
        /// 是否为预置镜像源（不可删除）
        /// </summary>
        public bool IsPreset { get; set; }
        public long? Latency { get; set; }

        /// <summary>
        /// 最近一次使用该镜像是否成功
        /// </summary>
        public bool? LastSuccess { get; set; }

        /// <summary>
        /// 连续失败次数
        /// </summary>
        public int FailCount { get; set; }

        /// <summary>
        /// 最后使用时间
        /// </summary>
        public DateTime? LastUsedTime { get; set; }
    }
}
