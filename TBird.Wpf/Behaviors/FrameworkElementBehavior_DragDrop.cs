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
        public static DependencyProperty DragDropProperty = BehaviorUtil.RegisterAttached(
            "DragDrop", typeof(FrameworkElementBehavior), default(ICommand), OnSetDragDropCallback
        );
        public static void SetDragDrop(DependencyObject target, object value)
        {
            target.SetValue(DragDropProperty, value);
        }
        public static ICommand GetDragDrop(DependencyObject target)
        {
            return (ICommand)target.GetValue(DragDropProperty);
        }

        private static void OnSetDragDropCallback(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is FrameworkElement fe)
            {
                BehaviorUtil.Loaded(fe, (sender, args) => fe.AllowDrop = true);

                BehaviorUtil.SetEventHandler(fe,
                    x => x.DragOver += FrameworkElementBehavior_DragDrop,
                    x => x.DragOver -= FrameworkElementBehavior_DragDrop
                );

                BehaviorUtil.SetEventHandler(fe,
                    x => x.Drop += FrameworkElementBehavior_Drop,
                    x => x.Drop -= FrameworkElementBehavior_Drop
                );
            }
        }

        /// <summary>
        /// 起動時に処理を実行します。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void FrameworkElementBehavior_DragDrop(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private static void FrameworkElementBehavior_Drop(object sender, DragEventArgs e)
        {
            if (sender is FrameworkElement element && GetDragDrop(element) is ICommand command)
            {
                command.TryExecute(e);
            }
        }

    }
}
