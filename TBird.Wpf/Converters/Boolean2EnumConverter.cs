using System;
using System.Globalization;
using System.Windows.Data;

namespace TBird.Wpf.Converters
{
	public class Boolean2EnumConverter : IValueConverter
	{
		/// <summary>
		/// 任意のEnum値がConverterParameterに設定したEnum値と同値であるかどうか判別するbool値へ変換します。
		/// </summary>
		/// <param name="value"></param>
		/// <param name="targetType"></param>
		/// <param name="parameter"></param>
		/// <param name="culture"></param>
		/// <returns></returns>
		public virtual object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (parameter is string s && Enum.IsDefined(value.GetType(), value))
			{
				return (int)Enum.Parse(value.GetType(), s) == (int)value;
			}
			else
			{
				return System.Windows.DependencyProperty.UnsetValue;
			}
		}

		public virtual object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return parameter is string s
				? Enum.Parse(targetType, s)
				: System.Windows.DependencyProperty.UnsetValue;
		}
	}
}