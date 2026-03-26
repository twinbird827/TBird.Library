using System;
using System.Globalization;
using System.Windows.Data;

namespace TBird.Wpf.Converters
{
	public class Int2StringConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value.ToString();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return int.Parse(value.ToString());
		}
	}
}