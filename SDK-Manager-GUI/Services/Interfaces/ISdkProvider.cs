using System.Collections.Generic;
using System.Threading.Tasks;
using SDK_Manager_GUI.Models;

namespace SDK_Manager_GUI.Services
{
    public interface ISdkProvider
    {
        SdkLanguage Language { get; }
        Task<IEnumerable<SdkVersion>> GetAvailableVersionsAsync();
        Task<string> GetDownloadUrlAsync(string version, string mirrorBaseUrl);
        Task<SdkVersion> ParseVersionAsync(string versionString);
        Task<bool> ValidateInstallationAsync(string installPath);
        string GetExecutableName();
        string GetEnvironmentVariableName();
        string GetBinPath(string installPath);
        /// <summary>
        /// 获取 Scripts 目录路径（Python embeddable 安装 pip 后有 Scripts 目录，其他 SDK 返回 null）
        /// </summary>
        string GetScriptsPath(string installPath);
        /// <summary>
        /// 获取用于检测版本的命令参数（如 --version 或 -version）
        /// </summary>
        string GetVersionArgument();
    }
}
