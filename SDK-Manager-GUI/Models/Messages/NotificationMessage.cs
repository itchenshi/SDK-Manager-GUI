using SDK_Manager_GUI.ViewModels;

namespace SDK_Manager_GUI.Models
{
    public sealed class NotificationMessage : IMessage
    {
        public string Title { get; set; }
        public string Content { get; set; }
        public NotificationType Type { get; set; }
    }
}
