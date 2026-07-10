using System.Collections.Generic;
using System.Threading.Tasks;

namespace SDK_Manager_GUI.Services
{
    public interface IEnvironmentManager
    {
        Task<bool> SetEnvironmentVariableAsync(string name, string value, bool systemLevel);
        Task<string> GetEnvironmentVariableAsync(string name, bool systemLevel);
        Task<bool> AddToPathAsync(string path, bool systemLevel);
        Task<bool> RemoveFromPathAsync(string path, bool systemLevel);
        Task RemoveWindowsAppsPythonAliasAsync();
        Task BackupEnvironmentVariablesAsync();
        Task RestoreEnvironmentVariablesAsync();
        void RefreshCurrentProcessEnvironment();
    }
}
