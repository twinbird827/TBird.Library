using System.Collections;
using System.Globalization;

namespace LanobeReader.Converters;

public class HasValueConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            null => false,
            bool b => b,
            string s => !string.IsNullOrEmpty(s),
            ICollection c => c.Count > 0,
            IEnumerable e => HasAny(e),
            int i => i != 0,
            long l => l != 0,
            double d => d != 0,
            float f => f != 0,
            decimal m => m != 0,
            _ => true,
        };
    }

    private static bool HasAny(IEnumerable source)
    {
        var enumerator = source.GetEnumerator();
        try
        {
            return enumerator.MoveNext();
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
