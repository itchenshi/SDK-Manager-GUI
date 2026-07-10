using System;
using System.Threading;
using System.Threading.Tasks;

namespace SDK_Manager_GUI.Services
{
    public interface IDialogService
    {
        Task<bool> ShowConfirmAsync(string title, string message);
        Task ShowInfoAsync(string title, string message);
        Task ShowErrorAsync(string title, string message);
        Task<string> ShowFolderBrowserDialogAsync(string description);
        Task ShowProgressAsync(string title, string message, IProgress<double> progress, CancellationToken cancellationToken);
    }
}
