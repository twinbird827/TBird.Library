using System.Windows;

namespace TBird.Wpf.Behaviors
{
    public partial class WindowBehavior
    {
        public static DependencyProperty DialogResultProperty = BehaviorUtil.RegisterAttached(
            "DialogResult", typeof(WindowBehavior), default(bool?), OnSetDialogResultCallback
        );

        public static void SetDialogResult(DependencyObject target, object value)
        {
            target.SetValue(DialogResultProperty, value);
        }

        public static bool? GetDialogResult(DependencyObject target)
        {
            return (bool?)target.GetValue(DialogResultProperty);
        }

        private static void OnSetDialogResultCallback(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is Window window)
            {
                window.DialogResult = (bool?)e.NewValue;
                window.Close();
            }
        }
    }
}