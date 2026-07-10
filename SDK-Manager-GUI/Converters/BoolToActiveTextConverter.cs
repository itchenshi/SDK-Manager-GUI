using System;
using System.Globalization;
using System.Windows.Data;

namespace SDK_Manager_GUI.Converters
{
    public class BoolToActiveTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b ? " (活跃)" : "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
