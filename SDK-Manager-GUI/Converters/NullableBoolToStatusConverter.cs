using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SDK_Manager_GUI.Converters
{
    public class NullableBoolToStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return Application.Current.FindResource("Common_NotTested") as string ?? "Not Tested";
            if (value is bool b) return b ? Application.Current.FindResource("Common_Success") as string ?? "Success" : Application.Current.FindResource("Common_Fail") as string ?? "Failed";
            return Application.Current.FindResource("Common_NotTested") as string ?? "Not Tested";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
