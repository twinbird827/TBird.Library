using System.Globalization;

namespace LanobeReader.Converters;

public class BoolToColorConverter : BindableObject, IValueConverter
{
    public static readonly BindableProperty TrueColorProperty =
        BindableProperty.Create(nameof(TrueColor), typeof(Color), typeof(BoolToColorConverter), Colors.Black);

    public static readonly BindableProperty FalseColorProperty =
        BindableProperty.Create(nameof(FalseColor), typeof(Color), typeof(BoolToColorConverter), Colors.Gray);

    public Color TrueColor
    {
        get => (Color)GetValue(TrueColorProperty);
        set => SetValue(TrueColorProperty, value);
    }

    public Color FalseColor
    {
        get => (Color)GetValue(FalseColorProperty);
        set => SetValue(FalseColorProperty, value);
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? TrueColor : FalseColor;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
