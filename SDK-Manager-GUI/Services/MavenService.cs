using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using SDK_Manager_GUI.Models;
using SDK_Manager_GUI.Services;

namespace SDK_Manager_GUI.Services
{
    /// <summary>
    /// Maven 下载、安装、卸载服务
    /// </summary>
    public class MavenService : IMavenService
    {
        private HttpClient _httpClient;
        private HttpClient _downloadHttpClient;
        private readonly IEnvironmentManager _environmentManager;
        private readonly IConfigService _configService;
        private readonly ILogService _logService;
        private readonly ILanguageService _languageService;

        public MavenService(IConfigService configService, IEnvironmentManager environmentManager, ILogService logService, ILanguageService languageService)
        {
            _environmentManager = environmentManager;
            _configService = configService;
            _logService = logService;
            _languageService = languageService;
            _httpClient = SdkProviderHelper.CreateHttpClient(TimeSpan.FromSeconds(30));
            _downloadHttpClient = SdkProviderHelper.CreateHttpClient(TimeSpan.FromMinutes(30));
            _mirrorConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "maven-download-mirrors.json");
            _downloadMirrors = LoadDownloadMirrors();
        }



        /// <summary>
        /// 从 Apache Maven 仓库获取可用版本列表
        /// </summary>
        public async Task<IEnumerable<MavenVersion>> GetAvailableVersionsAsync()
        {
            var versions = new List<MavenVersion>();

            // 构建版本列表 URL 列表：先尝试镜像源，最后回退到官方源
            var metadataUrls = new List<(string Name, string Url)>();
            var enabledMirrors = _downloadMirrors.Where(m => m.IsEnabled).ToList();
            foreach (var mirror in enabledMirrors)
            {
                var metadataUrl = $"{mirror.BaseUrl.TrimEnd('/')}/org/apache/maven/apache-maven/maven-metadata.xml";
                metadataUrls.Add((mirror.Name, metadataUrl));
            }
            // 官方源作为最后回退
            metadataUrls.Add((_languageService.GetString("Mirror_Official"), "https://repo.maven.apache.org/maven2/org/apache/maven/apache-maven/maven-metadata.xml"));

            Exception lastError = null;
            foreach (var (name, url) in metadataUrls)
            {
                try
                {
                    var response = await _httpClient.GetStringAsync(url);
                    var doc = XDocument.Parse(response);

                    var versionElements = doc.Descendants("version");
                    foreach (var ve in versionElements)
                    {
                        var ver = ve.Value.Trim();
                        // 只保留正式版本（不包含 alpha/beta 等）
                        if (ver.IndexOf("alpha", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            ver.IndexOf("beta", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            ver.IndexOf("milestone", StringComparison.OrdinalIgnoreCase) >= 0)
                            continue;

                        // 避免重复
                        if (versions.Any(v => v.Version == ver)) continue;

                        versions.Add(new MavenVersion
                        {
                            Version = ver,
                            DownloadUrl = $"https://repo.maven.apache.org/maven2/org/apache/maven/apache-maven/{ver}/apache-maven-{ver}-bin.zip"
                        });
                    }

                    if (versions.Count > 0)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    _logService.Warn($"从 [{name}] 获取 Maven 版本列表失败: {ex.Message}");
                    continue;
                }
            }

            if (versions.Count == 0 && lastError != null)
            {
                _logService.Warn($"所有源均无法获取 Maven 版本列表，最后错误: {lastError.Message}");
            }

            // 按语义版本降序排列
            versions = versions.OrderByDescending(v => v.Version, new SDK_Manager_GUI.Services.SemanticVersionComparer()).ToList();

            return versions;
        }

        /// <summary>
        /// 获取 Maven 镜像下载 URL（清华镜像）
        /// </summary>
        public string GetMirrorDownloadUrl(string version, string mirrorBaseUrl)
        {
            if (string.IsNullOrEmpty(mirrorBaseUrl) || mirrorBaseUrl.Contains("repo.maven.apache.org"))
                return $"https://repo.maven.apache.org/maven2/org/apache/maven/apache-maven/{version}/apache-maven-{version}-bin.zip";

            // 清华镜像路径
            return $"{mirrorBaseUrl.TrimEnd('/')}/org/apache/maven/apache-maven/{version}/apache-maven-{version}-bin.zip";
        }

        /// <summary>
        /// 检测已安装的 Maven 版本
        /// </summary>
        public async Task<MavenDetectionResult> DetectMavenAsync()
        {
            var result = new MavenDetectionResult();

            // 1. 从 MAVEN_HOME 环境变量检测
            var userMavenHome = await _environmentManager.GetEnvironmentVariableAsync("MAVEN_HOME", false);
            var systemMavenHome = await _environmentManager.GetEnvironmentVariableAsync("MAVEN_HOME", true);

            if (!string.IsNullOrEmpty(userMavenHome))
            {
                userMavenHome = userMavenHome.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                result.UserLevelPath = userMavenHome;
                result.IsUserLevelInstalled = true;
                result.UserLevelVersion = await DetectVersionFromPathAsync(userMavenHome);
            }

            if (!string.IsNullOrEmpty(systemMavenHome))
            {
                systemMavenHome = systemMavenHome.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                result.SystemLevelPath = systemMavenHome;
                result.IsSystemLevelInstalled = true;
                result.SystemLevelVersion = await DetectVersionFromPathAsync(systemMavenHome);
            }

            // 2. 从 PATH 中检测 mvn
            if (!result.IsUserLevelInstalled && !result.IsSystemLevelInstalled)
            {
                var mvnPath = await FindMvnInPathAsync();
                if (mvnPath != null)
                {
                    result.IsInstalledViaPath = true;
                    result.PathMvnLocation = mvnPath;
                    // 从 mvn --version 获取版本
                    result.PathMvnVersion = await GetMvnVersionFromCommandAsync();
                }
            }

            return result;
        }

        /// <summary>
        /// 安装 Maven：下载、解压、配置环境变量
        /// </summary>
        public async Task InstallMavenAsync(string version, string downloadUrl, IProgress<InstallProgress> progress, bool systemLevel = false)
        {
            if (systemLevel && !EnvironmentManager.IsRunningAsAdmin())
            {
                throw new InvalidOperationException(_languageService.GetString("Dialog_SystemInstallNeedAdmin"));
            }

            var config = await _configService.GetConfigAsync();
            var installBasePath = config.DefaultInstallPath;
            var mavenDir = Path.Combine(installBasePath, "maven", version);

            // 缓存目录
            var cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "Maven");
            if (!Directory.Exists(cacheDir))
                Directory.CreateDirectory(cacheDir);

            var cacheFile = Path.Combine(cacheDir, $"apache-maven-{version}-bin.zip");

            progress?.Report(new InstallProgress { Percent = 0, Message = _languageService.GetString("Progress_PreparingDownload") });

            // 下载（支持断点续传）
            long existingBytes = 0;
            bool cacheIsValid = false;
            if (File.Exists(cacheFile))
            {
                existingBytes = new FileInfo(cacheFile).Length;
                if (existingBytes > 0)
                {
                    // 验证已有缓存文件是否完整（尝试作为ZIP打开）
                    try
                    {
                        using (var archive = System.IO.Compression.ZipFile.OpenRead(cacheFile))
                        {
                            cacheIsValid = archive.Entries.Count > 0;
                        }
                    }
                    catch
                    {
                        cacheIsValid = false;
                    }

                    if (cacheIsValid)
                    {
                        _logService.Info($"发现完整的 Maven 下载缓存 ({existingBytes / 1024 / 1024}MB)，跳过下载");
                    }
                    else
                    {
                        _logService.Info($"发现不完整的 Maven 下载缓存 ({existingBytes / 1024 / 1024}MB)，尝试断点续传...");
                    }
                }
                else
                {
                    File.Delete(cacheFile);
                    existingBytes = 0;
                }
            }

            if (!cacheIsValid)
            {
                // 构建下载 URL 列表：先尝试已启用的镜像源，最后回退到原始 URL
                var downloadUrls = new List<(string Name, string Url)>();
                var enabledMirrors = _downloadMirrors.Where(m => m.IsEnabled).ToList();
                foreach (var mirror in enabledMirrors)
                {
                    downloadUrls.Add((mirror.Name, BuildDownloadUrl(version, mirror.BaseUrl)));
                }
                // 如果原始 URL 不在镜像列表中，作为最后回退
                if (!downloadUrls.Any(u => u.Url == downloadUrl))
                {
                    downloadUrls.Add((_languageService.GetString("Mirror_Official"), downloadUrl));
                }

                Exception lastError = null;
                bool downloaded = false;

                foreach (var (name, url) in downloadUrls)
                {
                    try
                    {
                        progress?.Report(new InstallProgress { Percent = 5, Message = string.Format(_languageService.GetString("Progress_DownloadingFrom"), name, version) });

                        var request = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);
                        // 断点续传：如果已有部分下载的文件，设置 Range 头
                        if (existingBytes > 0 && File.Exists(cacheFile))
                        {
                            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existingBytes, null);
                        }

                        using var response = await _downloadHttpClient.SendAsync(request, System.Net.Http.HttpCompletionOption.ResponseHeadersRead);

                        // 检查服务器是否支持断点续传（返回 206 Partial Content）
                        bool isResuming = response.StatusCode == System.Net.HttpStatusCode.PartialContent;

                        if (!isResuming && existingBytes > 0)
                        {
                            // 服务器不支持断点续传，从头开始下载
                            existingBytes = 0;
                        }

                        response.EnsureSuccessStatusCode();

                        var totalBytes = response.Content.Headers.ContentLength ?? 0;
                        var actualTotalBytes = isResuming ? existingBytes + totalBytes : totalBytes;

                        var fileMode = isResuming ? FileMode.Append : FileMode.Create;
                        using var stream = await response.Content.ReadAsStreamAsync();
                        using var fileStream = new FileStream(cacheFile, fileMode, FileAccess.Write, FileShare.None);

                        var buffer = new byte[81920];
                        long bytesRead = existingBytes;
                        int read;
                        int lastReportedPercent = actualTotalBytes > 0 ? 5 + (int)(bytesRead * 55.0 / actualTotalBytes) : 5;
                        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, read);
                            bytesRead += read;

                            if (actualTotalBytes > 0)
                            {
                                var percent = 5 + (int)(bytesRead * 55.0 / actualTotalBytes);
                                // 节流：仅在百分比变化时报告进度，避免UI线程过载
                                if (percent != lastReportedPercent)
                                {
                                    lastReportedPercent = percent;
                                    progress?.Report(new InstallProgress { Percent = percent, Message = string.Format(_languageService.GetString("Progress_DownloadingFrom"), name, version) + $" ({bytesRead / 1024 / 1024}MB / {actualTotalBytes / 1024 / 1024}MB)" });
                                }
                            }
                        }

                        downloaded = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logService.Warn($"从 [{name}] 下载 Maven {version} 失败: {ex.Message}");
                        lastError = ex;
                        // 注意：断点续传时不删除部分下载的缓存文件，以便下次重试
                        if (existingBytes == 0)
                        {
                            try { if (File.Exists(cacheFile)) File.Delete(cacheFile); } catch { }
                        }
                    }
                }

                if (!downloaded)
                {
                    throw new InvalidOperationException(string.Format(_languageService.GetString("Dialog_DownloadAllMirrorsFailed"), version, lastError?.Message));
                }
            }
            else
            {
                progress?.Report(new InstallProgress { Percent = 60, Message = _languageService.GetString("Progress_UsingCache") });
            }

            // 解压前验证 ZIP 文件完整性
            progress?.Report(new InstallProgress { Percent = 62, Message = _languageService.GetString("Progress_VerifyingFile") });
            try
            {
                await Task.Run(() =>
                {
                    using (var archive = System.IO.Compression.ZipFile.OpenRead(cacheFile))
                    {
                        if (archive.Entries.Count == 0)
                            throw new InvalidDataException("ZIP 文件为空");
                    }
                });
            }
            catch (InvalidDataException ex)
            {
                try { if (File.Exists(cacheFile)) File.Delete(cacheFile); } catch { }
                throw new InvalidOperationException(string.Format(_languageService.GetString("Dialog_ZipCorrupted"), ex.Message));
            }

            // 解压
            progress?.Report(new InstallProgress { Percent = 65, Message = _languageService.GetString("Progress_Extracting") });

            var tempExtractDir = Path.Combine(Path.GetTempPath(), $"maven-install-{Guid.NewGuid():N}");
            try
            {
                await Task.Run(() =>
                {
                    System.IO.Compression.ZipFile.ExtractToDirectory(cacheFile, tempExtractDir);
                });

                // 检查解压结果：通常只有一个子目录 apache-maven-{version}
                var dirs = Directory.GetDirectories(tempExtractDir);
                var sourceDir = (dirs.Length == 1 && Directory.GetFiles(tempExtractDir).Length == 0)
                    ? dirs[0]
                    : tempExtractDir;

                // 清理并创建安装目录
                if (Directory.Exists(mavenDir))
                {
                    try { DeleteDirectoryRobust(mavenDir); } catch { }
                }
                Directory.CreateDirectory(mavenDir);

                // 清除源目录中所有文件的只读属性
                ClearReadOnlyAttributes(sourceDir);

                // 复制所有文件和目录到安装路径（支持跨卷操作）
                foreach (var f in Directory.GetFiles(sourceDir))
                {
                    var dest = Path.Combine(mavenDir, Path.GetFileName(f));
                    var fi = new FileInfo(f);
                    fi.IsReadOnly = false;
                    File.Copy(f, dest, true);
                }
                foreach (var d in Directory.GetDirectories(sourceDir))
                {
                    var dest = Path.Combine(mavenDir, Path.GetFileName(d));
                    CopyDirectory(d, dest);
                }
            }
            finally
            {
                try { if (Directory.Exists(tempExtractDir)) DeleteDirectoryRobust(tempExtractDir); } catch { }
            }

            // 配置环境变量
            progress?.Report(new InstallProgress { Percent = 90, Message = _languageService.GetString("Progress_ConfiguringEnv") });

            await _environmentManager.BackupEnvironmentVariablesAsync();

            // 移除旧版本的 PATH 条目
            var oldMavenHome = await _environmentManager.GetEnvironmentVariableAsync("MAVEN_HOME", systemLevel);
            if (!string.IsNullOrEmpty(oldMavenHome))
            {
                var oldBinPath = Path.Combine(oldMavenHome.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), "bin");
                await _environmentManager.RemoveFromPathAsync(oldBinPath, systemLevel);
            }

            // 设置 MAVEN_HOME
            await _environmentManager.SetEnvironmentVariableAsync("MAVEN_HOME", mavenDir, systemLevel);

            // 添加 bin 到 PATH
            var binPath = Path.Combine(mavenDir, "bin");
            try
            {
                await _environmentManager.AddToPathAsync(binPath, systemLevel);
            }
            catch (InvalidOperationException) when (systemLevel)
            {
                // 系统级 PATH 添加失败（权限不足），回退到用户级
                await _environmentManager.AddToPathAsync(binPath, false);
                // 同时将 MAVEN_HOME 也改为用户级，保持一致性
                await _environmentManager.SetEnvironmentVariableAsync("MAVEN_HOME", mavenDir, false);
            }

            _environmentManager.RefreshCurrentProcessEnvironment();

            // 验证安装
            var mvnCmd = Path.Combine(mavenDir, "bin", "mvn.cmd");
            if (!File.Exists(mvnCmd))
            {
                throw new InvalidOperationException(_languageService.GetString("Dialog_MvnNotFound"));
            }

            progress?.Report(new InstallProgress { Percent = 100, Message = "Maven 安装完成" });
            _logService.Info($"Maven {version} 安装完成，路径: {mavenDir}");
        }

        /// <summary>
        /// 卸载 Maven
        /// </summary>
        public async Task UninstallMavenAsync(bool systemLevel)
        {
            var envName = "MAVEN_HOME";
            var mavenHome = await _environmentManager.GetEnvironmentVariableAsync(envName, systemLevel);

            if (string.IsNullOrEmpty(mavenHome))
            {
                _logService.Warn($"未找到 {(systemLevel ? "系统级" : "用户级")} MAVEN_HOME 环境变量");
                return;
            }

            mavenHome = mavenHome.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // 从 PATH 中移除 bin 目录
            var binPath = Path.Combine(mavenHome, "bin");
            await _environmentManager.RemoveFromPathAsync(binPath, systemLevel);

            // 删除 MAVEN_HOME 环境变量
            await _environmentManager.SetEnvironmentVariableAsync(envName, null, systemLevel);

            // 检查另一个级别是否还在使用同一安装路径
            var otherLevelPath = systemLevel
                ? await _environmentManager.GetEnvironmentVariableAsync(envName, false)
                : await _environmentManager.GetEnvironmentVariableAsync(envName, true);

            var otherLevelIsUsingThisPath = false;
            if (!string.IsNullOrEmpty(otherLevelPath))
            {
                var trimmed = otherLevelPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                otherLevelIsUsingThisPath = string.Equals(trimmed, mavenHome, StringComparison.OrdinalIgnoreCase);
            }

            _environmentManager.RefreshCurrentProcessEnvironment();

            // 只有另一个级别没有在使用同一安装路径时，才删除目录
            if (!otherLevelIsUsingThisPath && Directory.Exists(mavenHome))
            {
                await Task.Run(() => DeleteDirectoryRobust(mavenHome));
                _logService.Info($"Maven 目录已清理: {mavenHome}");
            }
            else
            {
                _logService.Info($"Maven 目录保留（{(systemLevel ? "用户级" : "系统级")}仍在使用）: {mavenHome}");
            }

            _logService.Info($"Maven {(systemLevel ? "系统级" : "用户级")}卸载完成");
        }

        /// <summary>
        /// 从安装路径检测 Maven 版本
        /// </summary>
        private async Task<string> DetectVersionFromPathAsync(string mavenHome)
        {
            // 方法1：从 lib 目录中的 maven-core JAR 文件名提取版本
            try
            {
                var libDir = Path.Combine(mavenHome, "lib");
                if (Directory.Exists(libDir))
                {
                    var mavenCoreJar = Directory.GetFiles(libDir, "maven-core-*.jar").FirstOrDefault();
                    if (mavenCoreJar != null)
                    {
                        var fileName = Path.GetFileNameWithoutExtension(mavenCoreJar);
                        var version = fileName.Substring("maven-core-".Length);
                        return version;
                    }
                }
            }
            catch { }

            // 方法2：执行 mvn --version
            var mvnCmd = Path.Combine(mavenHome, "bin", "mvn.cmd");
            if (File.Exists(mvnCmd))
            {
                var version = await GetMvnVersionFromCommandAsync(mvnCmd);
                if (!string.IsNullOrEmpty(version))
                    return version;
            }

            // 方法3：从目录名提取版本
            var dirName = Path.GetFileName(mavenHome);
            if (dirName.StartsWith("apache-maven-"))
                return dirName.Substring("apache-maven-".Length);

            return null;
        }

        /// <summary>
        /// 执行 mvn --version 获取版本号
        /// </summary>
        private async Task<string> GetMvnVersionFromCommandAsync(string mvnCmdPath = null)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = mvnCmdPath ?? "mvn",
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                // 设置 JAVA_HOME 确保 mvn 能运行
                var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
                if (string.IsNullOrEmpty(javaHome))
                    javaHome = Environment.GetEnvironmentVariable("JAVA_HOME", EnvironmentVariableTarget.User);
                if (string.IsNullOrEmpty(javaHome))
                    javaHome = Environment.GetEnvironmentVariable("JAVA_HOME", EnvironmentVariableTarget.Machine);

                if (!string.IsNullOrEmpty(javaHome))
                {
                    psi.EnvironmentVariables["JAVA_HOME"] = javaHome;
                    var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
                    var javaBin = Path.Combine(javaHome, "bin");
                    if (pathEnv.IndexOf(javaBin, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        psi.EnvironmentVariables["PATH"] = javaBin + ";" + pathEnv;
                    }
                }

                using var process = System.Diagnostics.Process.Start(psi);
                if (process == null) return null;

                var output = await process.StandardOutput.ReadToEndAsync();
                process.WaitForExit(15000);

                // 输出格式: "Apache Maven 3.9.6 (..."
                var match = System.Text.RegularExpressions.Regex.Match(output, @"Apache Maven\s+(\S+)");
                if (match.Success)
                    return match.Groups[1].Value;

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 在 PATH 中查找 mvn.cmd 的位置
        /// </summary>
        private async Task<string> FindMvnInPathAsync()
        {
            try
            {
                var pathValue = await _environmentManager.GetEnvironmentVariableAsync("PATH", false);
                if (string.IsNullOrEmpty(pathValue))
                    pathValue = await _environmentManager.GetEnvironmentVariableAsync("PATH", true);

                if (string.IsNullOrEmpty(pathValue)) return null;

                foreach (var dir in pathValue.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var mvnCmd = Path.Combine(dir, "mvn.cmd");
                    if (File.Exists(mvnCmd))
                        return Path.GetDirectoryName(mvnCmd); // 返回 bin 目录的父目录
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// 检查指定 Maven 版本是否有本地缓存
        /// </summary>
        public Task<bool> HasCacheAsync(string version)
        {
            var cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "Maven");
            var cacheFile = Path.Combine(cacheDir, $"apache-maven-{version}-bin.zip");
            return Task.FromResult(File.Exists(cacheFile));
        }

        /// <summary>
        /// 递归复制目录（支持跨卷操作）
        /// </summary>
        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var f in Directory.GetFiles(sourceDir))
            {
                var dest = Path.Combine(destDir, Path.GetFileName(f));
                try
                {
                    var fi = new FileInfo(f);
                    fi.IsReadOnly = false;
                    File.Copy(f, dest, true);
                }
                catch (IOException)
                {
                    for (int retry = 0; retry < 3; retry++)
                    {
                        Thread.Sleep(300);
                        try
                        {
                            var fi = new FileInfo(f);
                            fi.IsReadOnly = false;
                            File.Copy(f, dest, true);
                            break;
                        }
                        catch { }
                    }
                }
            }
            foreach (var d in Directory.GetDirectories(sourceDir))
            {
                CopyDirectory(d, Path.Combine(destDir, Path.GetFileName(d)));
            }
        }

        private static void ClearReadOnlyAttributes(string directory)
        {
            try
            {
                var di = new DirectoryInfo(directory);
                di.Attributes = FileAttributes.Normal;
                foreach (var fi in di.GetFiles("*", SearchOption.AllDirectories))
                {
                    fi.Attributes = FileAttributes.Normal;
                }
                foreach (var subDi in di.GetDirectories("*", SearchOption.AllDirectories))
                {
                    subDi.Attributes = FileAttributes.Normal;
                }
            }
            catch { }
        }

        private static void DeleteDirectoryRobust(string path)
        {
            if (!Directory.Exists(path)) return;
            ClearReadOnlyAttributes(path);
            Directory.Delete(path, true);
        }

        #region Maven 下载镜像源管理

        private List<MavenDownloadMirror> _downloadMirrors;
        private readonly string _mirrorConfigPath;
        private readonly object _saveLock = new object();

        private List<MavenDownloadMirror> GetDefaultDownloadMirrors()
        {
            return new List<MavenDownloadMirror>
            {
                new MavenDownloadMirror { Id = "maven-official", Name = "Apache 官方", BaseUrl = "https://repo.maven.apache.org/maven2", IsEnabled = true, IsDefault = true, IsPreset = true },
                new MavenDownloadMirror { Id = "maven-aliyun", Name = _languageService.GetString("Mirror_Aliyun"), BaseUrl = "https://maven.aliyun.com/repository/public", IsEnabled = true, IsDefault = false, IsPreset = true },
                new MavenDownloadMirror { Id = "maven-huawei", Name = _languageService.GetString("Mirror_Huawei"), BaseUrl = "https://repo.huaweicloud.com/repository/maven/", IsEnabled = true, IsDefault = false, IsPreset = true },
                new MavenDownloadMirror { Id = "maven-tencent", Name = _languageService.GetString("Mirror_Tencent"), BaseUrl = "https://mirrors.cloud.tencent.com/nexus/repository/maven-public/", IsEnabled = true, IsDefault = false, IsPreset = true },
            };
        }

        private List<MavenDownloadMirror> LoadDownloadMirrors()
        {
            try
            {
                if (File.Exists(_mirrorConfigPath))
                {
                    var json = File.ReadAllText(_mirrorConfigPath);
                    var mirrors = Newtonsoft.Json.JsonConvert.DeserializeObject<List<MavenDownloadMirror>>(json);
                    if (mirrors != null && mirrors.Count > 0)
                    {
                        var defaults = GetDefaultDownloadMirrors();
                        var presetIds = defaults.Select(m => m.Id).ToHashSet();

                        // 移除已失效的旧镜像源
                        var deprecatedIds = new HashSet<string>
                        {
                            "maven-tsinghua",  // 清华 Maven 镜像已停止服务
                        };
                        mirrors.RemoveAll(m => deprecatedIds.Contains(m.Id));

                        // 修复旧配置中缺少 IsPreset 字段的预置镜像
                        foreach (var m in mirrors)
                        {
                            if (presetIds.Contains(m.Id))
                                m.IsPreset = true;
                        }
                        return mirrors;
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.Warn($"加载 Maven 下载镜像源配置失败: {ex.Message}");
            }
            return GetDefaultDownloadMirrors();
        }

        private void SaveDownloadMirrors()
        {
            lock (_saveLock)
            {
                try
                {
                    var dir = Path.GetDirectoryName(_mirrorConfigPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(_downloadMirrors, Newtonsoft.Json.Formatting.Indented);
                    File.WriteAllText(_mirrorConfigPath, json);
                }
                catch (Exception ex)
                {
                    _logService.Warn($"保存 Maven 下载镜像源配置失败: {ex.Message}");
                }
            }
        }

        public Task<IEnumerable<MavenDownloadMirror>> GetDownloadMirrorsAsync()
        {
            return Task.FromResult(_downloadMirrors.AsEnumerable());
        }

        public Task AddDownloadMirrorAsync(MavenDownloadMirror mirror)
        {
            if (mirror == null) throw new ArgumentNullException(nameof(mirror));
            if (string.IsNullOrEmpty(mirror.Id))
                mirror.Id = $"custom-{Guid.NewGuid():N}".Substring(0, 16);
            mirror.BaseUrl = mirror.BaseUrl.TrimEnd('/');
            _downloadMirrors.Add(mirror);
            SaveDownloadMirrors();
            return Task.CompletedTask;
        }

        public Task RemoveDownloadMirrorAsync(string mirrorId)
        {
            var mirror = _downloadMirrors.FirstOrDefault(m => m.Id == mirrorId);
            if (mirror != null && !mirror.IsDefault)
            {
                _downloadMirrors.Remove(mirror);
                SaveDownloadMirrors();
            }
            return Task.CompletedTask;
        }

        public Task UpdateDownloadMirrorAsync(MavenDownloadMirror mirror)
        {
            if (mirror == null) return Task.CompletedTask;
            var existing = _downloadMirrors.FirstOrDefault(m => m.Id == mirror.Id);
            if (existing != null)
            {
                existing.Name = mirror.Name;
                existing.BaseUrl = mirror.BaseUrl.TrimEnd('/');
                existing.IsEnabled = mirror.IsEnabled;
                existing.Latency = mirror.Latency;
                existing.LastSuccess = mirror.LastSuccess;
                existing.LastUsedTime = mirror.LastUsedTime;
                SaveDownloadMirrors();
            }
            return Task.CompletedTask;
        }

        public async Task TestDownloadMirrorLatencyAsync(MavenDownloadMirror mirror)
        {
            try
            {
                var testUrl = BuildDownloadUrl("3.9.6", mirror.BaseUrl);
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                var sw = System.Diagnostics.Stopwatch.StartNew();
                using var response = await _httpClient.GetAsync(testUrl, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cts.Token);
                sw.Stop();
                mirror.Latency = sw.ElapsedMilliseconds;
                mirror.LastSuccess = response.IsSuccessStatusCode;
                mirror.LastUsedTime = DateTime.Now;
            }
            catch
            {
                mirror.Latency = -1;
                mirror.LastSuccess = false;
                mirror.LastUsedTime = DateTime.Now;
            }
            await UpdateDownloadMirrorAsync(mirror);
        }

        /// <summary>
        /// 根据镜像源构建 Maven 下载 URL
        /// </summary>
        public string BuildDownloadUrl(string version, string mirrorBaseUrl)
        {
            if (string.IsNullOrEmpty(mirrorBaseUrl))
                return $"https://repo.maven.apache.org/maven2/org/apache/maven/apache-maven/{version}/apache-maven-{version}-bin.zip";

            var baseTrimmed = mirrorBaseUrl.TrimEnd('/');
            return $"{baseTrimmed}/org/apache/maven/apache-maven/{version}/apache-maven-{version}-bin.zip";
        }

        /// <summary>
        /// 测试所有 Maven 下载镜像源延迟，不可达的自动禁用
        /// </summary>
        public async Task TestAndDisableUnreachableMirrorsAsync()
        {
            var allMirrors = _downloadMirrors.ToList();
            var testTasks = allMirrors.Select(async mirror =>
            {
                try
                {
                    await TestDownloadMirrorLatencyAsync(mirror);
                }
                catch
                {
                    mirror.Latency = -1;
                    mirror.LastSuccess = false;
                }
            }).ToList();

            await Task.WhenAll(testTasks);

            // 禁用不可达的镜像（非默认镜像）
            bool changed = false;
            foreach (var mirror in allMirrors)
            {
                if (mirror.LastSuccess == false && mirror.IsEnabled && !mirror.IsDefault)
                {
                    mirror.IsEnabled = false;
                    changed = true;
                }
            }

            if (changed)
            {
                SaveDownloadMirrors();
            }
        }

        #endregion
    }
}
