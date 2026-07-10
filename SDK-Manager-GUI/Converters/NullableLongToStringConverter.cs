using System;
using System.Globalization;
using System.Windows.Data;

namespace SDK_Manager_GUI.Converters
{
    public class NullableLongToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return "-";
            if (value is long l) return l.ToString();
            return "-";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
