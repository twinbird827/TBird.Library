using System.Globalization;

namespace LanobeReader.Converters;

public class BoolToGoldConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? Color.FromArgb("#FFC107") : Color.FromArgb("#CCCCCC");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
