using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SDK_Manager_GUI.Converters
{
    public class BoolToInstalledTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b ? Application.Current.FindResource("Common_Installed") as string ?? "Installed" : Application.Current.FindResource("Common_NotInstalled") as string ?? "Not Installed";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
