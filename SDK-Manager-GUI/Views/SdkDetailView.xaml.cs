using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SDK_Manager_GUI.ViewModels;

namespace SDK_Manager_GUI.Views
{
    public partial class SdkDetailView : UserControl
    {
        public SdkDetailView()
        {
            InitializeComponent();
            DataContextChanged += SdkDetailView_DataContextChanged;
        }

        private void SdkDetailView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is INotifyPropertyChanged oldVm)
                oldVm.PropertyChanged -= OnViewModelPropertyChanged;
            if (e.NewValue is INotifyPropertyChanged newVm)
                newVm.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SdkDetailViewModel.ShowReleaseDateColumn) && DataContext is SdkDetailViewModel vm)
            {
                ReleaseDateColumn.Visibility = vm.ShowReleaseDateColumn ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}
