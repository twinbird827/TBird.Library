using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace TBird.Wpf.Behaviors
{
    public partial class ButtonBehavior
    {
        public static DependencyProperty ClearFocusProperty = BehaviorUtil.RegisterAttached(
            "ClearFocus", typeof(ButtonBehavior), false, OnSetClearFocusCallback
        );
        public static void SetClearFocus(DependencyObject target, object value)
        {
            target.SetValue(ClearFocusProperty, value);
        }
        public static bool GetClearFocus(DependencyObject target)
        {
            return (bool)target.GetValue(ClearFocusProperty);
        }

        private static void OnSetClearFocusCallback(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is Button button)
            {
                BehaviorUtil.SetEventHandler(button,
                    (fe) => fe.Click += ButtonBehavior_ClearFocus,
                    (fe) => fe.Click -= ButtonBehavior_ClearFocus
                );
            }
            else if (target is FrameworkElement element)
            {
                BehaviorUtil.SetEventHandler(element,
                    (fe) => fe.PreviewMouseLeftButtonDown += PreviewMouseLeftButtonDown_ClearFocus,
                    (fe) => fe.PreviewMouseLeftButtonDown -= PreviewMouseLeftButtonDown_ClearFocus
                );
            }
        }

        /// <summary>
        /// ﾎﾞﾀﾝ押下時にﾌｫｰｶｽをｸﾘｱします。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void ButtonBehavior_ClearFocus(object sender, EventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                // ﾌｫｰｶｽｸﾘｱ
                ControlUtil.ClearFocus(element);
            }
        }

        /// <summary>
        /// ﾎﾞﾀﾝ押下時にﾌｫｰｶｽをｸﾘｱします。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void PreviewMouseLeftButtonDown_ClearFocus(object sender, MouseButtonEventArgs e)
        {
            // 共通処理
            ButtonBehavior_ClearFocus(sender, e);

            // ｺﾝﾄﾛｰﾙ個別の処理
            if (sender is ToggleButton toggle && toggle.Command != null)
            {
                // ﾄｸﾞﾙﾎﾞﾀﾝなら明示的にｺﾏﾝﾄﾞを実行する。
                toggle.Command.TryExecute(toggle.CommandParameter);
                // 処理済みにする
                e.Handled = true;
            }
        }

    }
}
