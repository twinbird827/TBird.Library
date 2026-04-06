using System.Globalization;

namespace LanobeReader.Converters;

public class BoolToGrayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // IsRead == true → Gray text, IsRead == false → normal text
        return value is true ? Colors.Gray : Colors.Black;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
