using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using SDK_Manager_GUI.Models;
using SDK_Manager_GUI.Services;

namespace SDK_Manager_GUI.Services
{
    public class PackageManagerMirrorService : IPackageManagerMirrorService
    {
        private readonly ILogService _logService;
        private readonly ILanguageService _languageService;

        // 全局缓存：各 SDK 的当前镜像源地址
        private readonly Dictionary<SdkLanguage, string> _cachedMirrors = new();

        public PackageManagerMirrorService(ILogService logService, ILanguageService languageService)
        {
            _logService = logService;
            _languageService = languageService;
        }

        /// <summary>
        /// 启动时预加载所有 SDK 的镜像源地址到缓存
        /// </summary>
        public async Task PreloadMirrorsAsync()
        {
            foreach (SdkLanguage language in new[] { SdkLanguage.NodeJs, SdkLanguage.Java, SdkLanguage.Python })
            {
                try
                {
                    var mirror = await GetCurrentMirrorInternalAsync(language);
                    _cachedMirrors[language] = mirror;
                }
                catch
                {
                    _cachedMirrors[language] = _languageService.GetString("Common_DetectFailed");
                }
            }
        }

        /// <summary>
        /// 从缓存获取镜像源地址（如缓存不存在则实时获取）
        /// </summary>
        public string GetCachedMirror(SdkLanguage language)
        {
            return _cachedMirrors.TryGetValue(language, out var mirror) ? mirror : null;
        }

        public async Task<string> GetCurrentMirrorAsync(SdkLanguage language)
        {
            try
            {
                var mirror = await GetCurrentMirrorInternalAsync(language);
                _cachedMirrors[language] = mirror;
                return mirror;
            }
            catch (Exception ex)
            {
                _logService.Warn($"获取 {language} 包管理器镜像源失败: {ex.Message}");
                _cachedMirrors[language] = _languageService.GetString("Common_DetectFailed");
                return _languageService.GetString("Common_DetectFailed");
            }
        }

        private async Task<string> GetCurrentMirrorInternalAsync(SdkLanguage language)
        {
            return language switch
            {
                SdkLanguage.Python => await GetPipMirrorAsync(),
                SdkLanguage.NodeJs => await GetNpmMirrorAsync(),
                SdkLanguage.Java => GetMavenMirror(),
                _ => _languageService.GetString("Common_NotSupported")
            };
        }

        public async Task<bool> SetMirrorAsync(SdkLanguage language, string mirrorUrl)
        {
            try
            {
                var success = language switch
                {
                    SdkLanguage.Python => await SetPipMirrorAsync(mirrorUrl),
                    SdkLanguage.NodeJs => await SetNpmMirrorAsync(mirrorUrl),
                    SdkLanguage.Java => SetMavenMirror(mirrorUrl),
                    _ => false
                };
                if (success)
                {
                    _cachedMirrors[language] = mirrorUrl;
                }
                return success;
            }
            catch (Exception ex)
            {
                _logService.Error($"设置 {language} 包管理器镜像源失败: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ResetToDefaultAsync(SdkLanguage language)
        {
            try
            {
                var defaultUrl = language switch
                {
                    SdkLanguage.Python => "https://pypi.org/simple",
                    SdkLanguage.NodeJs => "https://registry.npmjs.org",
                    _ => null
                };
                var success = language switch
                {
                    SdkLanguage.Python => await SetPipMirrorAsync("https://pypi.org/simple"),
                    SdkLanguage.NodeJs => await SetNpmMirrorAsync("https://registry.npmjs.org"),
                    SdkLanguage.Java => SetMavenMirror(null),
                    _ => false
                };
                if (success && defaultUrl != null)
                {
                    _cachedMirrors[language] = defaultUrl;
                }
                return success;
            }
            catch (Exception ex)
            {
                _logService.Error($"重置 {language} 包管理器镜像源失败: {ex.Message}");
                return false;
            }
        }

        public IEnumerable<PresetMirrorItem> GetPresetMirrors(SdkLanguage language)
        {
            return language switch
            {
                SdkLanguage.Python => new List<PresetMirrorItem>
                {
                    new PresetMirrorItem { Name = _languageService.GetString("Mirror_Tsinghua"), Url = "https://pypi.tuna.tsinghua.edu.cn/simple" },
                    new PresetMirrorItem { Name = _languageService.GetString("Mirror_Aliyun"), Url = "https://mirrors.aliyun.com/pypi/simple" },
                    new PresetMirrorItem { Name = _languageService.GetString("Mirror_USTC"), Url = "https://pypi.mirrors.ustc.edu.cn/simple" },
                    new PresetMirrorItem { Name = _languageService.GetString("Mirror_Tencent"), Url = "https://mirrors.cloud.tencent.com/pypi/simple" },
                    new PresetMirrorItem { Name = _languageService.GetString("Mirror_Huawei"), Url = "https://repo.huaweicloud.com/repository/pypi/simple" },
                    new PresetMirrorItem { Name = "PyPI 官方", Url = "https://pypi.org/simple" }
                },
                SdkLanguage.NodeJs => new List<PresetMirrorItem>
                {
                    new PresetMirrorItem { Name = _languageService.GetString("Mirror_NPMMirror"), Url = "https://registry.npmmirror.com" },
                    new PresetMirrorItem { Name = _languageService.GetString("Mirror_Tencent"), Url = "https://mirrors.cloud.tencent.com/npm/" },
                    new PresetMirrorItem { Name = _languageService.GetString("Mirror_Huawei"), Url = "https://repo.huaweicloud.com/repository/npm/" },
                    new PresetMirrorItem { Name = "npm 官方", Url = "https://registry.npmjs.org" }
                },
                SdkLanguage.Java => new List<PresetMirrorItem>
                {
                    new PresetMirrorItem { Name = _languageService.GetString("Mirror_Aliyun"), Url = "https://maven.aliyun.com/repository/public" },
                    new PresetMirrorItem { Name = _languageService.GetString("Mirror_Huawei"), Url = "https://repo.huaweicloud.com/repository/maven/" },
                    new PresetMirrorItem { Name = _languageService.GetString("Mirror_Tencent"), Url = "https://mirrors.cloud.tencent.com/nexus/repository/maven-public/" },
                    new PresetMirrorItem { Name = "Maven 中央仓库", Url = "https://repo.maven.apache.org/maven2" }
                },
                _ => new List<PresetMirrorItem>()
            };
        }

        #region pip

        /// <summary>
        /// 获取 Python 安装路径（从 PYTHON_HOME 环境变量）
        /// </summary>
        private string GetPythonInstallPath() => GetHomePath("PYTHON_HOME");

        /// <summary>
        /// 获取 pip 可执行文件路径
        /// </summary>
        private string GetPipPath()
        {
            var pythonHome = GetPythonInstallPath();
            if (!string.IsNullOrEmpty(pythonHome))
            {
                // embeddable 版本 pip 在 Scripts 子目录下
                var pipExe = Path.Combine(pythonHome, "Scripts", "pip.exe");
                if (File.Exists(pipExe)) return pipExe;
                // 也可能在安装目录下（非 embeddable 版本）
                pipExe = Path.Combine(pythonHome, "pip.exe");
                if (File.Exists(pipExe)) return pipExe;
            }
            return "pip"; // 回退到 PATH 中的 pip
        }

        private async Task<string> GetPipMirrorAsync()
        {
            var pipPath = GetPipPath();
            var pythonHome = GetPythonInstallPath();
            var result = await RunProcessWithEnvAsync(pipPath, "config get global.index-url", pythonHome, "PYTHON_HOME");
            if (string.IsNullOrEmpty(result) || result.Contains("ERROR"))
                return _languageService.GetString("Mirror_PythonNotConfigured");
            return result.Trim();
        }

        private async Task<bool> SetPipMirrorAsync(string url)
        {
            var pipPath = GetPipPath();
            var pythonHome = GetPythonInstallPath();
            var result = await RunProcessWithEnvAsync(pipPath, $"config set global.index-url {url}", pythonHome, "PYTHON_HOME");
            return !result.Contains("ERROR");
        }

        /// <summary>
        /// 执行外部进程命令并设置环境变量（确保子进程能找到依赖的可执行文件）
        /// </summary>
        private async Task<string> RunProcessWithEnvAsync(string fileName, string arguments, string homePath, string homeEnvName)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                if (!string.IsNullOrEmpty(homePath))
                {
                    psi.WorkingDirectory = homePath;
                    var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
                    var pathsToAdd = homePath;
                    // Python 需要额外添加 Scripts 目录到 PATH
                    if (homeEnvName == "PYTHON_HOME")
                        pathsToAdd += ";" + Path.Combine(homePath, "Scripts");

                    if (pathEnv.IndexOf(homePath, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        psi.EnvironmentVariables["PATH"] = pathsToAdd + ";" + pathEnv;
                    }
                    psi.EnvironmentVariables[homeEnvName] = homePath;
                }

                using var process = Process.Start(psi);
                if (process == null) return "";

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                process.WaitForExit(30000);

                return string.IsNullOrEmpty(output) ? error : output;
            }
            catch
            {
                return "";
            }
        }

        #endregion

        #region npm

        /// <summary>
        /// 获取 Node.js 安装路径（从 NODE_HOME 环境变量）
        /// </summary>
        private string GetNodeInstallPath() => GetHomePath("NODE_HOME");

        /// <summary>
        /// 获取 npm 可执行文件路径
        /// </summary>
        private string GetNpmPath()
        {
            var nodeHome = GetNodeInstallPath();
            if (!string.IsNullOrEmpty(nodeHome))
            {
                var npmCmd = Path.Combine(nodeHome, "npm.cmd");
                if (File.Exists(npmCmd)) return npmCmd;
                var npmExe = Path.Combine(nodeHome, "npm.exe");
                if (File.Exists(npmExe)) return npmExe;
            }
            return "npm"; // 回退到 PATH 中的 npm
        }

        private async Task<string> GetNpmMirrorAsync()
        {
            var npmPath = GetNpmPath();
            var nodeHome = GetNodeInstallPath();
            var result = await RunProcessWithEnvAsync(npmPath, "config get registry", nodeHome, "NODE_HOME");
            if (string.IsNullOrEmpty(result) || result.Contains("undefined"))
                return _languageService.GetString("Mirror_NpmNotConfigured");
            return result.Trim();
        }

        private async Task<bool> SetNpmMirrorAsync(string url)
        {
            var npmPath = GetNpmPath();
            var nodeHome = GetNodeInstallPath();
            var result = await RunProcessWithEnvAsync(npmPath, $"config set registry {url}", nodeHome, "NODE_HOME");
            return !result.Contains("ERR!");
        }

        #endregion

        #region Maven

        private string GetMavenMirror()
        {
            // 优先检查用户级 settings.xml
            var userSettingsPath = GetMavenUserSettingsPath();
            if (File.Exists(userSettingsPath))
            {
                var mirror = ParseMavenMirrorFromFile(userSettingsPath);
                if (mirror != null) return mirror;
            }

            // 再检查 Maven 安装目录下的全局 settings.xml
            var globalSettingsPath = GetMavenGlobalSettingsPath();
            if (File.Exists(globalSettingsPath))
            {
                var mirror = ParseMavenMirrorFromFile(globalSettingsPath);
                if (mirror != null) return mirror;
            }

            // Maven 已安装但未配置镜像
            if (IsMavenInstalled())
                return _languageService.GetString("Mirror_MavenNotConfigured");

            return _languageService.GetString("Common_MavenNotInstalled");
        }

        private static string ParseMavenMirrorFromFile(string settingsPath)
        {
            try
            {
                var content = File.ReadAllText(settingsPath);
                var mirrorsStart = content.IndexOf("<mirrors>", StringComparison.OrdinalIgnoreCase);
                if (mirrorsStart < 0) return null;

                var mirrorStart = content.IndexOf("<mirror>", mirrorsStart, StringComparison.OrdinalIgnoreCase);
                if (mirrorStart < 0) return null;

                var startTag = "<url>";
                var endTag = "</url>";
                var urlStart = content.IndexOf(startTag, mirrorStart, StringComparison.OrdinalIgnoreCase);
                if (urlStart < 0) return null;

                urlStart += startTag.Length;
                var urlEnd = content.IndexOf(endTag, urlStart, StringComparison.OrdinalIgnoreCase);
                if (urlEnd < 0) return null;

                return content.Substring(urlStart, urlEnd - urlStart).Trim();
            }
            catch
            {
                return null;
            }
        }

        private static bool IsMavenInstalled()
        {
            var mavenHome = Environment.GetEnvironmentVariable("MAVEN_HOME");
            if (!string.IsNullOrEmpty(mavenHome))
                return true;

            mavenHome = Environment.GetEnvironmentVariable("MAVEN_HOME", EnvironmentVariableTarget.User);
            if (!string.IsNullOrEmpty(mavenHome))
                return true;

            mavenHome = Environment.GetEnvironmentVariable("MAVEN_HOME", EnvironmentVariableTarget.Machine);
            if (!string.IsNullOrEmpty(mavenHome))
                return true;

            return false;
        }

        private static string GetMavenUserSettingsPath()
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userProfile, ".m2", "settings.xml");
        }

        private static string GetMavenGlobalSettingsPath()
        {
            var mavenHome = Environment.GetEnvironmentVariable("MAVEN_HOME");
            if (string.IsNullOrEmpty(mavenHome))
                mavenHome = Environment.GetEnvironmentVariable("MAVEN_HOME", EnvironmentVariableTarget.User);
            if (string.IsNullOrEmpty(mavenHome))
                mavenHome = Environment.GetEnvironmentVariable("MAVEN_HOME", EnvironmentVariableTarget.Machine);
            if (string.IsNullOrEmpty(mavenHome))
                return null;

            return Path.Combine(mavenHome.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), "conf", "settings.xml");
        }

        private bool SetMavenMirror(string mirrorUrl)
        {
            var settingsPath = GetMavenUserSettingsPath();
            var dir = Path.GetDirectoryName(settingsPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (mirrorUrl == null)
            {
                // 重置：删除 mirrors 节点
                if (!File.Exists(settingsPath)) return true;
                var content = File.ReadAllText(settingsPath);
                var mirrorsStart = content.IndexOf("<mirrors>", StringComparison.OrdinalIgnoreCase);
                var mirrorsEnd = content.IndexOf("</mirrors>", StringComparison.OrdinalIgnoreCase);
                if (mirrorsStart >= 0 && mirrorsEnd >= 0)
                {
                    content = content.Remove(mirrorsStart, mirrorsEnd + "</mirrors>".Length - mirrorsStart);
                    File.WriteAllText(settingsPath, content);
                }
                return true;
            }

            var mirrorXml = $@"
  <mirrors>
    <mirror>
      <id>sdk-manager-mirror</id>
      <mirrorOf>central</mirrorOf>
      <name>SDK Manager Mirror</name>
      <url>{mirrorUrl}</url>
    </mirror>
  </mirrors>";

            if (!File.Exists(settingsPath))
            {
                var newContent = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<settings xmlns=""http://maven.apache.org/SETTINGS/1.2.0""
          xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
          xsi:schemaLocation=""http://maven.apache.org/SETTINGS/1.2.0 https://maven.apache.org/xsd/settings-1.2.0.xsd"">
{mirrorXml}
</settings>";
                File.WriteAllText(settingsPath, newContent);
                return true;
            }

            // 替换已有的 mirrors 节点
            var existingContent = File.ReadAllText(settingsPath);
            var existMirrorsStart = existingContent.IndexOf("<mirrors>", StringComparison.OrdinalIgnoreCase);
            var existMirrorsEnd = existingContent.IndexOf("</mirrors>", StringComparison.OrdinalIgnoreCase);

            if (existMirrorsStart >= 0 && existMirrorsEnd >= 0)
            {
                existingContent = existingContent.Remove(existMirrorsStart, existMirrorsEnd + "</mirrors>".Length - existMirrorsStart);
                existingContent = existingContent.Insert(existMirrorsStart, mirrorXml);
            }
            else
            {
                // 在 </settings> 前插入
                var settingsEnd = existingContent.IndexOf("</settings>", StringComparison.OrdinalIgnoreCase);
                if (settingsEnd >= 0)
                {
                    existingContent = existingContent.Insert(settingsEnd, mirrorXml + "\n");
                }
            }

            File.WriteAllText(settingsPath, existingContent);
            return true;
        }

        public string GetMavenLocalRepository()
        {
            // Check user-level settings.xml first
            var userSettingsPath = GetMavenUserSettingsPath();
            if (File.Exists(userSettingsPath))
            {
                var localRepo = ParseLocalRepositoryFromFile(userSettingsPath);
                if (localRepo != null) return localRepo;
            }

            // Check global settings.xml
            var globalSettingsPath = GetMavenGlobalSettingsPath();
            if (globalSettingsPath != null && File.Exists(globalSettingsPath))
            {
                var localRepo = ParseLocalRepositoryFromFile(globalSettingsPath);
                if (localRepo != null) return localRepo;
            }

            // Return default path
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userProfile, ".m2", "repository");
        }

        private static string ParseLocalRepositoryFromFile(string settingsPath)
        {
            try
            {
                var content = File.ReadAllText(settingsPath);
                var startTag = "<localRepository>";
                var endTag = "</localRepository>";
                var start = content.IndexOf(startTag, StringComparison.OrdinalIgnoreCase);
                if (start < 0) return null;
                start += startTag.Length;
                var end = content.IndexOf(endTag, start, StringComparison.OrdinalIgnoreCase);
                if (end < 0) return null;
                return content.Substring(start, end - start).Trim();
            }
            catch
            {
                return null;
            }
        }

        public bool SetMavenLocalRepository(string path)
        {
            try
            {
                var settingsPath = GetMavenUserSettingsPath();
                var dir = Path.GetDirectoryName(settingsPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string content;
                if (File.Exists(settingsPath))
                {
                    content = File.ReadAllText(settingsPath);
                }
                else
                {
                    content = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<settings xmlns=\"http://maven.apache.org/SETTINGS/1.2.0\"\n         xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"\n         xsi:schemaLocation=\"http://maven.apache.org/SETTINGS/1.2.0 https://maven.apache.org/xsd/settings-1.2.0.xsd\">\n</settings>";
                }

                var startTag = "<localRepository>";
                var endTag = "</localRepository>";
                var start = content.IndexOf(startTag, StringComparison.OrdinalIgnoreCase);

                if (start >= 0)
                {
                    // Replace existing localRepository
                    var end = content.IndexOf(endTag, start, StringComparison.OrdinalIgnoreCase);
                    if (end >= 0)
                    {
                        content = content.Substring(0, start) + $"{startTag}{path}{endTag}" + content.Substring(end + endTag.Length);
                    }
                }
                else
                {
                    // Insert localRepository before closing </settings>
                    var settingsEnd = content.IndexOf("</settings>", StringComparison.OrdinalIgnoreCase);
                    if (settingsEnd >= 0)
                    {
                        content = content.Substring(0, settingsEnd) + $"  {startTag}{path}{endTag}\n" + content.Substring(settingsEnd);
                    }
                }

                File.WriteAllText(settingsPath, content);
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Common Helpers

        /// <summary>
        /// 从环境变量获取 HOME 路径（依次检查：当前进程 → 用户级 → 系统级）
        /// </summary>
        private static string GetHomePath(string envName)
        {
            var home = Environment.GetEnvironmentVariable(envName);
            if (!string.IsNullOrEmpty(home))
                return home.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            home = Environment.GetEnvironmentVariable(envName, EnvironmentVariableTarget.User);
            if (!string.IsNullOrEmpty(home))
                return home.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            home = Environment.GetEnvironmentVariable(envName, EnvironmentVariableTarget.Machine);
            if (!string.IsNullOrEmpty(home))
                return home.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return null;
        }

        #endregion
    }
}
