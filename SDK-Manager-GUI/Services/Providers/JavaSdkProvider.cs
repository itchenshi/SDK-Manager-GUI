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
    public class JavaSdkProvider : ISdkProvider
    {
        private readonly HttpClient _httpClient;

        public JavaSdkProvider()
        {
            _httpClient = SdkProviderHelper.CreateHttpClient();
        }

        public SdkLanguage Language => SdkLanguage.Java;

        public async Task<IEnumerable<SdkVersion>> GetAvailableVersionsAsync()
        {
            var versions = new List<SdkVersion>();

            try
            {
                // 获取 LTS 版本：8, 11, 17, 21
                foreach (var majorVersion in new[] { 8, 11, 17, 21 })
                {
                    try
                    {
                        var url = $"https://api.adoptium.net/v3/assets/latest/{majorVersion}/hotspot?architecture=x64&image_type=jdk&os=windows&vendor=eclipse";
                        var response = await _httpClient.GetStringAsync(url);
                        var array = JArray.Parse(response);

                        foreach (var item in array)
                        {
                            var versionData = item["version"]?["semver"];
                            var versionStr = versionData != null ? CleanJavaVersion(versionData.ToString()) : $"jdk-{majorVersion}";
                            var isLts = new[] { 8, 11, 17, 21 }.Contains(majorVersion);

                            // 从 API 获取发布日期
                            DateTime? releaseDate = null;
                            var timestampStr = item["binary"]?["package"]?["timestamp"]?.ToString();
                            if (!string.IsNullOrEmpty(timestampStr) && DateTime.TryParse(timestampStr, out var ts))
                                releaseDate = ts;

                            versions.Add(new SdkVersion
                            {
                                Version = versionStr,
                                Category = isLts ? VersionCategory.LTS : VersionCategory.Current,
                                ReleaseDate = releaseDate,
                                DownloadUrl = item["binary"]?["package"]?["link"]?.ToString()
                            });
                        }
                    }
                    catch
                    {
                        // 单个版本获取失败不影响其他版本
                    }
                }
            }
            catch
            {
                // API 不可用时返回空列表
            }

            return versions;
        }

        public async Task<string> GetDownloadUrlAsync(string version, string mirrorBaseUrl)
        {
            var majorVersion = GetMajorVersion(version);

            // 清华镜像：构建直接下载 URL（清华镜像不提供 API，只提供文件目录）
            if (mirrorBaseUrl.Contains("tsinghua") || mirrorBaseUrl.Contains("tuna"))
            {
                // 通过官方 API 获取下载链接以提取文件名
                try
                {
                    var apiUrl = $"https://api.adoptium.net/v3/assets/latest/{majorVersion}/hotspot?architecture=x64&image_type=jdk&os=windows&vendor=eclipse";
                    var response = await _httpClient.GetStringAsync(apiUrl);
                    var array = JArray.Parse(response);
                    var downloadLink = array.FirstOrDefault()?["binary"]?["package"]?["link"]?.ToString();
                    if (!string.IsNullOrEmpty(downloadLink))
                    {
                        var filename = Path.GetFileName(new Uri(downloadLink).AbsolutePath);
                        return $"{mirrorBaseUrl.TrimEnd('/')}/{majorVersion}/jdk/x64/windows/{filename}";
                    }
                }
                catch { }

                // API 不可用时，回退到官方 binary 端点
                return $"https://api.adoptium.net/v3/binary/latest/{majorVersion}/ga/windows/x64/jdk/hotspot/normal/eclipse";
            }

            // 官方 Adoptium API 及其他镜像：使用 binary 端点直接重定向到 ZIP 文件
            // /v3/binary/latest/{version}/ga/windows/x64/jdk/hotspot/normal/eclipse 会 302 重定向到实际 ZIP 下载地址
            return $"https://api.adoptium.net/v3/binary/latest/{majorVersion}/ga/windows/x64/jdk/hotspot/normal/eclipse";
        }

        private static int GetMajorVersion(string version)
        {
            if (string.IsNullOrEmpty(version)) return 11;
            // 处理 semver 格式如 "11.0.21+9"
            var parts = version.Split('.');
            if (int.TryParse(parts[0], out var major)) return major;
            return 11;
        }

        /// <summary>
        /// 清除 Java 版本号中的构建号后缀（如 +9、+10、+11）和 .LTS 标记
        /// 例如 "11.0.21+9" → "11.0.21"，"17.0.9+11.LTS" → "17.0.9"
        /// </summary>
        private static string CleanJavaVersion(string version)
        {
            if (string.IsNullOrEmpty(version)) return version;

            // 移除 +N 构建号后缀（如 +9、+10、+11）
            var plusIndex = version.IndexOf('+');
            if (plusIndex >= 0)
                version = version.Substring(0, plusIndex);

            // 移除 .LTS 后缀
            if (version.EndsWith(".LTS", StringComparison.OrdinalIgnoreCase))
                version = version.Substring(0, version.Length - 4);

            return version;
        }

        public Task<SdkVersion> ParseVersionAsync(string versionString)
        {
            var isLts = versionString.Contains("8.") || versionString.Contains("11.") ||
                        versionString.Contains("17.") || versionString.Contains("21.");

            return Task.FromResult(new SdkVersion
            {
                Version = versionString,
                Category = isLts ? VersionCategory.LTS : VersionCategory.Current
            });
        }

        public Task<bool> ValidateInstallationAsync(string installPath)
        {
            var javaExe = Path.Combine(installPath, "bin", "java.exe");
            return Task.FromResult(File.Exists(javaExe));
        }

        public string GetExecutableName() => "java.exe";

        public string GetEnvironmentVariableName() => "JAVA_HOME";

        public string GetBinPath(string installPath) => Path.Combine(installPath, "bin");
        public string GetScriptsPath(string installPath) => null;
        public string GetVersionArgument() => "-version";
    }
}
