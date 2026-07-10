using System;

namespace SDK_Manager_GUI.Models
{
    public class InstalledSdk
    {
        public SdkLanguage Language { get; set; }
        public string Version { get; set; }
        public string InstallPath { get; set; }
        public bool IsActive { get; set; }
        public DateTime InstallDate { get; set; }

        /// <summary>
        /// 是否在用户级 PATH 中
        /// </summary>
        public bool IsUserLevel { get; set; }

        /// <summary>
        /// 是否在系统级 PATH 中
        /// </summary>
        public bool IsSystemLevel { get; set; }
    }
}
