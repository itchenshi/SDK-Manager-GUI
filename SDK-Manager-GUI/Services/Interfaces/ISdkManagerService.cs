using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SDK_Manager_GUI.Models;

namespace SDK_Manager_GUI.Services
{
    public interface ISdkManagerService
    {
        Task<IEnumerable<SdkVersion>> GetAvailableVersionsAsync(SdkLanguage language);
        Task<IEnumerable<InstalledSdk>> GetInstalledVersionsAsync(SdkLanguage language);
        Task<InstalledSdk> GetActiveVersionAsync(SdkLanguage language);
        Task<InstalledSdk> InstallAsync(SdkLanguage language, string version, IProgress<InstallProgress> progress, CancellationToken cancellationToken, bool systemLevel = false);
        Task<bool> SwitchVersionAsync(SdkLanguage language, string version);
        Task<bool> UninstallAsync(SdkLanguage language, string version, bool systemLevel = false);
        Task<bool> ValidateInstallationAsync(SdkLanguage language, string installPath);
        Task<SdkDetectionResult> DetectSdkAsync(SdkLanguage language);

        /// <summary>
        /// 获取下载缓存列表
        /// </summary>
        Task<IEnumerable<DownloadCacheItem>> GetDownloadCacheAsync();
        /// <summary>
        /// 删除单个下载缓存
        /// </summary>
        Task<bool> DeleteDownloadCacheAsync(string filePath);
        /// <summary>
        /// 清理全部下载缓存
        /// </summary>
        Task<int> ClearAllDownloadCacheAsync();
        /// <summary>
        /// 获取下载缓存总大小（字节）
        /// </summary>
        Task<long> GetDownloadCacheSizeAsync();
        /// <summary>
        /// 检查指定SDK版本是否有本地缓存
        /// </summary>
        Task<bool> HasCacheAsync(SdkLanguage language, string version);
        /// <summary>
        /// 获取指定语言的环境变量名（如 NODE_HOME, JAVA_HOME, PYTHON_HOME）
        /// </summary>
        string GetEnvironmentVariableName(SdkLanguage language);
    }
}
