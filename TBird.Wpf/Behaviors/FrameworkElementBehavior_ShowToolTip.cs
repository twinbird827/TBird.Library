using System.Windows;
using System.Windows.Controls;

namespace TBird.Wpf.Behaviors
{
    public partial class FrameworkElementBehavior
    {
        public static DependencyProperty ShowToolTipProperty = BehaviorUtil.RegisterAttached(
            "ShowToolTip", typeof(FrameworkElementBehavior), false, OnSetShowToolTipCallback
        );

        public static void SetShowToolTip(DependencyObject target, object value)
        {
            target.SetValue(ShowToolTipProperty, value);
        }

        public static bool GetShowToolTip(DependencyObject target)
        {
            return (bool)target.GetValue(ShowToolTipProperty);
        }

        private static void OnSetShowToolTipCallback(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is FrameworkElement element)
            {
                BehaviorUtil.SetEventHandler(element,
                    (fe) => fe.SizeChanged += FrameworkElementBehavior_ShowToolTip_SizeChanged,
                    (fe) => fe.SizeChanged -= FrameworkElementBehavior_ShowToolTip_SizeChanged
                );

                BehaviorUtil.Loaded(element, FrameworkElementBehavior_ShowToolTip_Loaded);
            }
        }

        private static void FrameworkElementBehavior_ShowToolTip_Loaded(object sender, RoutedEventArgs e)
        {
            FrameworkElementBehavior_ShowToolTip_SizeChanged(sender, null);
        }

        private static void FrameworkElementBehavior_ShowToolTip_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!GetShowToolTip(sender as DependencyObject)) return;

            if (sender is ContentControl cc)
            {
                var ft = ControlUtil.GetFormattedText(cc);

                SetToolTip(cc, cc.ActualWidth < ft.Width + cc.Padding.Left + cc.Padding.Right
                    ? cc.Content
                    : null
                );
            }
            else if (sender is TextBlock tb)
            {
                var ft = ControlUtil.GetFormattedText(tb);

                SetToolTip(tb, tb.ActualWidth < ft.Width
                    ? tb.Text
                    : null
                );
            }
        }
    }
}