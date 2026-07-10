using SDK_Manager_GUI.ViewModels;

namespace SDK_Manager_GUI.Models
{
    public sealed class NavigateMessage : IMessage
    {
        public string Target { get; set; }
        public object Parameter { get; set; }
    }
}
