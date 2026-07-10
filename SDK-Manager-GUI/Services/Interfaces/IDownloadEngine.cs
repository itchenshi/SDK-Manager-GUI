using System;
using System.Threading;
using System.Threading.Tasks;
using SDK_Manager_GUI.Models;

namespace SDK_Manager_GUI.Services
{
    public interface IDownloadEngine
    {
        Task<DownloadTask> DownloadAsync(string url, string targetPath, string taskId, IProgress<DownloadProgressInfo> progress, CancellationToken cancellationToken);
        Task<bool> PauseAsync(string taskId);
        Task<bool> ResumeAsync(string taskId);
        Task CancelAsync(string taskId);
        Task<bool> ValidateFileAsync(string filePath, string expectedHash);

    }
}
