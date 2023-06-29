using System;
using System.Globalization;
using System.Windows.Data;
using TBird.Core;

namespace TBird.Wpf.Converters
{
    public class Enum2StringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var tmp = value as Enum;
            if (tmp != null)
            {
                return tmp.GetLabel();
            }
            else
            {
                return parameter;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}