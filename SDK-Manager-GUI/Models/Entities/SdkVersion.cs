using System;

namespace SDK_Manager_GUI.Models
{
    public class SdkVersion
    {
        public string Version { get; set; }
        public VersionCategory Category { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public long? FileSize { get; set; }
        public string DownloadUrl { get; set; }
        public string Sha256 { get; set; }
    }
}
