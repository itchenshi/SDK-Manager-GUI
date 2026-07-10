using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.Win32;
using SDK_Manager_GUI.Models;
using SDK_Manager_GUI.Services;

namespace SDK_Manager_GUI.Services
{
    public class EnvironmentManager : IEnvironmentManager
    {
        private readonly ILanguageService _languageService;
        private readonly Dictionary<string, string> _backup = new Dictionary<string, string>();

        public EnvironmentManager(ILanguageService languageService)
        {
            _languageService = languageService;
        }

        /// <summary>
        /// 检查当前进程是否以管理员身份运行
        /// </summary>
        public static bool IsRunningAsAdmin()
        {
            try
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 广播 WM_SETTINGCHANGE 消息，通知系统环境变量已更改
        /// 新开的 cmd/powershell 窗口才能读取到更新后的环境变量
        /// </summary>
        public static void BroadcastEnvironmentChange()
        {
            try
            {
                IntPtr result;
                SendMessageTimeout(
                    (IntPtr)0xFFFF, // HWND_BROADCAST
                    WM_SETTINGCHANGE,
                    UIntPtr.Zero,
                    "Environment",
                    SMTO_ABORTIFHUNG,
                    5000,
                    out result);
            }
            catch
            {
                // 广播失败不影响主流程
            }
        }

        private const uint WM_SETTINGCHANGE = 0x001A;
        private const uint SMTO_ABORTIFHUNG = 0x0002;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessageTimeout(
            IntPtr hWnd, uint Msg, UIntPtr wParam, string lParam,
            uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

        /// <summary>
        /// 使用 Win32 注册表 API 设置环境变量
        /// 关键：PATH 变量必须使用 REG_EXPAND_SZ 类型，否则新 cmd 窗口无法识别
        /// .NET 的 Environment.SetEnvironmentVariable 总是写 REG_SZ，导致 PATH 不生效
        /// </summary>
        public Task<bool> SetEnvironmentVariableAsync(string name, string value, bool systemLevel)
        {
            return Task.Run(() =>
            {
                try
                {
                    if (string.Equals(name, "PATH", StringComparison.OrdinalIgnoreCase))
                    {
                        // PATH 变量必须用 REG_EXPAND_SZ 类型写入注册表
                        SetRegistryEnvironmentVariable(name, value, systemLevel);
                    }
                    else
                    {
                        // 非 PATH 变量使用 .NET API（写 REG_SZ 即可）
                        var target = systemLevel
                            ? EnvironmentVariableTarget.Machine
                            : EnvironmentVariableTarget.User;
                        Environment.SetEnvironmentVariable(name, value, target);
                    }

                    // 通知系统环境变量已更改
                    BroadcastEnvironmentChange();

                    return true;
                }
                catch (System.Security.SecurityException)
                {
                    throw new InvalidOperationException(
                        systemLevel
                            ? string.Format(_languageService.GetString("Dialog_SetEnvNeedAdmin"), name)
                            : string.Format(_languageService.GetString("Dialog_SetEnvNeedAdmin"), name));
                }
                catch (UnauthorizedAccessException)
                {
                    throw new InvalidOperationException(
                        systemLevel
                            ? string.Format(_languageService.GetString("Dialog_SetEnvNeedAdmin"), name)
                            : string.Format(_languageService.GetString("Dialog_SetEnvNeedAdmin"), name));
                }
                catch (Exception) when (systemLevel)
                {
                    // 系统级操作可能因权限不足抛出各种异常，统一转换为友好提示
                    throw new InvalidOperationException(
                        string.Format(_languageService.GetString("Dialog_SetEnvNeedAdmin"), name));
                }
            });
        }

        /// <summary>
        /// 直接操作注册表写入环境变量，PATH 使用 REG_EXPAND_SZ 类型
        /// </summary>
        private void SetRegistryEnvironmentVariable(string name, string value, bool systemLevel)
        {
            var rootKey = systemLevel ? Registry.LocalMachine : Registry.CurrentUser;
            var subKey = systemLevel
                ? @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment"
                : @"Environment";

            using (var key = rootKey.OpenSubKey(subKey, true))
            {
                if (key == null)
                    throw new InvalidOperationException(string.Format(_languageService.GetString("Dialog_CannotOpenRegistry"), subKey));

                if (value == null)
                {
                    key.DeleteValue(name, false);
                }
                else
                {
                    // PATH 必须使用 REG_EXPAND_SZ 类型，这样 Windows 才能在新 cmd 窗口中正确展开
                    key.SetValue(name, value, RegistryValueKind.ExpandString);
                }
            }
        }

        /// <summary>
        /// 从注册表读取环境变量（直接读注册表，避免 .NET 缓存问题）
        /// </summary>
        public Task<string> GetEnvironmentVariableAsync(string name, bool systemLevel)
        {
            return Task.Run(() =>
            {
                var rootKey = systemLevel ? Registry.LocalMachine : Registry.CurrentUser;
                var subKey = systemLevel
                    ? @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment"
                    : @"Environment";

                using (var key = rootKey.OpenSubKey(subKey, false))
                {
                    if (key == null) return null;
                    var value = key.GetValue(name);
                    return value?.ToString();
                }
            });
        }

        public async Task<bool> AddToPathAsync(string path, bool systemLevel)
        {
            var currentPath = await GetEnvironmentVariableAsync("PATH", systemLevel);
            if (currentPath != null && currentPath.Split(';').Any(p =>
                string.Equals(p.TrimEnd('\\'), path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            var newPath = string.IsNullOrEmpty(currentPath)
                ? path
                : $"{currentPath};{path}";
            return await SetEnvironmentVariableAsync("PATH", newPath, systemLevel);
        }

        /// <summary>
        /// 清理用户级 PATH 中 WindowsApps 的 Python 别名路径
        /// Windows 10/11 会在 %LOCALAPPDATA%\Microsoft\WindowsApps\ 放置 python.exe/python3.exe
        /// 的应用执行别名，指向 Microsoft Store，优先级高于 SDK Manager 安装的 Python
        /// </summary>
        public async Task RemoveWindowsAppsPythonAliasAsync()
        {
            var currentPath = await GetEnvironmentVariableAsync("PATH", false);
            if (currentPath == null) return;

            var paths = currentPath.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            var modified = false;

            // 查找并移除 WindowsApps 目录中包含 python 相关别名的路径
            for (int i = paths.Count - 1; i >= 0; i--)
            {
                var p = paths[i].TrimEnd('\\');
                if (p.IndexOf("WindowsApps", StringComparison.OrdinalIgnoreCase) >= 0
                    && (p.IndexOf("Python", StringComparison.OrdinalIgnoreCase) >= 0
                        || p.EndsWith("Microsoft\\WindowsApps", StringComparison.OrdinalIgnoreCase)))
                {
                    paths.RemoveAt(i);
                    modified = true;
                }
            }

            if (modified)
            {
                var newPath = string.Join(";", paths);
                await SetEnvironmentVariableAsync("PATH", newPath, false);
            }
        }

        public async Task<bool> RemoveFromPathAsync(string path, bool systemLevel)
        {
            var currentPath = await GetEnvironmentVariableAsync("PATH", systemLevel);
            if (currentPath == null) return true;

            var paths = currentPath.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(p => !string.Equals(p.TrimEnd('\\'), path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                .ToList();
            var newPath = string.Join(";", paths);
            return await SetEnvironmentVariableAsync("PATH", newPath, systemLevel);
        }

        public async Task BackupEnvironmentVariablesAsync()
        {
            _backup.Clear();
            // 使用注册表直接读取，避免 .NET 缓存导致备份值不准确
            var userPath = await GetEnvironmentVariableAsync("PATH", false);
            var machinePath = await GetEnvironmentVariableAsync("PATH", true);
            _backup["PATH_User"] = userPath ?? "";
            _backup["PATH_Machine"] = machinePath ?? "";
            _backup["NODE_HOME"] = (await GetEnvironmentVariableAsync("NODE_HOME", false)) ?? "";
            _backup["JAVA_HOME"] = (await GetEnvironmentVariableAsync("JAVA_HOME", false)) ?? "";
            _backup["PYTHON_HOME"] = (await GetEnvironmentVariableAsync("PYTHON_HOME", false)) ?? "";
            // 同时备份系统级 HOME 变量
            _backup["NODE_HOME_Machine"] = (await GetEnvironmentVariableAsync("NODE_HOME", true)) ?? "";
            _backup["JAVA_HOME_Machine"] = (await GetEnvironmentVariableAsync("JAVA_HOME", true)) ?? "";
            _backup["PYTHON_HOME_Machine"] = (await GetEnvironmentVariableAsync("PYTHON_HOME", true)) ?? "";
        }

        public async Task RestoreEnvironmentVariablesAsync()
        {
            if (_backup.TryGetValue("PATH_User", out var userPath))
                await SetEnvironmentVariableAsync("PATH", userPath, false);
            if (_backup.TryGetValue("PATH_Machine", out var machinePath) && IsRunningAsAdmin())
            {
                try { await SetEnvironmentVariableAsync("PATH", machinePath, true); } catch { }
            }
            if (_backup.TryGetValue("NODE_HOME", out var nodeHome))
                await SetEnvironmentVariableAsync("NODE_HOME", nodeHome, false);
            if (_backup.TryGetValue("JAVA_HOME", out var javaHome))
                await SetEnvironmentVariableAsync("JAVA_HOME", javaHome, false);
            if (_backup.TryGetValue("PYTHON_HOME", out var pythonHome))
                await SetEnvironmentVariableAsync("PYTHON_HOME", pythonHome, false);
            // 恢复系统级 HOME 变量
            if (IsRunningAsAdmin())
            {
                if (_backup.TryGetValue("NODE_HOME_Machine", out var nodeHomeMachine))
                    try { await SetEnvironmentVariableAsync("NODE_HOME", nodeHomeMachine, true); } catch { }
                if (_backup.TryGetValue("JAVA_HOME_Machine", out var javaHomeMachine))
                    try { await SetEnvironmentVariableAsync("JAVA_HOME", javaHomeMachine, true); } catch { }
                if (_backup.TryGetValue("PYTHON_HOME_Machine", out var pythonHomeMachine))
                    try { await SetEnvironmentVariableAsync("PYTHON_HOME", pythonHomeMachine, true); } catch { }
            }
        }

        public void RefreshCurrentProcessEnvironment()
        {
            // 从注册表读取最新值，避免 .NET 缓存导致进程环境变量不准确
            var userPath = GetRegistryValue("PATH", false) ?? "";
            var machinePath = GetRegistryValue("PATH", true) ?? "";
            Environment.SetEnvironmentVariable("PATH", $"{machinePath};{userPath}", EnvironmentVariableTarget.Process);
        }

        private static string GetRegistryValue(string name, bool systemLevel)
        {
            var rootKey = systemLevel ? Registry.LocalMachine : Registry.CurrentUser;
            var subKey = systemLevel
                ? @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment"
                : @"Environment";

            using (var key = rootKey.OpenSubKey(subKey, false))
            {
                if (key == null) return null;
                var value = key.GetValue(name);
                return value?.ToString();
            }
        }
    }
}
