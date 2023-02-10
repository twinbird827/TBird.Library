using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace TBird.Wpf.Converters
{
    public class Boolean2VisibilityCollapsedConverter : Boolean2VisibilityConverter
    {
        protected override Visibility FalseVisibility => Visibility.Collapsed;
    }
}
