using System.Threading.Tasks;
using SDK_Manager_GUI.Models;

namespace SDK_Manager_GUI.Services
{
    public interface IConfigService
    {
        Task<AppConfig> GetConfigAsync();
        Task SaveConfigAsync(AppConfig config);
        Task ResetConfigAsync();
        AppConfig GetConfigSync();
    }
}
