using System.Windows;

namespace TBird.Wpf.Converters
{
    public class Boolean2VisibilityHiddenConverter : Boolean2VisibilityConverter
    {
        protected override Visibility FalseVisibility => Visibility.Hidden;
    }
}