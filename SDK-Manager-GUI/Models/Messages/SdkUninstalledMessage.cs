using SDK_Manager_GUI.ViewModels;

namespace SDK_Manager_GUI.Models
{
    public sealed class SdkUninstalledMessage : IMessage
    {
        public string Language { get; set; }
        public string Version { get; set; }
    }
}
