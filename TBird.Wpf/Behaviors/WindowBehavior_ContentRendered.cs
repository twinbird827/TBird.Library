using System;
using System.Windows;
using System.Windows.Input;

namespace TBird.Wpf.Behaviors
{
    public partial class WindowBehavior
    {
        public static DependencyProperty ContentRenderedProperty = BehaviorUtil.RegisterAttached(
            "ContentRendered", typeof(WindowBehavior), default(ICommand), OnSetContentRenderedCallback
        );

        public static void SetContentRendered(DependencyObject target, object value)
        {
            target.SetValue(ContentRenderedProperty, value);
        }

        public static ICommand GetContentRendered(DependencyObject target)
        {
            return (ICommand)target.GetValue(ContentRenderedProperty);
        }

        private static void OnSetContentRenderedCallback(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is Window window)
            {
                BehaviorUtil.SetEventHandler(window,
                    (fe) => fe.ContentRendered += WindowBehavior_ContentRendered,
                    (fe) => fe.ContentRendered -= WindowBehavior_ContentRendered
                );
            }
        }

        private static void WindowBehavior_ContentRendered(object sender, EventArgs e)
        {
            if (sender is Window window)
            {
                GetContentRendered(window).TryExecute(null);
            }
        }
    }
}