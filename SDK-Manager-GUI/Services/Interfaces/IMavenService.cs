using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SDK_Manager_GUI.Models;

namespace SDK_Manager_GUI.Services
{
    public interface IMavenService
    {
        Task<IEnumerable<MavenVersion>> GetAvailableVersionsAsync();
        string GetMirrorDownloadUrl(string version, string mirrorBaseUrl);
        Task<MavenDetectionResult> DetectMavenAsync();
        Task InstallMavenAsync(string version, string downloadUrl, IProgress<InstallProgress> progress, bool systemLevel = false);
        Task UninstallMavenAsync(bool systemLevel);
        Task<bool> HasCacheAsync(string version);

        // Maven 下载镜像源管理
        Task<IEnumerable<MavenDownloadMirror>> GetDownloadMirrorsAsync();
        Task AddDownloadMirrorAsync(MavenDownloadMirror mirror);
        Task RemoveDownloadMirrorAsync(string mirrorId);
        Task UpdateDownloadMirrorAsync(MavenDownloadMirror mirror);
        Task TestDownloadMirrorLatencyAsync(MavenDownloadMirror mirror);
        string BuildDownloadUrl(string version, string mirrorBaseUrl);
        Task TestAndDisableUnreachableMirrorsAsync();

    }
}
