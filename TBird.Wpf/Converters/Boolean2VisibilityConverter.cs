using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace TBird.Wpf.Converters
{
    public abstract class Boolean2VisibilityConverter : IValueConverter
    {
        private static BooleanToVisibilityConverter _inner = new BooleanToVisibilityConverter();

        protected abstract Visibility FalseVisibility { get; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (Visibility)_inner.Convert(value, targetType, parameter, culture) == Visibility.Visible
                ? Visibility.Visible
                : FalseVisibility;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return _inner.ConvertBack(value, targetType, parameter, culture);
        }
    }
}