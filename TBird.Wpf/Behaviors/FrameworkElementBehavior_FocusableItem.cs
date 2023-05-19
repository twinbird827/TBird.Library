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
        public static DependencyProperty FocusableItemProperty = BehaviorUtil.RegisterAttached(
            "FocusableItem", typeof(FrameworkElementBehavior), default(IFocusableItem), OnSetFocusableItemCallback
        );
        public static void SetFocusableItem(DependencyObject target, object value)
        {
            target.SetValue(FocusableItemProperty, value);
        }
        public static IFocusableItem GetFocusableItem(DependencyObject target)
        {
            return (IFocusableItem)target.GetValue(FocusableItemProperty);
        }

        private static void OnSetFocusableItemCallback(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is FrameworkElement element)
            {
                BehaviorUtil.SetEventHandler(element,
                    (fe) => fe.IsKeyboardFocusWithinChanged += FrameworkElementBehavior_FocusableItem,
                    (fe) => fe.IsKeyboardFocusWithinChanged -= FrameworkElementBehavior_FocusableItem
                );
            }
        }

        /// <summary>
        /// ﾌｫｰｶｽIN/OUT時に紐付くViewModelにﾌｫｰｶｽの状態を伝播させます。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void FrameworkElementBehavior_FocusableItem(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                GetFocusableItem(element).IsFocused = element.IsKeyboardFocusWithin;
            }
        }
    }
}
