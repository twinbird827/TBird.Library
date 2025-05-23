using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace TBird.Wpf.Behaviors
{
	public partial class WindowBehavior
	{
		public static DependencyProperty ClosingProperty = BehaviorUtil.RegisterAttached(
			"Closing", typeof(WindowBehavior), default(ICommand), OnSetClosingCallback
		);

		public static void SetClosing(DependencyObject target, object value)
		{
			target.SetValue(ClosingProperty, value);
		}

		public static ICommand GetClosing(DependencyObject target)
		{
			return (ICommand)target.GetValue(ClosingProperty);
		}

		private static void OnSetClosingCallback(DependencyObject target, DependencyPropertyChangedEventArgs e)
		{
			if (target is Window window)
			{
				BehaviorUtil.SetEventHandler(window,
					(fe) => fe.Closing += WindowBehavior_Closing,
					(fe) => fe.Closing -= WindowBehavior_Closing
				);
			}
		}

		private static void WindowBehavior_Closing(object sender, CancelEventArgs e)
		{
			if (sender is Window window)
			{
				GetClosing(window).TryExecute(e);
			}
		}
	}
}