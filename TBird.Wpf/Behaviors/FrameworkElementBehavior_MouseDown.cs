using System.Windows;
using System.Windows.Input;

namespace TBird.Wpf.Behaviors
{
	public partial class FrameworkElementBehavior
	{
		public static DependencyProperty MouseDownProperty = BehaviorUtil.RegisterAttached(
			"MouseDown", typeof(FrameworkElementBehavior), default(ICommand), OnSetMouseDownCallback
		);

		public static void SetMouseDown(DependencyObject target, object value)
		{
			target.SetValue(MouseDownProperty, value);
		}

		public static ICommand GetMouseDown(DependencyObject target)
		{
			return (ICommand)target.GetValue(MouseDownProperty);
		}

		private static void OnSetMouseDownCallback(DependencyObject target, DependencyPropertyChangedEventArgs e)
		{
			if (target is FrameworkElement element)
			{
				BehaviorUtil.SetEventHandler(element,
					(fe) => fe.MouseDown += FrameworkElementBehavior_MouseDown,
					(fe) => fe.MouseDown -= FrameworkElementBehavior_MouseDown
				);
			}
		}

		/// <summary>
		/// ﾏｳｽｸﾘｯｸ時に処理を実行します。
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private static void FrameworkElementBehavior_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (sender is FrameworkElement element)
			{
				e.Handled = GetMouseDown(element).TryExecute(e);
			}
		}
	}
}