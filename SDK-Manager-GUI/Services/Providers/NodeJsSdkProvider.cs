using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SDK_Manager_GUI.Models;

namespace SDK_Manager_GUI.Services
{
    public class NodeJsSdkProvider : ISdkProvider
    {
        private readonly HttpClient _httpClient;

        public NodeJsSdkProvider()
        {
            _httpClient = SdkProviderHelper.CreateHttpClient();
        }

        public SdkLanguage Language => SdkLanguage.NodeJs;

        public async Task<IEnumerable<SdkVersion>> GetAvailableVersionsAsync()
        {
            var versions = new List<SdkVersion>();
            var json = await _httpClient.GetStringAsync("https://nodejs.org/dist/index.json");
            var array = JArray.Parse(json);

            foreach (var item in array)
            {
                var versionStr = item["version"]?.ToString();
                if (string.IsNullOrEmpty(versionStr)) continue;

                var isLts = item["lts"] != null && item["lts"].Type != JTokenType.Boolean;
                var category = isLts ? VersionCategory.LTS : VersionCategory.Current;

                versions.Add(new SdkVersion
                {
                    Version = versionStr,
                    Category = category,
                    ReleaseDate = item["date"]?.ToObject<System.DateTime>(),
                    DownloadUrl = $"https://nodejs.org/dist/{versionStr}/node-{versionStr}-win-x64.zip"
                });
            }

            return versions;
        }

        public Task<string> GetDownloadUrlAsync(string version, string mirrorBaseUrl)
        {
            var url = $"{mirrorBaseUrl.TrimEnd('/')}/{version}/node-{version}-win-x64.zip";
            return Task.FromResult(url);
        }

        public Task<SdkVersion> ParseVersionAsync(string versionString)
        {
            var category = versionString.StartsWith("v18") || versionString.StartsWith("v20") || versionString.StartsWith("v22")
                ? VersionCategory.LTS : VersionCategory.Current;

            return Task.FromResult(new SdkVersion
            {
                Version = versionString,
                Category = category
            });
        }

        public Task<bool> ValidateInstallationAsync(string installPath)
        {
            var nodeExe = Path.Combine(installPath, "node.exe");
            return Task.FromResult(File.Exists(nodeExe));
        }

        public string GetExecutableName() => "node.exe";

        public string GetEnvironmentVariableName() => "NODE_HOME";

        public string GetBinPath(string installPath) => installPath;
        public string GetScriptsPath(string installPath) => null;
        public string GetVersionArgument() => "--version";
    }
}
