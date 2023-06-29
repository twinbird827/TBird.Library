using System.Windows;
using System.Windows.Input;

namespace TBird.Wpf.Behaviors
{
    public partial class FrameworkElementBehavior
    {
        public static DependencyProperty KeyDownProperty = BehaviorUtil.RegisterAttached(
            "KeyDown", typeof(FrameworkElementBehavior), default(ICommand), OnSetKeyDownCallback
        );

        public static void SetKeyDown(DependencyObject target, object value)
        {
            target.SetValue(KeyDownProperty, value);
        }

        public static ICommand GetKeyDown(DependencyObject target)
        {
            return (ICommand)target.GetValue(KeyDownProperty);
        }

        private static void OnSetKeyDownCallback(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is FrameworkElement element)
            {
                BehaviorUtil.SetEventHandler(element,
                    (fe) => fe.KeyDown += FrameworkElementBehavior_KeyDown,
                    (fe) => fe.KeyDown -= FrameworkElementBehavior_KeyDown
                );
            }
        }

        /// <summary>
        /// ｷｰ押下時に処理を実行します。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void FrameworkElementBehavior_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                e.Handled = GetKeyDown(element).TryExecute(e);
            }
        }
    }
}