using System;
using System.Globalization;
using System.Windows.Data;

namespace TBird.Wpf.Converters
{
	public class BooleanReverseConverter : IValueConverter
	{
		public virtual object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			bool x = true;
			if (value is bool)
			{
				x = (bool)value;
			}
			else if (value is bool?)
			{
				var tmp = (bool?)value;
				x = tmp.HasValue && tmp.Value;
			}
			return !x;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return Convert(value, targetType, parameter, culture);
		}
	}
}