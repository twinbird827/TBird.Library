using Microsoft.Web.WebView2.WinForms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace TBird.IO.Html
{
	public static class HeadlessWebView2
	{
		private static Dispatcher _dispatcher;

		private static Dispatcher GetDispatcher()
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

			var dispatcher = tcs.Task.Result; // メンバ変数に dispatcher を保存

			// 表のﾃﾞｨｽﾊﾟｯﾁｬｰが終了するタイミングで、こちらのﾃﾞｨｽﾊﾟｯﾁｬｰも終了する
			Dispatcher.CurrentDispatcher.ShutdownStarted += (s, e) => dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);

			return dispatcher;
		}

		//public static void Call(Action<WebView2> action)
		//{
		//	var dispatcher = GetDispatcher();

		//	dispatcher.Invoke(() => action(new WebView2()));
		//}

		public static async Task Call(Uri uri, Func<WebView2, Task> func)
		{
			var dispatcher = _dispatcher ?? GetDispatcher();

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

			Shutdown(dispatcher);
		}

		public static void Shutdown(Dispatcher dispatcher)
		{
			if (dispatcher != null)
			{
				dispatcher.InvokeShutdown();
			}
		}
	}
}