namespace TBird.Wpf
{
	public static class WpfToast
	{
		public static void ShowMessage(string title, string message)
		{
#if WINRT
			new ToastContentBuilder()
				.AddText(title)
				.AddText(message)
				.Show();
#endif
		}
	}
}