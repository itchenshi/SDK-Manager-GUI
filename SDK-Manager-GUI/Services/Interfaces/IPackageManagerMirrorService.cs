using System.Collections.Generic;
using System.Threading.Tasks;
using SDK_Manager_GUI.Models;

namespace SDK_Manager_GUI.Services
{
    public interface IPackageManagerMirrorService
    {
        /// <summary>
        /// 启动时预加载所有 SDK 的镜像源地址到缓存
        /// </summary>
        Task PreloadMirrorsAsync();

        /// <summary>
        /// 从缓存获取镜像源地址
        /// </summary>
        string GetCachedMirror(SdkLanguage language);

        /// <summary>
        /// 获取当前包管理器镜像源URL
        /// </summary>
        Task<string> GetCurrentMirrorAsync(SdkLanguage language);

        /// <summary>
        /// 设置包管理器镜像源
        /// </summary>
        Task<bool> SetMirrorAsync(SdkLanguage language, string mirrorUrl);

        /// <summary>
        /// 重置为默认镜像源
        /// </summary>
        Task<bool> ResetToDefaultAsync(SdkLanguage language);

        /// <summary>
        /// 获取预设镜像源列表
        /// </summary>
        IEnumerable<PresetMirrorItem> GetPresetMirrors(SdkLanguage language);

        /// <summary>
        /// 获取 Maven 本地仓库路径
        /// </summary>
        string GetMavenLocalRepository();

        /// <summary>
        /// 设置 Maven 本地仓库路径
        /// </summary>
        bool SetMavenLocalRepository(string path);
    }
}
