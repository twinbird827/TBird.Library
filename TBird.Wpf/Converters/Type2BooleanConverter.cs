using System;
using System.Globalization;
using System.Windows.Data;

namespace TBird.Wpf.Converters
{
    public class Type2BooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value.GetType() == parameter as Type;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}