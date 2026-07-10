using System;

namespace SDK_Manager_GUI.Models
{
    public class Sdk
    {
        public SdkLanguage Language { get; set; }
        public string Name { get; set; }
        public string Icon { get; set; }
        public string DefaultInstallPath { get; set; }
        public string EnvironmentVariableName { get; set; }
    }
}
