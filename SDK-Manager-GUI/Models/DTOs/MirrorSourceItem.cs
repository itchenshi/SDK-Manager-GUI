using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SDK_Manager_GUI.Models
{
    public class MirrorSourceItem : INotifyPropertyChanged
    {
        private string _id;
        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        private string _name;
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        private string _baseUrl;
        public string BaseUrl
        {
            get => _baseUrl;
            set { _baseUrl = value; OnPropertyChanged(); }
        }

        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(); }
        }

        private int _priority;
        public int Priority
        {
            get => _priority;
            set { _priority = value; OnPropertyChanged(); }
        }

        private long? _latency;
        public long? Latency
        {
            get => _latency;
            set { _latency = value; OnPropertyChanged(); OnPropertyChanged(nameof(LatencyDisplay)); }
        }

        private bool _isDefault;
        public bool IsDefault
        {
            get => _isDefault;
            set { _isDefault = value; OnPropertyChanged(); }
        }

        private bool _isPreset;
        public bool IsPreset
        {
            get => _isPreset;
            set { _isPreset = value; OnPropertyChanged(); }
        }

        public string LatencyDisplay
        {
            get
            {
                if (!Latency.HasValue) return "-";
                if (Latency.Value < 0) return "Timeout";
                return Latency.Value.ToString();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
