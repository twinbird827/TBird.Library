using System.Windows;
using System.Windows.Media.Imaging;

namespace TBird.Wpf.Behaviors
{
	public partial class BitmapImageBehavior
	{
		public static DependencyProperty SyncProperty = BehaviorUtil.RegisterAttached(
			"Sync", typeof(BitmapImageBehavior), default(BitmapImage), OnSetSyncCallback
		);

		public static void SetSync(DependencyObject target, object value)
		{
			target.SetValue(SyncProperty, value);
		}

		public static BitmapImage GetSync(DependencyObject target)
		{
			return (BitmapImage)target.GetValue(SyncProperty);
		}

		private static void OnSetSyncCallback(DependencyObject target, DependencyPropertyChangedEventArgs e)
		{
			if (target is FrameworkElement fe)
			{
				BehaviorUtil.SetEventHandler(fe,
					x => x.SizeChanged += BitmapImageBehavior_Sync_SizeChanged,
					x => x.SizeChanged -= BitmapImageBehavior_Sync_SizeChanged
				);
			}
		}

		private static void BitmapImageBehavior_Sync_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			if (sender is FrameworkElement fe && GetSync(fe) is BitmapImage image)
			{
				image.DecodePixelWidth = (int)fe.ActualWidth;
				image.DecodePixelHeight = (int)fe.ActualHeight;
			}
		}

	}
}