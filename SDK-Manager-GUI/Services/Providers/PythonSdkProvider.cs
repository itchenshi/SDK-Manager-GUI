using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using SDK_Manager_GUI.Models;

namespace SDK_Manager_GUI.Services
{
    public class PythonSdkProvider : ISdkProvider
    {
        private readonly HttpClient _httpClient;

        public PythonSdkProvider()
        {
            _httpClient = SdkProviderHelper.CreateHttpClient();
        }

        public SdkLanguage Language => SdkLanguage.Python;

        /// <summary>
        /// 获取 Python Embeddable ZIP 的下载 URL（绿色安装版，所有版本都有）
        /// 格式：https://www.python.org/ftp/python/{version}/python-{version}-embed-amd64.zip
        /// </summary>
        public static string GetZipUrl(string version)
        {
            return $"https://www.python.org/ftp/python/{version}/python-{version}-embed-amd64.zip";
        }

        public async Task<IEnumerable<SdkVersion>> GetAvailableVersionsAsync()
        {
            var versions = new List<SdkVersion>();

            // 优先使用 Python 官方 API
            try
            {
                var json = await _httpClient.GetStringAsync("https://www.python.org/api/v2/downloads/release/");
                var array = JArray.Parse(json);

                foreach (var item in array)
                {
                    var versionStr = item["name"]?.ToString();
                    if (string.IsNullOrEmpty(versionStr)) continue;

                    // 清理版本号，提取纯数字版本
                    var match = Regex.Match(versionStr, @"(\d+\.\d+\.\d+)");
                    if (!match.Success) continue;
                    var cleanVersion = match.Groups[1].Value;

                    if (versions.Any(v => v.Version == cleanVersion)) continue;

                    var isPreRelease = versionStr.Contains("a") || versionStr.Contains("b") ||
                                       versionStr.Contains("rc") || versionStr.Contains("Alpha") ||
                                       versionStr.Contains("Beta");
                    var category = isPreRelease ? VersionCategory.PreRelease : GetPythonCategory(cleanVersion);

                    versions.Add(new SdkVersion
                    {
                        Version = cleanVersion,
                        Category = category,
                        ReleaseDate = item["release_date"] != null
                            ? DateTime.TryParse(item["release_date"].ToString(), out var rd) ? rd : (DateTime?)null
                            : null,
                        DownloadUrl = GetZipUrl(cleanVersion)
                    });
                }

                if (versions.Count > 0) return versions;
            }
            catch
            {
                // API 不可用，尝试备用方案
            }

            // 备用方案：解析 HTML 页面
            try
            {
                var html = await _httpClient.GetStringAsync("https://www.python.org/downloads/");
                var regex = new Regex(@"Python (\d+\.\d+\.\d+)");
                var matches = regex.Matches(html);

                foreach (Match match in matches)
                {
                    var versionStr = match.Groups[1].Value;
                    if (versions.Any(v => v.Version == versionStr)) continue;

                    var isPreRelease = versionStr.Contains("a") || versionStr.Contains("b") || versionStr.Contains("rc");
                    var category = isPreRelease ? VersionCategory.PreRelease : GetPythonCategory(versionStr);

                    versions.Add(new SdkVersion
                    {
                        Version = versionStr,
                        Category = category,
                        DownloadUrl = GetZipUrl(versionStr)
                    });
                }
            }
            catch
            {
                // HTML 解析也失败，尝试第三种方案
            }

            // 第三种方案：使用已知的版本列表
            if (versions.Count == 0)
            {
                foreach (var v in GetFallbackVersions())
                {
                    versions.Add(v);
                }
            }

            return versions;
        }

        private IEnumerable<SdkVersion> GetFallbackVersions()
        {
            var knownVersions = new[]
            {
                "3.13.3", "3.13.2", "3.13.1", "3.13.0",
                "3.12.10", "3.12.9", "3.12.8", "3.12.7", "3.12.6", "3.12.5", "3.12.4", "3.12.3", "3.12.2", "3.12.1", "3.12.0",
                "3.11.12", "3.11.11", "3.11.10", "3.11.9", "3.11.8", "3.11.7", "3.11.6", "3.11.5", "3.11.4", "3.11.3", "3.11.2", "3.11.1", "3.11.0",
                "3.10.17", "3.10.16", "3.10.15", "3.10.14", "3.10.13", "3.10.12", "3.10.11", "3.10.10", "3.10.9", "3.10.8", "3.10.7", "3.10.6", "3.10.5", "3.10.4", "3.10.3", "3.10.2", "3.10.1", "3.10.0",
                "3.9.22", "3.9.21", "3.9.20", "3.9.19", "3.9.18", "3.9.17", "3.9.16", "3.9.15", "3.9.14", "3.9.13", "3.9.12", "3.9.11", "3.9.10", "3.9.9", "3.9.8", "3.9.7", "3.9.6", "3.9.5", "3.9.4", "3.9.3", "3.9.2", "3.9.1", "3.9.0",
                "3.8.20", "3.8.19", "3.8.18", "3.8.17", "3.8.16", "3.8.15", "3.8.14", "3.8.13", "3.8.12", "3.8.11", "3.8.10",
            };

            foreach (var v in knownVersions)
            {
                yield return new SdkVersion
                {
                    Version = v,
                    Category = GetPythonCategory(v),
                    DownloadUrl = GetZipUrl(v)
                };
            }
        }

        public Task<string> GetDownloadUrlAsync(string version, string mirrorBaseUrl)
        {
            var url = $"{mirrorBaseUrl.TrimEnd('/')}/{version}/python-{version}-embed-amd64.zip";
            return Task.FromResult(url);
        }

        public Task<SdkVersion> ParseVersionAsync(string versionString)
        {
            var isPreRelease = versionString.Contains("a") || versionString.Contains("b") || versionString.Contains("rc");
            return Task.FromResult(new SdkVersion
            {
                Version = versionString,
                Category = isPreRelease ? VersionCategory.PreRelease : GetPythonCategory(versionString)
            });
        }

        public Task<bool> ValidateInstallationAsync(string installPath)
        {
            var pythonExe = Path.Combine(installPath, "python.exe");
            return Task.FromResult(File.Exists(pythonExe));
        }

        public string GetExecutableName() => "python.exe";

        public string GetEnvironmentVariableName() => "PYTHON_HOME";

        public string GetBinPath(string installPath) => installPath;

        /// <summary>
        /// Python embeddable 版本安装 pip 后，Scripts 目录包含 pip.exe 等工具
        /// </summary>
        public string GetScriptsPath(string installPath) => Path.Combine(installPath, "Scripts");
        public string GetVersionArgument() => "--version";

        /// <summary>
        /// 根据 Python 版本号判断分类（LTS/Current）
        /// Python 没有 LTS 概念，但将安全维护中的版本标记为 LTS 以便筛选
        /// </summary>
        private static VersionCategory GetPythonCategory(string version)
        {
            if (string.IsNullOrEmpty(version)) return VersionCategory.Current;

            var parts = version.Split('.');
            if (parts.Length < 2 || !int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor))
                return VersionCategory.Current;

            var securityMaintenanceVersions = new[] { 9, 10 };
            if (major == 3 && securityMaintenanceVersions.Contains(minor))
                return VersionCategory.LTS;

            return VersionCategory.Current;
        }
    }
}
