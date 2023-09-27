using Microsoft.Toolkit.Uwp.Notifications;

namespace TBird.Wpf
{
	public static class WpfToast
	{
		public static void ShowMessage(string title, string message)
		{
			new ToastContentBuilder()
				.AddText(title)
				.AddText(message)
				.Show();
		}
	}
}