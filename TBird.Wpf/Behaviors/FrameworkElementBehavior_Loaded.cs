using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace TBird.Wpf.Behaviors
{
    public partial class FrameworkElementBehavior
    {
        public static DependencyProperty LoadedProperty = BehaviorUtil.RegisterAttached(
            "Loaded", typeof(FrameworkElementBehavior), default(ICommand), OnSetLoadedCallback
        );
        public static void SetLoaded(DependencyObject target, object value)
        {
            target.SetValue(LoadedProperty, value);
        }
        public static ICommand GetLoaded(DependencyObject target)
        {
            return (ICommand)target.GetValue(LoadedProperty);
        }

        private static void OnSetLoadedCallback(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is FrameworkElement fe)
            {
                BehaviorUtil.Loaded(fe, FrameworkElementBehavior_Loaded);
            }
        }

        /// <summary>
        /// 起動時に処理を実行します。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void FrameworkElementBehavior_Loaded(object sender, EventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                GetLoaded(element).TryExecute(e);
            }
        }
    }
}
