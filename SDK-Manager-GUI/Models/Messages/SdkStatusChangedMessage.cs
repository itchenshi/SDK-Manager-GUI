using SDK_Manager_GUI.ViewModels;

namespace SDK_Manager_GUI.Models
{
    /// <summary>
    /// SDK 状态变更消息，用于全局同步已安装SDK的状态和版本
    /// </summary>
    public sealed class SdkStatusChangedMessage : IMessage
    {
        public string Language { get; set; }
        public string Version { get; set; }
        public string Action { get; set; } // Install, Switch, Uninstall
    }
}
