using System.Globalization;

namespace TBird.Maui.Converters;

public class BoolToOpacityConverter : BindableObject, IValueConverter
{
    public static readonly BindableProperty TrueOpacityProperty =
        BindableProperty.Create(nameof(TrueOpacity), typeof(double), typeof(BoolToOpacityConverter), 1.0);

    public static readonly BindableProperty FalseOpacityProperty =
        BindableProperty.Create(nameof(FalseOpacity), typeof(double), typeof(BoolToOpacityConverter), 0.4);

    public double TrueOpacity
    {
        get => (double)GetValue(TrueOpacityProperty);
        set => SetValue(TrueOpacityProperty, value);
    }

    public double FalseOpacity
    {
        get => (double)GetValue(FalseOpacityProperty);
        set => SetValue(FalseOpacityProperty, value);
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? TrueOpacity : FalseOpacity;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
