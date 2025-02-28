using System.Windows;

namespace TBird.Wpf.Converters
{
    public class Boolean2VisibilityCollapsedConverter : Boolean2VisibilityConverter
    {
        protected override Visibility FalseVisibility => Visibility.Collapsed;
    }
}