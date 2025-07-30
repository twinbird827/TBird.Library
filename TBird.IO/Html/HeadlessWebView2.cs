using Microsoft.Web.WebView2.WinForms;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace TBird.IO.Html
{
	public static class HeadlessWebView2
	{
		/// <summary>
		/// STAｽﾚｯﾄﾞ上で作成したﾃﾞｨｽﾊﾟｯﾁｬを取得します。
		/// </summary>
		/// <returns></returns>
		private static Task<Dispatcher> GetSTADispatcher()
		{
			// ｽﾚｯﾄﾞを起動して、そこで dispatcherを実行する
			var tcs = new TaskCompletionSource<Dispatcher>();
			var thread = new Thread(new ThreadStart(() =>
			{
				tcs.SetResult(Dispatcher.CurrentDispatcher);
				Dispatcher.Run();
			}));
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();

			return tcs.Task;
		}

		/// <summary>
		/// <see cref="WebView2"/>を利用して<see cref="Uri"/>を開き、指定した処理を行います。
		/// </summary>
		/// <param name="uri">開くURI</param>
		/// <param name="func">URI上で処理したい内容</param>
		/// <returns></returns>
		public static async Task Call(Uri uri, Func<WebView2, Task> func)
		{
			var dispatcher = await GetSTADispatcher();

			var tcs = new TaskCompletionSource<Task>();
			dispatcher.Invoke(() =>
			{
				var view = new WebView2();

				view.Source = uri;
				view.NavigationCompleted += (sender, e) =>
				{
					tcs.SetResult(func(view));
				};
			});
			await tcs.Task;
			await tcs.Task.Result;

			if (dispatcher != null)
			{
				dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
			}
		}
	}
}