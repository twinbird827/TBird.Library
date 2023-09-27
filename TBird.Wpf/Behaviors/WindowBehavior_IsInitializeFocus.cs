using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TBird.Wpf.Behaviors
{
	public partial class WindowBehavior
	{
		public static DependencyProperty IsInitializeFocusProperty = BehaviorUtil.RegisterAttached(
			"IsInitializeFocus", typeof(WindowBehavior), false, OnSetIsInitializeFocusCallback
		);

		public static void SetIsInitializeFocus(DependencyObject target, object value)
		{
			target.SetValue(IsInitializeFocusProperty, value);
		}

		public static bool GetIsInitializeFocus(DependencyObject target)
		{
			return (bool)target.GetValue(IsInitializeFocusProperty);
		}

		private static void OnSetIsInitializeFocusCallback(DependencyObject target, DependencyPropertyChangedEventArgs e)
		{
			if (target is Window window)
			{
				BehaviorUtil.Loaded(window, Window_IsInitializeFocus_Loaded);
			}
		}

		/// <summary>
		/// Window起動時に初期ﾌｫｰｶｽを設定します。
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private static void Window_IsInitializeFocus_Loaded(object sender, RoutedEventArgs e)
		{
			if (sender is Window window && GetIsInitializeFocus(window))
			{
				var target = BehaviorUtil.EnumerateDescendantObjects<Control>(window)
					.FirstOrDefault(x => (x is TextBox t && !t.IsReadOnly && t.IsEnabled) || (x is PasswordBox p && p.IsEnabled));

				if (target != null)
				{
					BehaviorUtil.Invoke(target, FocusManager.SetFocusedElement, window, target);
				}
				else
				{
					window.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
				}
			}
		}
	}
}