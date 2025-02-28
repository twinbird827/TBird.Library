using System.Windows;
using System.Windows.Input;

namespace TBird.Wpf.Behaviors
{
    public partial class FrameworkElementBehavior
    {
        public static readonly DependencyProperty MouseOverItemProperty = BehaviorUtil.RegisterAttached(
            "MouseOverItem", typeof(FrameworkElementBehavior), default(IMouseOverItem), OnSetMouseOverItemCallback
        );

        public static IMouseOverItem GetMouseOverItem(DependencyObject obj)
        {
            return (IMouseOverItem)obj.GetValue(MouseOverItemProperty);
        }

        public static void SetMouseOverItem(DependencyObject obj, IMouseOverItem value)
        {
            obj.SetValue(MouseOverItemProperty, value);
        }

        private static void OnSetMouseOverItemCallback(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                BehaviorUtil.SetEventHandler(element,
                    fe => fe.MouseEnter += FrameworkElementBehavior_MouseOverItem_MouseEnter,
                    fe => fe.MouseEnter -= FrameworkElementBehavior_MouseOverItem_MouseEnter
                );
                BehaviorUtil.SetEventHandler(element,
                    fe => fe.MouseLeave += FrameworkElementBehavior_MouseOverItem_MouseLeave,
                    fe => fe.MouseLeave -= FrameworkElementBehavior_MouseOverItem_MouseLeave
                );
            }
        }

        /// <summary>
        /// ﾏｳｽｶｰｿﾙが項目上に存在することを通知します。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void FrameworkElementBehavior_MouseOverItem_MouseEnter(object sender, MouseEventArgs e)
        {
            FrameworkElementBehavior_MouseOverItem(sender, e, true);
        }

        /// <summary>
        /// ﾏｳｽｶｰｿﾙが項目上から離れたことを通知します。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void FrameworkElementBehavior_MouseOverItem_MouseLeave(object sender, MouseEventArgs e)
        {
            FrameworkElementBehavior_MouseOverItem(sender, e, false);
        }

        /// <summary>
        /// ﾏｳｽｶｰｿﾙの状態を紐付くViewModelに伝播させます。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="isMouseOver">ｶｰｿﾙの状態</param>
        private static void FrameworkElementBehavior_MouseOverItem(object sender, MouseEventArgs e, bool isMouseOver)
        {
            if (sender is FrameworkElement element)
            {
                GetMouseOverItem(element).IsMouseOver = isMouseOver;
            }
        }
    }
}