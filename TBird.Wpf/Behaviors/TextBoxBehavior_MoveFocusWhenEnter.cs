using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TBird.Wpf.Behaviors
{
	public partial class TextBoxBehavior
	{
		public static DependencyProperty MoveFocusWhenEnterProperty = BehaviorUtil.RegisterAttached(
			"MoveFocusWhenEnter", typeof(TextBoxBehavior), false, OnSetMoveFocusWhenEnterCallback
		);

		public static void SetMoveFocusWhenEnter(DependencyObject target, object value)
		{
			target.SetValue(MoveFocusWhenEnterProperty, value);
		}

		public static bool GetMoveFocusWhenEnter(DependencyObject target)
		{
			return (bool)target.GetValue(MoveFocusWhenEnterProperty);
		}

		public static DependencyProperty MoveFocusableProperty = BehaviorUtil.RegisterAttached(
			"MoveFocusable", typeof(TextBoxBehavior), default(FrameworkElement), null
		);

		public static void SetMoveFocusable(DependencyObject target, object value)
		{
			target.SetValue(MoveFocusableProperty, value);
		}

		public static FrameworkElement GetMoveFocusable(DependencyObject target)
		{
			return (FrameworkElement)target.GetValue(MoveFocusableProperty);
		}

		private static void OnSetMoveFocusWhenEnterCallback(DependencyObject target, DependencyPropertyChangedEventArgs e)
		{
			if (target is TextBox textbox)
			{
				BehaviorUtil.SetEventHandler(textbox,
					(fe) => fe.PreviewKeyDown += TextBoxBehavior_MoveFocusWhenEnter_PreviewKeyDown,
					(fe) => fe.PreviewKeyDown -= TextBoxBehavior_MoveFocusWhenEnter_PreviewKeyDown
				);
			}
		}

		private static void TextBoxBehavior_MoveFocusWhenEnter_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			// Enterｷｰ以外は中断
			if (e.Key != Key.Enter) return;

			if (sender is TextBox textbox && GetMoveFocusWhenEnter(textbox))
			{
				var nextfocusable = GetMoveFocusable(textbox);
				if (nextfocusable != null)
				{
					// 移動先の項目を取得できた場合、その項目へﾌｫｰｶｽを遷移させた後ﾌｫｰｶｽをｸﾘｱする。
					nextfocusable.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
					ControlUtil.ClearFocus(nextfocusable);
				}
				else
				{
					var shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
					var focusNavigationDirection = shift ? FocusNavigationDirection.Previous : FocusNavigationDirection.Next;
					// 次のﾌｫｰｶｽへ移動する。
					textbox.MoveFocus(new TraversalRequest(focusNavigationDirection));
				}
				e.Handled = true;
			}
		}
	}
}