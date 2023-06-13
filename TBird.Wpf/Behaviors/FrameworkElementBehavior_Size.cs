using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace TBird.Wpf.Behaviors
{
    public partial class FrameworkElementBehavior
    {
        public static DependencyProperty WidthProperty = BehaviorUtil.RegisterAttached(
            "Width", typeof(FrameworkElementBehavior), default(double), OnSetWidthCallback
        );

        public static void SetWidth(DependencyObject target, object value)
        {
            target.SetValue(WidthProperty, value);
        }

        public static double GetWidth(DependencyObject target)
        {
            return (double)target.GetValue(WidthProperty);
        }

        private static void OnSetWidthCallback(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is FrameworkElement fe && e.NewValue is double width && !double.IsNaN(width) && 0 < width)
            {
                fe.Width = width;
                fe.MaxWidth = width;
                fe.MinWidth = width;
            }
        }

        public static DependencyProperty HeightProperty = BehaviorUtil.RegisterAttached(
            "Height", typeof(FrameworkElementBehavior), default(double), OnSetHeightCallback
        );

        public static void SetHeight(DependencyObject target, object value)
        {
            target.SetValue(HeightProperty, value);
        }

        public static double GetHeight(DependencyObject target)
        {
            return (double)target.GetValue(HeightProperty);
        }

        private static void OnSetHeightCallback(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is FrameworkElement fe && e.NewValue is double height && !double.IsNaN(height) && 0 < height)
            {
                fe.Height = height;
                fe.MaxHeight = height;
                fe.MinHeight = height;
            }
        }
    }
}
