using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using SDK_Manager_GUI.Models;
using SDK_Manager_GUI.ViewModels;
using SDK_Manager_GUI.Services;

namespace SDK_Manager_GUI.Services
{
    public class SdkManagerService : ISdkManagerService
    {
        private readonly IEnumerable<ISdkProvider> _providers;
        private readonly IDownloadEngine _downloadEngine;
        private readonly IEnvironmentManager _environmentManager;
        private readonly IMirrorProvider _mirrorProvider;
        private readonly IConfigService _configService;
        private readonly ILogService _logService;
        private readonly ILanguageService _languageService;

        // 版本列表缓存
        private readonly Dictionary<SdkLanguage, List<SdkVersion>> _versionCache = new Dictionary<SdkLanguage, List<SdkVersion>>();
        private readonly Dictionary<SdkLanguage, DateTime> _cacheTimestamp = new Dictionary<SdkLanguage, DateTime>();
        private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(1);

        public SdkManagerService(
            IEnumerable<ISdkProvider> providers,
            IDownloadEngine downloadEngine,
            IEnvironmentManager environmentManager,
            IMirrorProvider mirrorProvider,
            IConfigService configService,
            ILogService logService,
            ILanguageService languageService)
        {
            _providers = providers;
            _downloadEngine = downloadEngine;
            _environmentManager = environmentManager;
            _mirrorProvider = mirrorProvider;
            _configService = configService;
            _logService = logService;
            _languageService = languageService;
        }

        public async Task<IEnumerable<SdkVersion>> GetAvailableVersionsAsync(SdkLanguage language)
        {
            // 检查缓存是否有效
            if (_versionCache.TryGetValue(language, out var cached) && cached.Count > 0)
            {
                if (_cacheTimestamp.TryGetValue(language, out var timestamp) &&
                    DateTime.Now - timestamp < CacheExpiration)
                {
                    return cached;
                }
            }

            // 缓存无效，重新获取
            var provider = GetProvider(language);
            var versions = (await provider.GetAvailableVersionsAsync())
                .OrderByDescending(v => v.Version, new SemanticVersionComparer())
                .ToList();

            _versionCache[language] = versions;
            _cacheTimestamp[language] = DateTime.Now;

            return versions;
        }

        /// <summary>
        /// 强制刷新版本列表缓存
        /// </summary>
        public async Task<IEnumerable<SdkVersion>> RefreshAvailableVersionsAsync(SdkLanguage language)
        {
            var provider = GetProvider(language);
            var versions = (await provider.GetAvailableVersionsAsync())
                .OrderByDescending(v => v.Version, new SemanticVersionComparer())
                .ToList();

            _versionCache[language] = versions;
            _cacheTimestamp[language] = DateTime.Now;

            return versions;
        }

        /// <summary>
        /// 检查版本列表是否已缓存
        /// </summary>
        public bool HasCachedVersions(SdkLanguage language)
        {
            return _versionCache.TryGetValue(language, out var cached) && cached.Count > 0;
        }

        public async Task<IEnumerable<InstalledSdk>> GetInstalledVersionsAsync(SdkLanguage language)
        {
            var provider = GetProvider(language);
            var installed = new List<InstalledSdk>();

            var config = await _configService.GetConfigAsync();
            var installBasePath = EnsureWritablePath(config.DefaultInstallPath);
            var langBasePath = Path.Combine(installBasePath, language.ToString());

            // 获取用户级和系统级环境变量路径，用于判断安装级别
            var envName = provider.GetEnvironmentVariableName();
            var userEnvPath = await _environmentManager.GetEnvironmentVariableAsync(envName, false);
            var systemEnvPath = await _environmentManager.GetEnvironmentVariableAsync(envName, true);
            if (!string.IsNullOrEmpty(userEnvPath))
                userEnvPath = userEnvPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.IsNullOrEmpty(systemEnvPath))
                systemEnvPath = systemEnvPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (!Directory.Exists(langBasePath))
                return installed;

            // 扫描版本子目录：{basePath}/{Language}/{version}/
            foreach (var versionDir in Directory.GetDirectories(langBasePath))
            {
                var versionName = Path.GetFileName(versionDir);
                if (IsInstallationValid(language, versionDir))
                {
                    var detectedVersion = await DetectVersionFromExecutableAsync(language, versionDir);
                    var isActive = await IsInstallPathActiveAsync(language, versionDir);
                    var trimmedDir = versionDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                    installed.Add(new InstalledSdk
                    {
                        Language = language,
                        Version = detectedVersion ?? versionName,
                        InstallPath = versionDir,
                        IsActive = isActive,
                        InstallDate = Directory.GetCreationTime(versionDir),
                        IsUserLevel = string.Equals(trimmedDir, userEnvPath, StringComparison.OrdinalIgnoreCase),
                        IsSystemLevel = string.Equals(trimmedDir, systemEnvPath, StringComparison.OrdinalIgnoreCase)
                    });
                }
            }

            return installed;
        }

        /// <summary>
        /// 通过执行文件检测已安装 SDK 的版本号
        /// </summary>
        private async Task<string> DetectVersionFromExecutableAsync(SdkLanguage language, string installPath)
        {
            var provider = GetProvider(language);
            var exeName = provider.GetExecutableName();
            var exePath = language == SdkLanguage.Java
                ? Path.Combine(installPath, "bin", exeName)
                : Path.Combine(installPath, exeName);

            if (!File.Exists(exePath)) return null;

            var rawVersion = await ExecuteCommandForVersionAsync($"\"{exePath}\"", provider.GetVersionArgument());
            if (string.IsNullOrEmpty(rawVersion)) return null;

            return NormalizeDetectedVersion(rawVersion.Trim(), language);
        }

        /// <summary>
        /// 检查指定安装路径是否为当前活跃版本
        /// </summary>
        private async Task<bool> IsInstallPathActiveAsync(SdkLanguage language, string installPath)
        {
            var provider = GetProvider(language);
            var envName = provider.GetEnvironmentVariableName();

            var envValue = await _environmentManager.GetEnvironmentVariableAsync(envName, false);
            if (string.IsNullOrEmpty(envValue))
                envValue = await _environmentManager.GetEnvironmentVariableAsync(envName, true);

            if (string.IsNullOrEmpty(envValue)) return false;

            envValue = envValue.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(envValue, installPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 格式化下载速度
        /// </summary>
        private static string FormatSpeed(long bytesPerSecond)
        {
            if (bytesPerSecond <= 0) return "";
            if (bytesPerSecond < 1024) return $"{bytesPerSecond} B/s";
            if (bytesPerSecond < 1024 * 1024) return $"{bytesPerSecond / 1024.0:F1} KB/s";
            return $"{bytesPerSecond / (1024.0 * 1024):F1} MB/s";
        }

        /// <summary>
        /// 执行 npm 命令，使用指定安装路径下的 npm
        /// </summary>
        private async Task<(string output, string error, int exitCode)> RunNpmCommandAsync(string installPath, string arguments)
        {
            // 优先使用安装目录下的 npm.cmd，确保使用正确的 Node.js 版本
            var npmCmd = Path.Combine(installPath, "npm.cmd");
            var npmExe = Path.Combine(installPath, "npm.exe");
            var fileName = File.Exists(npmCmd) ? npmCmd
                         : File.Exists(npmExe) ? npmExe
                         : "npm";

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = installPath
            };

            // 确保子进程能找到 node.exe
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            if (pathEnv.IndexOf(installPath, StringComparison.OrdinalIgnoreCase) < 0)
            {
                psi.EnvironmentVariables["PATH"] = installPath + ";" + pathEnv;
            }
            psi.EnvironmentVariables["NODE_HOME"] = installPath;

            using var process = System.Diagnostics.Process.Start(psi);
            if (process == null) return ("", _languageService.GetString("Dialog_CannotStartProcess"), -1);

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            process.WaitForExit(30000);

            return (output.Trim(), error.Trim(), process.ExitCode);
        }

        /// <summary>
        /// 修改 Python embeddable 版本的 _pth 文件，启用 site 模块并添加 site-packages 路径
        /// </summary>
        private void ConfigurePthFile(string installPath, string _version)
        {
            // _pth 文件名格式：python312._pth（版本号去掉点）
            var pthFiles = Directory.GetFiles(installPath, "*._pth");

            foreach (var pthFile in pthFiles)
            {
                var content = File.ReadAllText(pthFile);
                // 取消注释 import site
                content = content.Replace("# import site", "import site");
                // 添加 Lib\site-packages 路径（如果不存在）
                if (!content.Contains("Lib\\site-packages") && !content.Contains("Lib/site-packages"))
                {
                    content = content.TrimEnd() + "\r\nLib\\site-packages\r\n";
                }
                File.WriteAllText(pthFile, content);
                _logService.Info($"已修改 _pth 文件: {pthFile}");
            }
        }

        /// <summary>
        /// 下载 get-pip.py 并使用嵌入版 Python 安装 pip
        /// </summary>
        private async Task InstallPipAsync(string installPath, IProgress<InstallProgress> _progress)
        {
            var getPipPath = Path.Combine(installPath, "get-pip.py");

            // 下载 get-pip.py
            try
            {
                using var client = new System.Net.Http.HttpClient();
                client.Timeout = TimeSpan.FromSeconds(60);
                var getPipContent = await client.GetByteArrayAsync("https://bootstrap.pypa.io/get-pip.py");
                File.WriteAllBytes(getPipPath, getPipContent);
                _logService.Info($"已下载 get-pip.py 到: {getPipPath}");
            }
            catch (Exception ex)
            {
                _logService.Warn($"下载 get-pip.py 失败: {ex.Message}，尝试使用缓存");
                // 如果下载失败，检查缓存中是否有
                var cachedGetPip = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "get-pip.py");
                if (File.Exists(cachedGetPip))
                {
                    File.Copy(cachedGetPip, getPipPath, true);
                }
                else
                {
                    throw new InvalidOperationException(_languageService.GetString("Dialog_GetPipFailed"));
                }
            }

            // 使用嵌入版 Python 执行 get-pip.py 安装 pip
            var pythonExe = Path.Combine(installPath, "python.exe");
            if (!File.Exists(pythonExe))
            {
                throw new InvalidOperationException(string.Format(_languageService.GetString("Dialog_PythonNotFound"), pythonExe));
            }

            var psi = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = $"\"{getPipPath}\" --no-warn-script-location",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = installPath
            };

            // 确保 Python 子进程能找到自身
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            if (pathEnv.IndexOf(installPath, StringComparison.OrdinalIgnoreCase) < 0)
            {
                psi.EnvironmentVariables["PATH"] = installPath + ";" + pathEnv;
            }
            psi.EnvironmentVariables["PYTHON_HOME"] = installPath;

            using var process = Process.Start(psi);
            if (process == null)
            {
                throw new InvalidOperationException(_languageService.GetString("Dialog_CannotStartPip"));
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            process.WaitForExit(60000);

            _logService.Info($"pip 安装结果: exit={process.ExitCode}, out={output}, err={error}");

            if (process.ExitCode != 0)
            {
                _logService.Warn($"pip 安装失败: {error}");
            }

            // 缓存 get-pip.py 供后续使用
            try
            {
                var cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache");
                if (!Directory.Exists(cacheDir))
                    Directory.CreateDirectory(cacheDir);
                var cachedGetPip = Path.Combine(cacheDir, "get-pip.py");
                if (File.Exists(getPipPath) && !File.Exists(cachedGetPip))
                {
                    File.Copy(getPipPath, cachedGetPip);
                }
            }
            catch { }
        }

        /// <summary>
        /// 下载完整安装包并补全 Tcl/Tk (tkinter) 及可选的 IDLE 文件
        /// embeddable 版本缺少 _tkinter.pyd、tcl/tk DLL、Lib/tkinter/、Lib/idlelib/，
        /// 需从完整安装包中提取并复制到 embeddable 目录
        /// </summary>
        private async Task InstallTclTkAsync(string installPath, string version, IProgress<InstallProgress> _progress, bool includeIdle)
        {
            var installerUrl = PythonSdkProvider.GetInstallerUrl(version);
            var tempInstallerPath = Path.Combine(Path.GetTempPath(), $"python-{version}-installer.exe");
            var tempInstallDir = Path.Combine(Path.GetTempPath(), $"python-{version}-tcltk-extract");

            try
            {
                // 1. 下载完整安装包（支持缓存）
                _logService.Info($"正在下载 Python 完整安装包: {installerUrl}");
                bool downloaded = false;
                try
                {
                    using var client = new System.Net.Http.HttpClient();
                    client.Timeout = TimeSpan.FromMinutes(10);
                    var data = await client.GetByteArrayAsync(installerUrl);
                    File.WriteAllBytes(tempInstallerPath, data);
                    downloaded = true;
                    _logService.Info($"Python 安装包下载完成: {tempInstallerPath}");
                }
                catch (Exception ex)
                {
                    _logService.Warn($"下载安装包失败: {ex.Message}，尝试使用缓存");
                }

                if (!downloaded)
                {
                    var cachedInstaller = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", $"python-{version}-amd64.exe");
                    if (File.Exists(cachedInstaller))
                    {
                        File.Copy(cachedInstaller, tempInstallerPath, true);
                        downloaded = true;
                    }
                }

                if (!downloaded)
                {
                    throw new InvalidOperationException(_languageService.GetString("Dialog_InstallerDownloadFailed"));
                }

                // 缓存安装包供后续使用
                try
                {
                    var cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache");
                    if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);
                    var cachedInstaller = Path.Combine(cacheDir, $"python-{version}-amd64.exe");
                    if (!File.Exists(cachedInstaller))
                    {
                        File.Copy(tempInstallerPath, cachedInstaller);
                    }
                }
                catch { }

                // 2. 静默安装到临时目录（仅安装 Tcl/Tk 相关组件）
                if (Directory.Exists(tempInstallDir))
                    DeleteDirectoryRobust(tempInstallDir);

                var psi = new ProcessStartInfo
                {
                    FileName = tempInstallerPath,
                    Arguments = $"/quiet InstallAllUsers=0 TargetDir=\"{tempInstallDir}\" Include_tcltk=1 Include_pip=0 Include_idle={(includeIdle ? 1 : 0)} Include_doc=0 Include_test=0 Include_dev=0 Include_launcher=0 Include_tools=0 SimpleInstall=1",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                _logService.Info($"正在静默安装 Python 到临时目录以提取 Tcl/Tk: {tempInstallDir}");
                using var process = Process.Start(psi);
                if (process == null)
                {
                    throw new InvalidOperationException(_languageService.GetString("Dialog_CannotStartInstaller"));
                }
                process.WaitForExit(300000); // 最多等待 5 分钟

                if (process.ExitCode != 0)
                {
                    _logService.Warn($"临时安装退出码: {process.ExitCode}");
                }

                // 3. 复制 Tcl/Tk 相关文件到 embeddable 目录
                var tempDllsDir = Path.Combine(tempInstallDir, "DLLs");
                var tempLibDir = Path.Combine(tempInstallDir, "Lib");

                // _tkinter.pyd（C 扩展模块）
                var tkInterSource = Path.Combine(tempDllsDir, "_tkinter.pyd");
                if (File.Exists(tkInterSource))
                {
                    File.Copy(tkInterSource, Path.Combine(installPath, "_tkinter.pyd"), true);
                    _logService.Info("已复制 _tkinter.pyd");
                }

                // tcl86t.dll, tk86t.dll（Tcl/Tk 运行时 DLL）
                foreach (var dll in new[] { "tcl86t.dll", "tk86t.dll" })
                {
                    var source = Path.Combine(tempDllsDir, dll);
                    if (File.Exists(source))
                    {
                        File.Copy(source, Path.Combine(installPath, dll), true);
                        _logService.Info($"已复制 {dll}");
                    }
                }

                // tcl/ 目录（Tcl/Tk 标准库脚本）
                var tclSourceDir = Path.Combine(tempDllsDir, "tcl");
                if (Directory.Exists(tclSourceDir))
                {
                    var tclDestDir = Path.Combine(installPath, "tcl");
                    if (Directory.Exists(tclDestDir)) DeleteDirectoryRobust(tclDestDir);
                    CopyDirectoryRobust(tclSourceDir, tclDestDir);
                    _logService.Info("已复制 tcl/ 目录");
                }

                // Lib/tkinter/（Python tkinter 包）
                var tkinterSourceDir = Path.Combine(tempLibDir, "tkinter");
                if (Directory.Exists(tkinterSourceDir))
                {
                    var libDir = Path.Combine(installPath, "Lib");
                    if (!Directory.Exists(libDir)) Directory.CreateDirectory(libDir);
                    var tkinterDestDir = Path.Combine(libDir, "tkinter");
                    if (Directory.Exists(tkinterDestDir)) DeleteDirectoryRobust(tkinterDestDir);
                    CopyDirectoryRobust(tkinterSourceDir, tkinterDestDir);
                    _logService.Info("已复制 Lib/tkinter/");
                }

                // Lib/idlelib/（IDLE 编辑器，依赖 Tcl/Tk）
                if (includeIdle)
                {
                    var idlelibSourceDir = Path.Combine(tempLibDir, "idlelib");
                    if (Directory.Exists(idlelibSourceDir))
                    {
                        var libDir = Path.Combine(installPath, "Lib");
                        if (!Directory.Exists(libDir)) Directory.CreateDirectory(libDir);
                        var idlelibDestDir = Path.Combine(libDir, "idlelib");
                        if (Directory.Exists(idlelibDestDir)) DeleteDirectoryRobust(idlelibDestDir);
                        CopyDirectoryRobust(idlelibSourceDir, idlelibDestDir);
                        _logService.Info("已复制 Lib/idlelib/");
                    }
                }

                // 4. 更新 _pth 文件，添加 Lib 路径（tkinter/idlelib 需要被 import 找到）
                EnsureLibInPthFile(installPath);
            }
            finally
            {
                // 5. 清理临时安装目录和安装包
                try { if (Directory.Exists(tempInstallDir)) DeleteDirectoryRobust(tempInstallDir); } catch { }
                try { if (File.Exists(tempInstallerPath)) File.Delete(tempInstallerPath); } catch { }
            }
        }

        /// <summary>
        /// 确保 _pth 文件中包含 Lib 路径，使 tkinter/idlelib 等可被 import
        /// </summary>
        private void EnsureLibInPthFile(string installPath)
        {
            var pthFiles = Directory.GetFiles(installPath, "*._pth");
            foreach (var pthFile in pthFiles)
            {
                var content = File.ReadAllText(pthFile);
                var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).Select(l => l.Trim()).ToList();

                // 检查是否已存在 Lib 路径（精确匹配 Lib 或 Lib\）
                bool hasLib = lines.Any(l => l.Equals("Lib", StringComparison.OrdinalIgnoreCase) ||
                                              l.Equals("Lib\\", StringComparison.OrdinalIgnoreCase) ||
                                              l.Equals("Lib/", StringComparison.OrdinalIgnoreCase));
                if (hasLib) continue;

                // 在 Lib\site-packages 之前插入 Lib
                var newLines = new List<string>();
                bool inserted = false;
                foreach (var line in lines)
                {
                    if (!inserted && (line.Equals("Lib\\site-packages", StringComparison.OrdinalIgnoreCase) ||
                                       line.Equals("Lib/site-packages", StringComparison.OrdinalIgnoreCase)))
                    {
                        newLines.Add("Lib");
                        inserted = true;
                    }
                    newLines.Add(line);
                }

                if (!inserted)
                {
                    // 没找到 site-packages 行，追加到末尾（import site 之前）
                    var finalLines = new List<string>();
                    foreach (var line in newLines)
                    {
                        if (line.Equals("import site", StringComparison.OrdinalIgnoreCase) && !inserted)
                        {
                            finalLines.Add("Lib");
                            inserted = true;
                        }
                        finalLines.Add(line);
                    }
                    newLines = inserted ? finalLines : newLines;
                    if (!inserted) newLines.Add("Lib");
                }

                File.WriteAllText(pthFile, string.Join("\r\n", newLines));
                _logService.Info($"已在 _pth 中添加 Lib 路径: {pthFile}");
            }
        }

        /// <summary>
        /// 配置 IDLE 启动器（创建 idle.bat / idle.pyw，确保 pythonw.exe 存在）
        /// IDLE 依赖 Tcl/Tk 和 idlelib，需先执行 InstallTclTkAsync
        /// </summary>
        private void ConfigureIdle(string installPath)
        {
            // 确保 idlelib 目录存在
            var idlelibDir = Path.Combine(installPath, "Lib", "idlelib");
            if (!Directory.Exists(idlelibDir))
            {
                _logService.Warn("idlelib 目录不存在，无法配置 IDLE（请确保已安装 Tcl/Tk）");
                return;
            }

            // 检查 pythonw.exe（embeddable 版本通常已包含）
            var pythonwExe = Path.Combine(installPath, "pythonw.exe");
            if (!File.Exists(pythonwExe))
            {
                _logService.Warn("pythonw.exe 不存在，IDLE 将使用 python.exe 启动（会显示控制台窗口）");
            }

            // 创建 idle.bat 启动脚本（双击即可启动 IDLE）
            var idleBatPath = Path.Combine(installPath, "idle.bat");
            var idleLauncher = File.Exists(pythonwExe) ? "pythonw.exe" : "python.exe";
            var idleBatContent = $"@echo off\r\n\"%~dp0{idleLauncher}\" -m idlelib.idle %*\r\n";
            File.WriteAllText(idleBatPath, idleBatContent);
            _logService.Info($"已创建 IDLE 启动器: {idleBatPath}");

            // 创建 idle.pyw 作为备选启动方式（.pyw 不会弹出控制台）
            var idlePywPath = Path.Combine(installPath, "idle.pyw");
            File.WriteAllText(idlePywPath, "import idlelib.idle\r\n");
            _logService.Info($"已创建 IDLE 启动脚本: {idlePywPath}");
        }

        /// <summary>
        /// 将 Python 注册到 Windows 注册表
        /// 注册位置：SOFTWARE\Python\PythonCore\{version}\InstallPath
        /// 这样其他工具（如 py launcher、IDE）可以发现此 Python 安装
        /// </summary>
        private void RegisterPythonInRegistry(string installPath, string version, bool systemLevel)
        {
            var rootKey = systemLevel ? Registry.LocalMachine : Registry.CurrentUser;
            var pythonKeyPath = $@"SOFTWARE\Python\PythonCore\{version}";
            var installPathKeyPath = $@"{pythonKeyPath}\InstallPath";

            try
            {
                using (var pythonKey = rootKey.CreateSubKey(pythonKeyPath))
                {
                    pythonKey.SetValue("DisplayName", $"Python {version}");
                    pythonKey.SetValue("SupportUrl", "https://www.python.org/");
                    pythonKey.SetValue("Version", version);
                    // SysVersion 为主版本号.次版本号，如 3.12
                    pythonKey.SetValue("SysVersion", version.Length >= 3 ? version.Substring(0, 3) : version);
                    pythonKey.SetValue("Architecture", "64bit");
                }

                using (var installPathKey = rootKey.CreateSubKey(installPathKeyPath))
                {
                    installPathKey.SetValue("", installPath, RegistryValueKind.String);
                    installPathKey.SetValue("ExecutablePath", Path.Combine(installPath, "python.exe"), RegistryValueKind.String);
                    installPathKey.SetValue("ExecutablewPath", Path.Combine(installPath, "pythonw.exe"), RegistryValueKind.String);
                    installPathKey.SetValue("WindowedExecutablePath", Path.Combine(installPath, "pythonw.exe"), RegistryValueKind.String);
                }

                _logService.Info($"已将 Python {version} 注册到 {(systemLevel ? "HKLM" : "HKCU")}\\{pythonKeyPath}");
            }
            catch (UnauthorizedAccessException)
            {
                _logService.Warn($"注册 Python 到注册表失败：权限不足（系统级注册需要管理员权限）");
                throw new InvalidOperationException(_languageService.GetString("Dialog_SystemInstallNeedAdmin"));
            }
        }

        /// <summary>
        /// 关联 .py / .pyw / .pyc 文件到 Python 解释器
        /// 注册位置：SOFTWARE\Classes\.py 和 SOFTWARE\Classes\Python.File
        /// </summary>
        private void AssociatePythonFiles(string installPath, bool systemLevel)
        {
            var rootKey = systemLevel ? Registry.LocalMachine : Registry.CurrentUser;
            var classesPath = @"SOFTWARE\Classes";
            var pythonExe = Path.Combine(installPath, "python.exe");
            var pythonwExe = Path.Combine(installPath, "pythonw.exe");

            try
            {
                // .py 文件关联 → Python.File（控制台程序）
                using (var dotPyKey = rootKey.CreateSubKey($@"{classesPath}\.py"))
                {
                    dotPyKey.SetValue("", "Python.File", RegistryValueKind.String);
                    dotPyKey.SetValue("Content Type", "text/plain", RegistryValueKind.String);
                }

                using (var fileKey = rootKey.CreateSubKey($@"{classesPath}\Python.File"))
                {
                    fileKey.SetValue("", "Python File", RegistryValueKind.String);
                    using (var shellKey = fileKey.CreateSubKey("shell"))
                    using (var openKey = shellKey.CreateSubKey("open"))
                    using (var cmdKey = openKey.CreateSubKey("command"))
                    {
                        cmdKey.SetValue("", $"\"{pythonExe}\" \"%1\" %*", RegistryValueKind.String);
                    }
                }

                // .pyw 文件关联 → Python.NoConFile（无控制台程序）
                using (var dotPywKey = rootKey.CreateSubKey($@"{classesPath}\.pyw"))
                {
                    dotPywKey.SetValue("", "Python.NoConFile", RegistryValueKind.String);
                    dotPywKey.SetValue("Content Type", "text/plain", RegistryValueKind.String);
                }

                using (var fileKey = rootKey.CreateSubKey($@"{classesPath}\Python.NoConFile"))
                {
                    fileKey.SetValue("", "Python Windowed File", RegistryValueKind.String);
                    using (var shellKey = fileKey.CreateSubKey("shell"))
                    using (var openKey = shellKey.CreateSubKey("open"))
                    using (var cmdKey = openKey.CreateSubKey("command"))
                    {
                        cmdKey.SetValue("", $"\"{pythonwExe}\" \"%1\" %*", RegistryValueKind.String);
                    }
                }

                // .pyc 文件关联 → Python.CompiledFile
                using (var dotPycKey = rootKey.CreateSubKey($@"{classesPath}\.pyc"))
                {
                    dotPycKey.SetValue("", "Python.CompiledFile", RegistryValueKind.String);
                }

                using (var fileKey = rootKey.CreateSubKey($@"{classesPath}\Python.CompiledFile"))
                {
                    fileKey.SetValue("", "Python Compiled File", RegistryValueKind.String);
                    using (var shellKey = fileKey.CreateSubKey("shell"))
                    using (var openKey = shellKey.CreateSubKey("open"))
                    using (var cmdKey = openKey.CreateSubKey("command"))
                    {
                        cmdKey.SetValue("", $"\"{pythonExe}\" \"%1\" %*", RegistryValueKind.String);
                    }
                }

                _logService.Info($"已关联 .py/.pyw/.pyc 文件到 {(systemLevel ? "HKLM" : "HKCU")} (python={pythonExe})");
            }
            catch (UnauthorizedAccessException)
            {
                _logService.Warn($"关联 .py 文件失败：权限不足（系统级关联需要管理员权限）");
                throw new InvalidOperationException(_languageService.GetString("Dialog_SystemInstallNeedAdmin"));
            }
        }

        public async Task<InstalledSdk> GetActiveVersionAsync(SdkLanguage language)
        {
            var provider = GetProvider(language);
            var envName = provider.GetEnvironmentVariableName();

            // 同时检查用户级和系统级环境变量，优先使用用户级
            var envValue = await _environmentManager.GetEnvironmentVariableAsync(envName, false);
            if (string.IsNullOrEmpty(envValue))
                envValue = await _environmentManager.GetEnvironmentVariableAsync(envName, true);

            if (string.IsNullOrEmpty(envValue))
                return null;

            // 去除末尾路径分隔符
            envValue = envValue.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (!Directory.Exists(envValue))
                return null;

            // 通过执行文件检测实际版本号，不依赖文件夹名称
            var detectedVersion = await DetectVersionFromExecutableAsync(language, envValue);

            return new InstalledSdk
            {
                Language = language,
                Version = detectedVersion ?? Path.GetFileName(envValue),
                InstallPath = envValue,
                IsActive = true
            };
        }

        public async Task<InstalledSdk> InstallAsync(SdkLanguage language, string version, IProgress<InstallProgress> progress, CancellationToken cancellationToken, bool systemLevel = false)
        {
            var provider = GetProvider(language);
            var config = await _configService.GetConfigAsync();

            // 使用传入的 systemLevel 覆盖配置
            var useSystemLevel = systemLevel;

            // 权限检查：系统级安装需要管理员权限
            if (useSystemLevel && !EnvironmentManager.IsRunningAsAdmin())
            {
                throw new InvalidOperationException(_languageService.GetString("Dialog_SystemInstallNeedAdmin"));
            }

            _logService.Info($"开始安装 {language} {version}");

            // 确保安装基础路径可写
            var installBasePath = EnsureWritablePath(config.DefaultInstallPath);
            // 安装路径：{basePath}/{Language}/{version}/，支持多版本共存
            var installPath = Path.Combine(installBasePath, language.ToString(), version);

            // 下载缓存目录：程序目录/cache/{language}/
            var cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", language.ToString());
            if (!Directory.Exists(cacheDir))
                Directory.CreateDirectory(cacheDir);

            // 所有 SDK 统一使用 ZIP 压缩包分发，解压即用
            var cacheFileExt = ".zip";
            var cacheFile = Path.Combine(cacheDir, $"{language}_{version}{cacheFileExt}");

            progress?.Report(new InstallProgress { Percent = 0, Message = _languageService.GetString("Progress_PreparingDownload") });

            // 如果缓存文件已存在，跳过下载
            if (File.Exists(cacheFile))
            {
                progress?.Report(new InstallProgress { Percent = 60, Message = _languageService.GetString("Progress_UsingCache") });
            }
            else
            {
                // 按连接效率排序镜像：延迟低 + 成功率高的排前面
                var mirrors = await _mirrorProvider.GetMirrorsAsync(language);
                var enabledMirrors = mirrors.Where(m => m.IsEnabled).ToList();

                // 按效率排序：优先级 > 延迟 > 成功率
                enabledMirrors = enabledMirrors
                    .OrderBy(m => m.Priority)
                    .ThenBy(m => m.Latency ?? int.MaxValue)
                    .ThenByDescending(m => m.LastSuccess == true ? 1 : (m.LastSuccess == false ? -1 : 0))
                    .ThenBy(m => m.FailCount)
                    .ToList();

                // 将最优镜像排到最前面
                var bestMirror = await _mirrorProvider.GetBestMirrorAsync(language);
                if (bestMirror != null)
                {
                    enabledMirrors = enabledMirrors.Where(m => m.Id != bestMirror.Id).ToList();
                    enabledMirrors.Insert(0, bestMirror);
                }

                bool downloadSuccess = false;
                string lastError = "";
                string usedMirrorName = "";
                string usedMirrorId = "";
                var triedMirrors = new System.Collections.Generic.List<string>();

                foreach (var mirror in enabledMirrors)
                {
                    try
                    {
                        triedMirrors.Add(mirror.Name);
                        var downloadUrl = await provider.GetDownloadUrlAsync(version, mirror.BaseUrl);
                        usedMirrorName = mirror.Name;
                        usedMirrorId = mirror.Id;
                        progress?.Report(new InstallProgress { Percent = 5, Message = string.Format(_languageService.GetString("Progress_DownloadingFrom"), mirror.Name, triedMirrors.Count), MirrorName = mirror.Name });

                        var downloadProgress = new Progress<DownloadProgressInfo>(p =>
                        {
                            var percent = 5 + p.Percent * 0.55;
                            var speedStr = FormatSpeed(p.Speed);
                            var msg = string.IsNullOrEmpty(p.Message)
                                ? string.Format(_languageService.GetString("Progress_DownloadingFrom"), mirror.Name, "") + $" {speedStr}"
                                : p.Message;
                            progress?.Report(new InstallProgress
                            {
                                Percent = percent,
                                Message = msg,
                                Speed = p.Speed,
                                DownloadedSize = p.DownloadedSize,
                                TotalSize = p.TotalSize,
                                RemainingTime = p.RemainingTime,
                                MirrorName = mirror.Name
                            });
                        });

                        var downloadTask = await _downloadEngine.DownloadAsync(downloadUrl, cacheFile, Guid.NewGuid().ToString(), downloadProgress, cancellationToken);

                        if (downloadTask.Status == DownloadStatus.Completed)
                        {
                            // 验证下载文件：文件必须存在且大小 > 0
                            if (File.Exists(cacheFile) && new FileInfo(cacheFile).Length > 0)
                            {
                                downloadSuccess = true;
                                await _mirrorProvider.RecordMirrorResultAsync(mirror.Id, true);
                                break;
                            }
                            else
                            {
                                lastError = _languageService.GetString("Dialog_DownloadEmptyFile");
                                await _mirrorProvider.RecordMirrorResultAsync(mirror.Id, false);
                                try { if (File.Exists(cacheFile)) File.Delete(cacheFile); } catch { }
                            }
                        }
                        else if (downloadTask.Status == DownloadStatus.Cancelled)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                        }
                        else
                        {
                            lastError = downloadTask.ErrorMessage;
                            // 记录镜像失败
                            await _mirrorProvider.RecordMirrorResultAsync(mirror.Id, false);
                            try { if (File.Exists(cacheFile)) File.Delete(cacheFile); } catch { }
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        lastError = ex.Message;
                        // 记录镜像失败
                        await _mirrorProvider.RecordMirrorResultAsync(mirror.Id, false);
                        try { if (File.Exists(cacheFile)) File.Delete(cacheFile); } catch { }
                    }
                }

                if (!downloadSuccess)
                {
                    var mirrorList = string.Join("、", triedMirrors);
                    throw new InvalidOperationException(string.Format(_languageService.GetString("Dialog_DownloadAllMirrorsFailed"), mirrorList, lastError));
                }
            }

            progress?.Report(new InstallProgress { Percent = 70, Message = _languageService.GetString("Progress_Extracting") });

            await Task.Run(() =>
            {
                // 验证 ZIP 文件完整性
                try
                {
                    using (var archive = System.IO.Compression.ZipFile.OpenRead(cacheFile))
                    {
                        // 仅尝试打开并读取条目数，验证中央目录是否完整
                        if (archive.Entries.Count == 0)
                            throw new InvalidDataException("ZIP 文件为空");
                    }
                }
                catch (InvalidDataException ex)
                {
                    // ZIP 文件损坏，删除缓存文件并提示重新下载
                    try { if (File.Exists(cacheFile)) File.Delete(cacheFile); } catch { }
                    throw new InvalidOperationException(string.Format(_languageService.GetString("Dialog_ZipCorrupted"), ex.Message));
                }

                // ZIP 解压安装（所有 SDK 统一方式）
                var tempExtractDir = installPath + "_extract_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                try
                {
                    Directory.CreateDirectory(tempExtractDir);
                    System.IO.Compression.ZipFile.ExtractToDirectory(cacheFile, tempExtractDir);

                    // 检查解压结果：如果只有一个子目录（如 node-v20.11.0-win-x64），使用其内容
                    var dirs = Directory.GetDirectories(tempExtractDir);
                    var files = Directory.GetFiles(tempExtractDir);
                    var sourceDir = (dirs.Length == 1 && files.Length == 0) ? dirs[0] : tempExtractDir;

                    // 清理并创建安装目录
                    if (Directory.Exists(installPath))
                    {
                        try { DeleteDirectoryRobust(installPath); } catch { }
                    }
                    Directory.CreateDirectory(installPath);

                    // 清除源目录中所有文件和子目录的只读属性（JDK等ZIP中可能包含只读文件）
                    ClearReadOnlyAttributes(sourceDir);

                    // 复制所有文件和目录到安装路径（复制比移动更可靠，避免只读属性/文件锁/跨卷问题）
                    foreach (var f in Directory.GetFiles(sourceDir))
                    {
                        var dest = Path.Combine(installPath, Path.GetFileName(f));
                        var fi = new FileInfo(f);
                        fi.IsReadOnly = false;
                        File.Copy(f, dest, true);
                    }
                    foreach (var d in Directory.GetDirectories(sourceDir))
                    {
                        var dest = Path.Combine(installPath, Path.GetFileName(d));
                        CopyDirectoryRobust(d, dest);
                    }
                }
                finally
                {
                    // 清理临时目录
                    try { if (Directory.Exists(tempExtractDir)) DeleteDirectoryRobust(tempExtractDir); } catch { }
                }
            }, cancellationToken);

            progress?.Report(new InstallProgress { Percent = 90, Message = _languageService.GetString("Progress_ConfiguringEnv") });

            await _environmentManager.BackupEnvironmentVariablesAsync();

            // 移除旧版本的 PATH 条目（仅清理当前安装级别，避免跨级别误删）
            var envName = provider.GetEnvironmentVariableName();
            var oldEnvValue = await _environmentManager.GetEnvironmentVariableAsync(envName, useSystemLevel);
            if (!string.IsNullOrEmpty(oldEnvValue))
            {
                var oldInstallPath = oldEnvValue.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var oldBinPath = provider.GetBinPath(oldInstallPath);
                await _environmentManager.RemoveFromPathAsync(oldBinPath, useSystemLevel);
                // Node.js 专用：清理旧版本的 node_global 路径
                if (language == SdkLanguage.NodeJs)
                {
                    var oldNodeGlobalPath = Path.Combine(oldInstallPath, "node_global");
                    await _environmentManager.RemoveFromPathAsync(oldNodeGlobalPath, useSystemLevel);
                }
                // Python 专用：清理旧版本的 Scripts 路径
                if (language == SdkLanguage.Python)
                {
                    var oldScriptsPath = provider.GetScriptsPath(oldInstallPath);
                    if (!string.IsNullOrEmpty(oldScriptsPath))
                    {
                        await _environmentManager.RemoveFromPathAsync(oldScriptsPath, useSystemLevel);
                    }
                }
            }

            var binPath = provider.GetBinPath(installPath);

            // Python 安装时清理 Windows 应用执行别名（WindowsApps 中的 python.exe 指向 Microsoft Store）
            if (language == SdkLanguage.Python)
            {
                await _environmentManager.RemoveWindowsAppsPythonAliasAsync();
            }

            try
            {
                await _environmentManager.AddToPathAsync(binPath, useSystemLevel);
                await _environmentManager.SetEnvironmentVariableAsync(provider.GetEnvironmentVariableName(), installPath, useSystemLevel);
            }
            catch (InvalidOperationException) when (useSystemLevel)
            {
                // 系统级环境变量设置失败（非管理员），回退到用户级
                useSystemLevel = false;
                await _environmentManager.AddToPathAsync(binPath, false);
                await _environmentManager.SetEnvironmentVariableAsync(provider.GetEnvironmentVariableName(), installPath, false);
            }
            _environmentManager.RefreshCurrentProcessEnvironment();

            // Node.js 专用配置：创建 npm 全局安装目录和缓存目录，并将 node_global 添加到 PATH
            if (language == SdkLanguage.NodeJs)
            {
                var nodeGlobalPath = Path.Combine(installPath, "node_global");
                var nodeCachePath = Path.Combine(installPath, "node_cache");
                if (!Directory.Exists(nodeGlobalPath))
                    Directory.CreateDirectory(nodeGlobalPath);
                if (!Directory.Exists(nodeCachePath))
                    Directory.CreateDirectory(nodeCachePath);

                try
                {
                    await _environmentManager.AddToPathAsync(nodeGlobalPath, useSystemLevel);
                }
                catch (InvalidOperationException) when (useSystemLevel)
                {
                    await _environmentManager.AddToPathAsync(nodeGlobalPath, false);
                }
                _environmentManager.RefreshCurrentProcessEnvironment();

                // 配置 npm 全局安装路径和缓存路径
                try
                {
                    var (prefixOut, prefixErr, prefixCode) = await RunNpmCommandAsync(installPath, $"config set prefix \"{nodeGlobalPath}\"");
                    _logService.Info($"npm config set prefix: exit={prefixCode}, out={prefixOut}, err={prefixErr}");

                    var (cacheOut, cacheErr, cacheCode) = await RunNpmCommandAsync(installPath, $"config set cache \"{nodeCachePath}\"");
                    _logService.Info($"npm config set cache: exit={cacheCode}, out={cacheOut}, err={cacheErr}");
                }
                catch (Exception ex)
                {
                    _logService.Warn($"配置 npm 全局路径失败（不影响基本使用）: {ex.Message}");
                }
            }

            // Python 专用配置：根据用户配置补全 embeddable 版本
            if (language == SdkLanguage.Python)
            {
                try
                {
                    var pythonConfig = config;

                    // 步骤1：修改 _pth 文件，启用 site 模块并添加 site-packages 路径
                    if (pythonConfig.PythonEnableSitePackages)
                    {
                        await Task.Run(() => ConfigurePthFile(installPath, version));
                    }

                    // 步骤2：下载 get-pip.py 并安装 pip
                    if (pythonConfig.PythonInstallPip)
                    {
                        progress?.Report(new InstallProgress { Percent = 93, Message = _languageService.GetString("Progress_InstallingPip") });
                        await InstallPipAsync(installPath, progress);
                    }

                    // 步骤3：将 Scripts 目录添加到 PATH
                    if (pythonConfig.PythonInstallPip)
                    {
                        var scriptsPath = provider.GetScriptsPath(installPath);
                        if (!string.IsNullOrEmpty(scriptsPath))
                        {
                            try
                            {
                                await _environmentManager.AddToPathAsync(scriptsPath, useSystemLevel);
                            }
                            catch (InvalidOperationException) when (useSystemLevel)
                            {
                                await _environmentManager.AddToPathAsync(scriptsPath, false);
                            }
                            _environmentManager.RefreshCurrentProcessEnvironment();
                        }
                    }

                    // 步骤4：补全 Tcl/Tk (tkinter) 支持（IDLE 也依赖 Tcl/Tk，会一并提取 idlelib）
                    if (pythonConfig.PythonInstallTclTk || pythonConfig.PythonInstallIdle)
                    {
                        progress?.Report(new InstallProgress { Percent = 95, Message = _languageService.GetString("Progress_InstallingTclTk") });
                        await InstallTclTkAsync(installPath, version, progress, pythonConfig.PythonInstallIdle);
                    }

                    // 步骤5：补全 IDLE 启动器（依赖 Tcl/Tk 和 idlelib）
                    if (pythonConfig.PythonInstallIdle)
                    {
                        await Task.Run(() => ConfigureIdle(installPath));
                    }

                    // 步骤6：注册到 Windows 注册表
                    if (pythonConfig.PythonRegisterRegistry)
                    {
                        await Task.Run(() => RegisterPythonInRegistry(installPath, version, useSystemLevel));
                    }

                    // 步骤7：关联 .py 文件
                    if (pythonConfig.PythonAssociateFiles)
                    {
                        await Task.Run(() => AssociatePythonFiles(installPath, useSystemLevel));
                    }
                }
                catch (Exception ex)
                {
                    _logService.Warn($"Python 补全配置失败（不影响 Python 基本使用）: {ex.Message}");
                }
            }

            // 验证安装后可执行文件是否存在
            var exeFullPath = Path.Combine(installPath, provider.GetExecutableName());
            if (language == SdkLanguage.Java)
                exeFullPath = Path.Combine(installPath, "bin", provider.GetExecutableName());

            if (!File.Exists(exeFullPath))
            {
                throw new InvalidOperationException(string.Format(_languageService.GetString("Dialog_ExecutableNotFound"), provider.GetExecutableName()));
            }

            progress?.Report(new InstallProgress { Percent = 100, Message = _languageService.GetString("Progress_InstallComplete") });

            _logService.Info($"{language} {version} 安装完成，路径: {installPath}");

            // 发送全局状态变更消息
            WeakMessenger.Send(new SdkStatusChangedMessage { Language = language.ToString(), Version = version, Action = "Install" });

            return new InstalledSdk
            {
                Language = language,
                Version = version,
                InstallPath = installPath,
                IsActive = true,
                InstallDate = DateTime.Now
            };
        }

        /// <summary>
        /// 验证安装目录的有效性：检查核心可执行文件是否存在
        /// </summary>
        private bool IsInstallationValid(SdkLanguage language, string installPath)
        {
            if (!Directory.Exists(installPath)) return false;

            var provider = GetProvider(language);
            var exeName = provider.GetExecutableName();

            // Java 的可执行文件在 bin 子目录下
            var exePath = language == SdkLanguage.Java
                ? Path.Combine(installPath, "bin", exeName)
                : Path.Combine(installPath, exeName);

            return File.Exists(exePath);
        }

        private string EnsureWritablePath(string basePath)
        {
            var candidates = new[]
            {
                basePath,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SDK-Manager"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "SDK-Manager"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sdk"),
            };

            foreach (var path in candidates)
            {
                try
                {
                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);

                    // 测试写权限
                    var testFile = Path.Combine(path, ".write_test");
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);

                    return path;
                }
                catch
                {
                    continue;
                }
            }

            // 所有路径都不可写，抛出异常
            throw new InvalidOperationException(_languageService.GetString("Dialog_NoWritablePath"));
        }

        public Task<bool> SwitchVersionAsync(SdkLanguage language, string version)
        {
            // 单版本模式下，切换版本等同于重新安装
            // 保留此方法以保持接口兼容性
            throw new InvalidOperationException(_languageService.GetString("Dialog_SingleVersionMode"));
        }

        public async Task<bool> UninstallAsync(SdkLanguage language, string version, bool systemLevel = false)
        {
            var provider = GetProvider(language);
            var config = await _configService.GetConfigAsync();

            _logService.Info($"开始卸载 {language} {version}（{(systemLevel ? "系统级" : "用户级")}）");

            var installBasePath = EnsureWritablePath(config.DefaultInstallPath);
            var langBasePath = Path.Combine(installBasePath, language.ToString());

            // 安装路径：{basePath}/{Language}/{version}/
            var installPath = Path.Combine(langBasePath, version);

            // 如果精确路径不存在，尝试在子目录中查找匹配的版本目录
            if (!Directory.Exists(installPath))
            {
                var normalizedVersion = version.TrimStart('v', 'V');
                var altPath = Path.Combine(langBasePath, version.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? normalizedVersion : "v" + version);

                if (Directory.Exists(altPath))
                {
                    installPath = altPath;
                    _logService.Info($"使用替代路径: {installPath}");
                }
                else if (Directory.Exists(langBasePath))
                {
                    // 在子目录中查找版本号匹配的目录
                    foreach (var dir in Directory.GetDirectories(langBasePath))
                    {
                        var dirName = Path.GetFileName(dir);
                        var dirNormalized = dirName.TrimStart('v', 'V');
                        if (string.Equals(dirNormalized, normalizedVersion, StringComparison.OrdinalIgnoreCase))
                        {
                            installPath = dir;
                            _logService.Info($"使用匹配路径: {installPath}");
                            break;
                        }
                    }
                }
            }

            if (!Directory.Exists(installPath))
            {
                _logService.Warn($"卸载目录不存在: {installPath}");
                // 即使目录不存在，仍然尝试清理环境变量
                var envName = provider.GetEnvironmentVariableName();
                var envValue = await _environmentManager.GetEnvironmentVariableAsync(envName, systemLevel);
                if (!string.IsNullOrEmpty(envValue))
                {
                    var trimmedEnvValue = envValue.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    var binPath = provider.GetBinPath(trimmedEnvValue);
                    await _environmentManager.RemoveFromPathAsync(binPath, systemLevel);
                    // Node.js 专用：清理 node_global 路径
                    if (language == SdkLanguage.NodeJs)
                    {
                        var nodeGlobalPath = Path.Combine(trimmedEnvValue, "node_global");
                        await _environmentManager.RemoveFromPathAsync(nodeGlobalPath, systemLevel);
                    }
                    // Python 专用：清理 Scripts 路径
                    if (language == SdkLanguage.Python)
                    {
                        var scriptsPath = Path.Combine(trimmedEnvValue, "Scripts");
                        await _environmentManager.RemoveFromPathAsync(scriptsPath, systemLevel);
                    }
                    await _environmentManager.SetEnvironmentVariableAsync(envName, null, systemLevel);
                    _environmentManager.RefreshCurrentProcessEnvironment();
                    _logService.Info($"已清理{(systemLevel ? "系统级" : "用户级")}环境变量");
                }

                // 即使目录不存在，也要通知 UI 刷新状态
                WeakMessenger.Send(new SdkStatusChangedMessage { Language = language.ToString(), Version = version, Action = "Uninstall" });

                return false;
            }

            // 根据卸载级别清理环境变量
            var binPathToDelete = provider.GetBinPath(installPath);
            await _environmentManager.RemoveFromPathAsync(binPathToDelete, systemLevel);
            await _environmentManager.SetEnvironmentVariableAsync(provider.GetEnvironmentVariableName(), null, systemLevel);

            // Node.js 专用：从 PATH 中移除 node_global 路径
            if (language == SdkLanguage.NodeJs)
            {
                var nodeGlobalPath = Path.Combine(installPath, "node_global");
                await _environmentManager.RemoveFromPathAsync(nodeGlobalPath, systemLevel);
            }

            // Python 专用：从 PATH 中移除 Scripts 路径
            if (language == SdkLanguage.Python)
            {
                var scriptsPath = provider.GetScriptsPath(installPath);
                if (!string.IsNullOrEmpty(scriptsPath))
                {
                    await _environmentManager.RemoveFromPathAsync(scriptsPath, systemLevel);
                }
            }

            // 检查另一个级别是否还在使用同一安装路径
            var envVarName = provider.GetEnvironmentVariableName();
            var otherLevelPath = systemLevel
                ? await _environmentManager.GetEnvironmentVariableAsync(envVarName, false)  // 系统级卸载时检查用户级
                : await _environmentManager.GetEnvironmentVariableAsync(envVarName, true);  // 用户级卸载时检查系统级

            var otherLevelIsUsingThisPath = false;
            if (!string.IsNullOrEmpty(otherLevelPath))
            {
                var trimmed = otherLevelPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                otherLevelIsUsingThisPath = string.Equals(trimmed, installPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
            }

            _environmentManager.RefreshCurrentProcessEnvironment();

            // 只有另一个级别没有在使用同一安装路径时，才删除目录
            if (!otherLevelIsUsingThisPath)
            {
                await Task.Run(() => DeleteDirectoryRobust(installPath));
                _logService.Info($"{language} {version} 目录已清理: {installPath}");
            }
            else
            {
                _logService.Info($"{language} {version} 目录保留（{(systemLevel ? "用户级" : "系统级")}仍在使用）: {installPath}");
            }

            _logService.Info($"{language} {version} 卸载完成");

            // 发送全局状态变更消息
            WeakMessenger.Send(new SdkStatusChangedMessage { Language = language.ToString(), Version = version, Action = "Uninstall" });

            return true;
        }

        public Task<bool> ValidateInstallationAsync(SdkLanguage language, string installPath)
        {
            var provider = GetProvider(language);
            return provider.ValidateInstallationAsync(installPath);
        }

        public async Task<IEnumerable<DownloadCacheItem>> GetDownloadCacheAsync()
        {
            var result = new List<DownloadCacheItem>();
            var cacheBase = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache");

            if (!Directory.Exists(cacheBase))
                return result;

            foreach (var langDir in Directory.GetDirectories(cacheBase))
            {
                var langName = Path.GetFileName(langDir);

                // 支持 SdkLanguage 枚举目录和 Maven 目录
                if (!Enum.TryParse<SdkLanguage>(langName, out var language) && langName != "Maven") continue;

                // 扫描 .zip 和 .exe（Python 使用 exe 安装程序）
                foreach (var file in Directory.GetFiles(langDir, "*.*"))
                {
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext != ".zip" && ext != ".exe") continue;

                    var fi = new FileInfo(file);
                    // 从文件名解析版本号
                    var fileName = Path.GetFileNameWithoutExtension(fi.Name);

                    string version;
                    if (langName == "Maven")
                    {
                        // apache-maven-3.9.6-bin -> 3.9.6
                        var prefix = "apache-maven-";
                        var suffix = "-bin";
                        if (fileName.StartsWith(prefix) && fileName.EndsWith(suffix))
                            version = fileName.Substring(prefix.Length, fileName.Length - prefix.Length - suffix.Length);
                        else
                            version = fileName;
                    }
                    else
                    {
                        version = fileName.StartsWith($"{language}_")
                            ? fileName.Substring($"{language}_".Length)
                            : fileName;
                    }

                    result.Add(new DownloadCacheItem
                    {
                        FileName = fi.Name,
                        FilePath = fi.FullName,
                        FileSize = fi.Length,
                        Language = langName,
                        Version = version,
                        CreateTime = fi.CreationTime
                    });
                }
            }

            return await Task.FromResult(result);
        }

        public async Task<bool> DeleteDownloadCacheAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        return true;
                    }
                    return false;
                }
                catch
                {
                    return false;
                }
            });
        }

        public async Task<int> ClearAllDownloadCacheAsync()
        {
            return await Task.Run(() =>
            {
                var count = 0;
                var cacheBase = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache");
                if (!Directory.Exists(cacheBase)) return 0;

                foreach (var file in Directory.GetFiles(cacheBase, "*.*", SearchOption.AllDirectories))
                {
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext != ".zip" && ext != ".exe") continue;
                    try
                    {
                        File.Delete(file);
                        count++;
                    }
                    catch { }
                }
                return count;
            });
        }

        public async Task<long> GetDownloadCacheSizeAsync()
        {
            return await Task.Run(() =>
            {
                long total = 0;
                var cacheBase = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache");
                if (!Directory.Exists(cacheBase)) return 0L;

                foreach (var file in Directory.GetFiles(cacheBase, "*.*", SearchOption.AllDirectories))
                {
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext != ".zip" && ext != ".exe") continue;
                    try
                    {
                        total += new FileInfo(file).Length;
                    }
                    catch { }
                }
                return total;
            });
        }

        public async Task<bool> HasCacheAsync(SdkLanguage language, string version)
        {
            var cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", language.ToString());
            var cacheFile = Path.Combine(cacheDir, $"{language}_{version}.zip");
            if (!File.Exists(cacheFile)) return false;

            // 验证缓存文件完整性：文件大小 > 0 且能作为 ZIP 打开
            try
            {
                var fi = new FileInfo(cacheFile);
                if (fi.Length == 0) return false;
                using (var archive = System.IO.Compression.ZipFile.OpenRead(cacheFile))
                {
                    return archive.Entries.Count > 0;
                }
            }
            catch
            {
                // 缓存文件损坏，删除并返回 false
                try { File.Delete(cacheFile); } catch { }
                return false;
            }
        }

        private ISdkProvider GetProvider(SdkLanguage language)
        {
            var provider = _providers.FirstOrDefault(p => p.Language == language);
            if (provider == null)
                throw new NotSupportedException(string.Format(_languageService.GetString("Dialog_UnsupportedSdkLanguage"), language));
            return provider;
        }

        public string GetEnvironmentVariableName(SdkLanguage language)
        {
            return GetProvider(language).GetEnvironmentVariableName();
        }

        public async Task<SdkDetectionResult> DetectSdkAsync(SdkLanguage language)
        {
            var provider = GetProvider(language);
            var result = new SdkDetectionResult { Language = language };

            // 1. 分别读取用户级和系统级环境变量
            var envName = provider.GetEnvironmentVariableName();
            var userEnvValue = await _environmentManager.GetEnvironmentVariableAsync(envName, false);
            var systemEnvValue = await _environmentManager.GetEnvironmentVariableAsync(envName, true);

            // 填充用户级信息
            if (!string.IsNullOrEmpty(userEnvValue))
            {
                var trimmed = userEnvValue.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                result.UserLevelPath = trimmed;
                if (Directory.Exists(trimmed))
                {
                    var v = await DetectVersionFromExecutableAsync(language, trimmed);
                    result.UserLevelVersion = v ?? Path.GetFileName(trimmed);
                }
                else
                {
                    result.UserLevelVersion = Path.GetFileName(trimmed);
                }
            }

            // 填充系统级信息
            if (!string.IsNullOrEmpty(systemEnvValue))
            {
                var trimmed = systemEnvValue.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                result.SystemLevelPath = trimmed;
                if (Directory.Exists(trimmed))
                {
                    var v = await DetectVersionFromExecutableAsync(language, trimmed);
                    result.SystemLevelVersion = v ?? Path.GetFileName(trimmed);
                }
                else
                {
                    result.SystemLevelVersion = Path.GetFileName(trimmed);
                }
            }

            // 2. 兼容原有逻辑：优先使用用户级，否则使用系统级
            var envValue = !string.IsNullOrEmpty(userEnvValue) ? userEnvValue : systemEnvValue;
            if (!string.IsNullOrEmpty(envValue))
            {
                envValue = envValue.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (Directory.Exists(envValue))
                {
                    result.InstallPath = envValue;
                    result.IsManaged = true;
                }
            }

            // 2. 优先从环境变量路径直接执行版本检测（最可靠，不依赖 PATH 和文件夹名称）
            if (!string.IsNullOrEmpty(result.InstallPath))
            {
                var detectedVersion = await DetectVersionFromExecutableAsync(language, result.InstallPath);
                if (!string.IsNullOrEmpty(detectedVersion))
                {
                    result.IsInstalled = true;
                    result.DetectedVersion = detectedVersion;
                    result.ManagedVersion = detectedVersion;
                }
            }

            // 3. 如果环境变量路径检测失败，尝试从 PATH 执行命令
            if (!result.IsInstalled)
            {
                var exeName = provider.GetExecutableName();
                var versionArg = provider.GetVersionArgument();
                var detectedVersion = await ExecuteCommandForVersionAsync(exeName, versionArg);

                if (!string.IsNullOrEmpty(detectedVersion))
                {
                    result.IsInstalled = true;
                    result.DetectedVersion = NormalizeDetectedVersion(detectedVersion.Trim(), language);
                }
            }

            // 4. 如果命令行检测成功但没有安装路径，从 PATH 中查找
            if (result.IsInstalled && string.IsNullOrEmpty(result.InstallPath))
            {
                var exeName = provider.GetExecutableName();
                var pathValue = await _environmentManager.GetEnvironmentVariableAsync("PATH", false);
                if (string.IsNullOrEmpty(pathValue))
                    pathValue = await _environmentManager.GetEnvironmentVariableAsync("PATH", true);
                if (!string.IsNullOrEmpty(pathValue))
                {
                    var pathDirs = pathValue.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var dir in pathDirs)
                    {
                        var exePath = Path.Combine(dir, exeName);
                        if (File.Exists(exePath))
                        {
                            result.InstallPath = language == SdkLanguage.Java
                                ? Path.GetDirectoryName(Path.GetDirectoryName(exePath))
                                : Path.GetDirectoryName(exePath);
                            break;
                        }
                    }
                }
            }

            // 5. 如果命令行检测失败但环境变量存在
            if (!result.IsInstalled && !string.IsNullOrEmpty(result.InstallPath))
            {
                result.IsInstalled = true;
                if (string.IsNullOrEmpty(result.DetectedVersion) && !string.IsNullOrEmpty(result.ManagedVersion))
                {
                    result.DetectedVersion = result.ManagedVersion;
                }
            }

            return result;
        }

        /// <summary>
        /// 标准化命令行检测到的版本号，使其与版本列表中的格式一致
        /// </summary>
        private static string NormalizeDetectedVersion(string rawVersion, SdkLanguage language)
        {
            if (string.IsNullOrEmpty(rawVersion)) return rawVersion;

            switch (language)
            {
                case SdkLanguage.NodeJs:
                    // node --version 输出 "v20.11.0"，版本列表也是 "v20.11.0"，无需转换
                    return rawVersion;

                case SdkLanguage.Java:
                    // java -version 输出到 stderr，格式如：
                    // "java version "1.8.0_392"" 或 "openjdk version "11.0.21" 2023-10-17"
                    // 提取版本号
                    var javaMatch = System.Text.RegularExpressions.Regex.Match(rawVersion, @"version\s+""([^""]+)""");
                    if (javaMatch.Success)
                    {
                        var ver = javaMatch.Groups[1].Value;
                        // Java 8 的版本号是 1.8.0_xxx 格式，转换为 8.0.xxx
                        if (ver.StartsWith("1.8."))
                        {
                            var updatePart = ver.Substring("1.8.0_".Length);
                            return $"8.0.{updatePart}";
                        }
                        // Java 9+ 版本号如 11.0.21，直接返回
                        return ver;
                    }
                    return rawVersion;

                case SdkLanguage.Python:
                    // python --version 输出 "Python 3.12.3"，提取版本号
                    if (rawVersion.StartsWith("Python ", StringComparison.OrdinalIgnoreCase))
                        return rawVersion.Substring("Python ".Length);
                    return rawVersion;

                default:
                    return rawVersion;
            }
        }

        private async Task<string> ExecuteCommandForVersionAsync(string executableName, string argument)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = executableName,
                    Arguments = argument,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = new Process { StartInfo = psi })
                {
                    process.Start();

                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();

                    // 等待进程退出，超时5秒后强制终止
                    if (!process.WaitForExit(5000))
                    {
                        try { process.Kill(); } catch { }
                        return null;
                    }

                    var output = (await outputTask).Trim();
                    var error = (await errorTask).Trim();

                    if (string.IsNullOrEmpty(output) && !string.IsNullOrEmpty(error))
                        return error;

                    return output;
                }
            }
            catch
            {
                return null;
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

        private static void CopyDirectoryRobust(string sourceDir, string destDir)
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
                    // 文件可能被短暂锁定（杀毒软件/索引器），重试3次
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
                var dest = Path.Combine(destDir, Path.GetFileName(d));
                CopyDirectoryRobust(d, dest);
            }
        }
    }

    /// <summary>
    /// 语义版本比较器，支持 v 前缀和纯数字版本号
    /// 确保 1.10.0 > 1.8.0（而非字符串排序的 1.8.0 > 1.10.0）
    /// </summary>
    public class SemanticVersionComparer : System.Collections.Generic.IComparer<string>
    {
        public int Compare(string x, string y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            var xParts = ParseVersion(x);
            var yParts = ParseVersion(y);

            for (int i = 0; i < System.Math.Max(xParts.Length, yParts.Length); i++)
            {
                var xVal = i < xParts.Length ? xParts[i] : 0;
                var yVal = i < yParts.Length ? yParts[i] : 0;

                if (xVal != yVal) return xVal.CompareTo(yVal);
            }

            return 0;
        }

        private int[] ParseVersion(string version)
        {
            // 去掉 v 前缀
            var v = version.TrimStart('v', 'V');
            // 只取数字和点号部分
            var clean = new System.Text.StringBuilder();
            foreach (var c in v)
            {
                if (char.IsDigit(c) || c == '.')
                    clean.Append(c);
                else
                    break;
            }

            var parts = clean.ToString().Split(new[] { '.' }, System.StringSplitOptions.RemoveEmptyEntries);
            var result = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                int.TryParse(parts[i], out result[i]);
            }
            return result;
        }
    }
}
