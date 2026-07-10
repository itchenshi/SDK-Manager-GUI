using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace SDK_Manager_GUI.Services
{
    public class DialogService : IDialogService
    {
        public Task<bool> ShowConfirmAsync(string title, string message)
        {
            return Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
                return result == MessageBoxResult.Yes;
            }).Task;
        }

        public Task ShowInfoAsync(string title, string message)
        {
            return Application.Current.Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
            }).Task;
        }

        public Task ShowErrorAsync(string title, string message)
        {
            return Application.Current.Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            }).Task;
        }

        public Task<string> ShowFolderBrowserDialogAsync(string description)
        {
            return Application.Current.Dispatcher.InvokeAsync(() =>
            {
                using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
                {
                    dialog.Description = description;
                    dialog.ShowNewFolderButton = true;
                    var result = dialog.ShowDialog();
                    return result == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
                }
            }).Task;
        }

        public Task ShowProgressAsync(string title, string message, IProgress<double> progress, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
