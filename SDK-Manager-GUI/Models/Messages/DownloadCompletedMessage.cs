using SDK_Manager_GUI.ViewModels;

namespace SDK_Manager_GUI.Models
{
    public sealed class DownloadCompletedMessage : IMessage
    {
        public string TaskId { get; set; }
        public string Language { get; set; }
        public string Version { get; set; }
        public string FilePath { get; set; }
    }
}
