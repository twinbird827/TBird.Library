using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;

namespace TBird.Wpf.Behaviors
{
    public partial class FrameworkElementBehavior
    {
        public static DependencyProperty LDoubleClickProperty = BehaviorUtil.RegisterAttached(
            "LDoubleClick", typeof(FrameworkElementBehavior), default(ICommand), OnSetCommandCallback
        );

        public static void SetLDoubleClick(DependencyObject target, object value)
        {
            target.SetValue(LDoubleClickProperty, value);
        }

        public static ICommand GetLDoubleClick(DependencyObject target)
        {
            return (ICommand)target.GetValue(LDoubleClickProperty);
        }

        public static DependencyProperty LSingleClickProperty = BehaviorUtil.RegisterAttached(
            "LSingleClick", typeof(FrameworkElementBehavior), default(ICommand), OnSetCommandCallback
        );

        public static void SetLSingleClick(DependencyObject target, object value)
        {
            target.SetValue(LSingleClickProperty, value);
        }

        public static ICommand GetLSingleClick(DependencyObject target)
        {
            return (ICommand)target.GetValue(LSingleClickProperty);
        }

        private static void OnSetCommandCallback(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            var control = target as FrameworkElement;

            BehaviorUtil.SetEventHandler(control,
                (fe) => fe.PreviewMouseLeftButtonDown += FrameworkElement_LeftMouseDown_MouseLeftButtonDown,
                (fe) => fe.PreviewMouseLeftButtonDown -= FrameworkElement_LeftMouseDown_MouseLeftButtonDown
            );
        }

        /// <summary>
        /// ﾏｳｽｸﾘｯｸ時に処理を実行します。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void FrameworkElement_LeftMouseDown_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe)
            {
                // ｸﾘｯｸ数によって実行するｺﾏﾝﾄﾞを変更する。
                var command = e.ClickCount == 1
                    ? GetLSingleClick(fe)
                    : e.ClickCount == 2
                    ? GetLDoubleClick(fe)
                    : null;

                if (command == null) return;

                // ﾌｫｰｶｽｸﾘｱ
                ControlUtil.ClearFocus(fe);

                // ｺﾏﾝﾄﾞ実行
                command.Execute(e);

                // 処理済にする。
                e.Handled = command.TryExecute(e);
            }
        }
    }
}
