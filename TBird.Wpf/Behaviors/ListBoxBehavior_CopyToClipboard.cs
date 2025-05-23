using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TBird.Core;

namespace TBird.Wpf.Behaviors
{
	public partial class ListBoxBehavior
	{
		public static readonly DependencyProperty CopyToClipboardProperty = BehaviorUtil.RegisterAttached(
			"CopyToClipboard", typeof(ListBoxBehavior), false, OnSetCopyToClipboardCallback
		);

		public static bool GetCopyToClipboard(DependencyObject obj)
		{
			return (bool)obj.GetValue(CopyToClipboardProperty);
		}

		public static void SetCopyToClipboard(DependencyObject obj, bool value)
		{
			obj.SetValue(CopyToClipboardProperty, value);
		}

		private static void OnSetCopyToClipboardCallback(DependencyObject sender, DependencyPropertyChangedEventArgs e)
		{
			if (sender is ListBox listbox)
			{
				BehaviorUtil.Loaded(listbox, (sender, e) =>
				{
					if (GetCopyToClipboard(listbox))
					{
						listbox.ContextMenu = listbox.ContextMenu ?? new ContextMenu();
						listbox.ContextMenu.Items.Add(BehaviorUtil.CreateMenuItem(WpfConst.L_Copy, (sender, e) =>
						{
							CopyToClipboard(listbox);
						}));
					}
				});
				BehaviorUtil.SetEventHandler(listbox,
					fe => fe.PreviewKeyDown += ListBoxBehavior_CopyToClipboard_PreviewKeyDown,
					fe => fe.PreviewKeyDown -= ListBoxBehavior_CopyToClipboard_PreviewKeyDown
				);
			}
		}

		private static void ListBoxBehavior_CopyToClipboard_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (sender is ListBox listbox && GetCopyToClipboard(listbox))
			{
				if (e.Key == Key.C && EnumUtil.IsIncluded(Keyboard.Modifiers, ModifierKeys.Control))
				{
					// ｸﾘｯﾌﾟﾎﾞｰﾄﾞにｺﾋﾟｰしてｷｰ入力をｷｬﾝｾﾙ
					CopyToClipboard(listbox);
					e.Handled = true;
				}
			}
		}

		private static void CopyToClipboard(ListBox listbox)
		{
			var sb = new StringBuilder();

			foreach (var item in listbox.SelectedItems)
			{
				if (item is ICopyToClipboard x)
				{
					sb.AppendLine(x.CopyToClipboard());
				}
				else
				{
					sb.AppendLine(item.ToString());
				}
			}

			Clipboard.SetText(sb.ToString());
		}
	}
}