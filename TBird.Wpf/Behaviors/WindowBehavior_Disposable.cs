using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace TBird.Wpf.Behaviors
{
    public partial class WindowBehavior
    {
        public static DependencyProperty DisposableProperty = BehaviorUtil.RegisterAttached(
            "Disposable", typeof(WindowBehavior), default(IDisposable), OnSetDisposableCallback
        );
        public static void SetDisposable(DependencyObject target, object value)
        {
            target.SetValue(DisposableProperty, value);
        }
        public static IDisposable GetDisposable(DependencyObject target)
        {
            return (IDisposable)target.GetValue(DisposableProperty);
        }

        private static void OnSetDisposableCallback(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is Window window)
            {
                BehaviorUtil.SetEventHandler(window,
                    (fe) => fe.Closed += WindowBehavior_Disposable_Closed,
                    (fe) => fe.Closed -= WindowBehavior_Disposable_Closed
                );
            }
        }

        private static void WindowBehavior_Disposable_Closed(object sender, EventArgs e)
        {
            if (sender is Window window)
            {
                GetDisposable(window).Dispose();
            }
        }
    }
}
