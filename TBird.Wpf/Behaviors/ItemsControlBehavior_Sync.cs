using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TBird.Core;

namespace TBird.Wpf.Behaviors
{
	public partial class ItemsControlBehavior
	{
		private static Type Type = typeof(ItemsControlBehavior);

		public static DependencyProperty SyncRFooterProperty = BehaviorUtil.RegisterAttached(
			"SyncRFooter", Type, default(RowDefinition), OnSetSyncRFooterCallback
		);

		public static void SetSyncRFooter(DependencyObject target, object value)
		{
			target.SetValue(SyncRFooterProperty, value);
		}

		public static RowDefinition GetSyncRFooter(DependencyObject target)
		{
			return (RowDefinition)target.GetValue(SyncRFooterProperty);
		}

		private static void OnSetSyncRFooterCallback(DependencyObject target, DependencyPropertyChangedEventArgs e)
		{
			if (target is ItemsControl ic)
			{
				BehaviorUtil.Loaded(ic, ItemsControlBehavior_SyncRFooter_Loaded);
			}
		}

		private static void ItemsControlBehavior_SyncRFooter_Loaded(object sender, RoutedEventArgs e)
		{
			if (sender is DependencyObject target)
			{
				ScrollViewerBehavior.SetSyncRFooter(BehaviorUtil.GetScrollViewer(target), GetSyncRFooter(target));
			}
		}

		public static DependencyProperty SyncCFooterProperty = BehaviorUtil.RegisterAttached(
			"SyncCFooter", Type, default(ColumnDefinition), OnSetSyncCFooterCallback
		);

		public static void SetSyncCFooter(DependencyObject target, object value)
		{
			target.SetValue(SyncCFooterProperty, value);
		}

		public static ColumnDefinition GetSyncCFooter(DependencyObject target)
		{
			return (ColumnDefinition)target.GetValue(SyncCFooterProperty);
		}

		private static void OnSetSyncCFooterCallback(DependencyObject target, DependencyPropertyChangedEventArgs e)
		{
			if (target is ItemsControl viewer)
			{
				BehaviorUtil.Loaded(viewer, ItemsControlBehavior_SyncCFooter_Loaded);
			}
		}

		private static void ItemsControlBehavior_SyncCFooter_Loaded(object sender, RoutedEventArgs e)
		{
			if (sender is DependencyObject target)
			{
				ScrollViewerBehavior.SetSyncCFooter(BehaviorUtil.GetScrollViewer(target), GetSyncCFooter(target));
			}
		}

		public static DependencyProperty SyncRScrollProperty = BehaviorUtil.RegisterAttached(
			"SyncRScroll", Type, default(DependencyObject), OnSetSyncRScrollCallback
		);

		public static void SetSyncRScroll(DependencyObject target, object value)
		{
			target.SetValue(SyncRScrollProperty, value);
		}

		public static DependencyObject GetSyncRScroll(DependencyObject target)
		{
			return (DependencyObject)target.GetValue(SyncRScrollProperty);
		}

		private static void OnSetSyncRScrollCallback(DependencyObject target, DependencyPropertyChangedEventArgs e)
		{
			if (target is ItemsControl viewer)
			{
				BehaviorUtil.Loaded(viewer, ItemsControlBehavior_SyncRScroll_Loaded);
			}
		}

		private static void ItemsControlBehavior_SyncRScroll_Loaded(object sender, RoutedEventArgs e)
		{
			if (sender is DependencyObject target)
			{
				ScrollViewerBehavior.SetSyncRScroll(BehaviorUtil.GetScrollViewer(target), BehaviorUtil.GetScrollViewer(GetSyncRScroll(target)));
			}
		}

		public static DependencyProperty SyncCScrollProperty = BehaviorUtil.RegisterAttached(
			"SyncCScroll", Type, default(DependencyObject), OnSetSyncCScrollCallback
		);

		public static void SetSyncCScroll(DependencyObject target, object value)
		{
			target.SetValue(SyncCScrollProperty, value);
		}

		public static DependencyObject GetSyncCScroll(DependencyObject target)
		{
			return (DependencyObject)target.GetValue(SyncCScrollProperty);
		}

		private static void OnSetSyncCScrollCallback(DependencyObject target, DependencyPropertyChangedEventArgs e)
		{
			if (target is ItemsControl viewer)
			{
				BehaviorUtil.Loaded(viewer, ItemsControlBehavior_SyncCScroll_Loaded);
			}
		}

		private static void ItemsControlBehavior_SyncCScroll_Loaded(object sender, RoutedEventArgs e)
		{
			if (sender is DependencyObject target)
			{
				ScrollViewerBehavior.SetSyncCScroll(BehaviorUtil.GetScrollViewer(target), BehaviorUtil.GetScrollViewer(GetSyncCScroll(target)));
			}
		}

		public static DependencyProperty SyncWidthProperty = BehaviorUtil.RegisterAttached(
			"SyncWidth", Type, default(DependencyObject), OnSetSyncWidthCallback
		);

		public static void SetSyncWidth(DependencyObject target, object value)
		{
			target.SetValue(SyncWidthProperty, value);
		}

		public static DependencyObject GetSyncWidth(DependencyObject target)
		{
			return (DependencyObject)target.GetValue(SyncWidthProperty);
		}

		private static void OnSetSyncWidthCallback(DependencyObject target, DependencyPropertyChangedEventArgs e)
		{
			if (target is FrameworkElement element && e.NewValue is ItemsControl ic && BehaviorUtil.GetScrollViewer(ic) is ScrollViewer viewer)
			{
				ScrollChangedEventHandler handler = (sender, args) =>
				{
					if (0 < args.ExtentWidthChange || 0 < args.ViewportWidthChange)
					{
						element.Width = CoreUtil.Arr(viewer.ExtentWidth, viewer.ViewportWidth).Max();
					}
				};
				BehaviorUtil.SetEventHandler(viewer,
					x => x.ScrollChanged += handler,
					x => x.ScrollChanged -= handler
				);
			}
		}
	}
}