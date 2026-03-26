using MahApps.Metro.IconPacks;
using System.Windows;
using System.Windows.Controls;
using TBird.Wpf;

namespace Moviewer.Core.Styles
{
	public class IconPacksButton : Button
	{
		static IconPacksButton()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(IconPacksButton), new FrameworkPropertyMetadata(typeof(IconPacksButton)));
		}

		/// <summary>
		/// 表示文字
		/// </summary>
		public string Text
		{
			get => (string)GetValue(TextProperty);
			set => SetValue(TextProperty, value);
		}

		public static readonly DependencyProperty TextProperty =
			BehaviorUtil.Register(nameof(Text), typeof(IconPacksButton), default(string), null);

		/// <summary>
		/// ﾎﾞﾀﾝ構成の向き
		/// </summary>
		public Orientation Orientation
		{
			get => (Orientation)GetValue(OrientationProperty);
			set => SetValue(OrientationProperty, value);
		}

		public static readonly DependencyProperty OrientationProperty =
			BehaviorUtil.Register(nameof(Orientation), typeof(IconPacksButton), Orientation.Vertical, null);

		/// <summary>
		/// ｱｲｺﾝの種類
		/// </summary>
		public PackIconMaterialKind Kind
		{
			get => (PackIconMaterialKind)GetValue(KindProperty);
			set => SetValue(KindProperty, value);
		}

		public static readonly DependencyProperty KindProperty =
			BehaviorUtil.Register(nameof(Kind), typeof(IconPacksButton), default(PackIconMaterialKind), null);
	}
}