using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TBird.Wpf;

namespace Moviewer.Core.Styles
{
	public class LinkedTextBlock : TextBlock
	{
		private static Type Owner = typeof(LinkedTextBlock);

		static LinkedTextBlock()
		{
			DefaultStyleKeyProperty.OverrideMetadata(Owner, new FrameworkPropertyMetadata(Owner));
		}

		/// <summary>
		/// 表示文字
		/// </summary>
		public ICommand Command
		{
			get => (ICommand)GetValue(CommandProperty);
			set => SetValue(CommandProperty, value);
		}

		public static readonly DependencyProperty CommandProperty =
			BehaviorUtil.Register(nameof(Command), Owner, default(ICommand), null);

	}
}