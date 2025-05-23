using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TBird.Wpf.Behaviors
{
	public partial class TextBoxBehavior
	{
		public static DependencyProperty IsSelectAllWhenGotFocusProperty = BehaviorUtil.RegisterAttached(
			"IsSelectAllWhenGotFocus", typeof(TextBoxBehavior), false, OnSetIsSelectAllWhenGotFocusCallback
		);

		public static void SetIsSelectAllWhenGotFocus(DependencyObject target, object value)
		{
			target.SetValue(IsSelectAllWhenGotFocusProperty, value);
		}

		public static bool GetIsSelectAllWhenGotFocus(DependencyObject target)
		{
			return (bool)target.GetValue(IsSelectAllWhenGotFocusProperty);
		}

		private static void OnSetIsSelectAllWhenGotFocusCallback(DependencyObject target, DependencyPropertyChangedEventArgs e)
		{
			if (target is TextBox textbox)
			{
				BehaviorUtil.SetEventHandler(textbox,
					(fe) => fe.GotFocus += TextBoxBehavior_IsSelectAllWhenGotFocus_GotFocus,
					(fe) => fe.GotFocus -= TextBoxBehavior_IsSelectAllWhenGotFocus_GotFocus
				);
				BehaviorUtil.SetEventHandler(textbox,
					(fe) => fe.PreviewMouseLeftButtonDown += TextBoxBehavior_IsSelectAllWhenGotFocus_PreviewMouseLeftButtonDown,
					(fe) => fe.PreviewMouseLeftButtonDown -= TextBoxBehavior_IsSelectAllWhenGotFocus_PreviewMouseLeftButtonDown
				);
			}
		}

		private static void TextBoxBehavior_IsSelectAllWhenGotFocus_GotFocus(object sender, RoutedEventArgs e)
		{
			if (sender is TextBox textbox && GetIsSelectAllWhenGotFocus(textbox))
			{
				textbox.SelectAll();
			}
		}

		private static void TextBoxBehavior_IsSelectAllWhenGotFocus_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (sender is TextBox textbox && GetIsSelectAllWhenGotFocus(textbox))
			{
				e.Handled = textbox.Focus();
			}
		}
	}
}