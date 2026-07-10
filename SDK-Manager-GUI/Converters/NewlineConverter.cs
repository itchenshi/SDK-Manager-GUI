using System;
using System.Globalization;
using System.Windows.Data;

namespace SDK_Manager_GUI.Converters
{
    public class NewlineConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                return str.Replace("\\n", Environment.NewLine).Replace("\n", Environment.NewLine);
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
