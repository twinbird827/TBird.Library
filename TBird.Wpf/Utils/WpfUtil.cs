using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using TBird.Core;

namespace TBird.Wpf
{
	public static class WpfUtil
	{
		/// <summary>
		/// UIのﾃﾞｨｽﾊﾟｯﾁｬ
		/// </summary>
		private static Dispatcher _dispatcher;

		/// <summary>
		/// 指定したﾃﾞｨｽﾊﾟｯﾁｬがUIｽﾚｯﾄﾞではない場合、ｱｲﾄﾞﾙ状態になったらｼｬｯﾄﾀﾞｳﾝします。
		/// </summary>
		/// <param name="dispatcher">対象ﾃﾞｨｽﾊﾟｯﾁｬ</param>
		public static void BeginInvokeShutdown(Dispatcher dispatcher)
		{
			if (dispatcher != null && !OnUI(dispatcher.Thread))
			{
				dispatcher.BeginInvokeShutdown(DispatcherPriority.SystemIdle);
			}
		}

		private static Disposer<Dispatcher> GetShutdownDispatcherDisposer() => new Disposer<Dispatcher>(Dispatcher.CurrentDispatcher, BeginInvokeShutdown);

		/// <summary>
		/// 現在のﾃﾞｨｽﾊﾟｯﾁｬがUI上かどうか確認します。
		/// </summary>
		/// <returns></returns>
		public static bool OnUI(Thread thread)
		{
			if (Application.Current == null) return false;

			// UIのﾃﾞｨｽﾊﾟｯﾁｬ取得
			_dispatcher = _dispatcher ?? Application.Current.Dispatcher;
			// UIとｶﾚﾝﾄのﾃﾞｨｽﾊﾟｯﾁｬを比較した結果を返却
			return _dispatcher.Thread == thread;
		}

		/// <summary>
		/// 現在のﾃﾞｨｽﾊﾟｯﾁｬがUI上かどうか確認します。
		/// </summary>
		/// <returns></returns>
		public static bool OnUI() => OnUI(Thread.CurrentThread);

		public static SynchronizationContext GetContext()
		{
			return _context = _context ?? ExecuteOnUI(() => SynchronizationContext.Current);
		}

		private static SynchronizationContext _context;

		/// <summary>
		/// UI上で処理を実行します。
		/// </summary>
		/// <param name="action"></param>
		public static void ExecuteOnUI(Action action)
		{
			if (!OnUI())
			{
				// 現在のｽﾚｯﾄﾞがUIのﾃﾞｨｽﾊﾟｯﾁｬ上ではない場合、UIのﾃﾞｨｽﾊﾟｯﾁｬ上で処理を実行する。
				WaitDoEvents(_dispatcher.BeginInvoke(action));
			}
			else
			{
				action();
			}
		}

		/// <summary>
		/// UI上で処理を実行します。
		/// </summary>
		/// <param name="action"></param>
		public static T ExecuteOnUI<T>(Func<T> action)
		{
			if (!OnUI())
			{
				// 現在のｽﾚｯﾄﾞがUIのﾃﾞｨｽﾊﾟｯﾁｬ上ではない場合、UIのﾃﾞｨｽﾊﾟｯﾁｬ上で処理を実行する。
				return (T)WaitDoEvents(_dispatcher.BeginInvoke(action)).Result;
			}
			else
			{
				return action();
			}
		}

		/// <summary>
		/// UIｽﾚｯﾄﾞの処理を徐々に実行します。
		/// </summary>
		/// <param name="x">処理ﾀｽｸ</param>
		/// <returns></returns>
		private static DispatcherOperation WaitDoEvents(DispatcherOperation x)
		{
			using (GetShutdownDispatcherDisposer())
			{
				while (x.Status == DispatcherOperationStatus.Executing || x.Status == DispatcherOperationStatus.Pending)
				{
					x.Wait(TimeSpan.FromMilliseconds(32));
					DoEvents();
				}
				return x;
			}
		}

		private static T ReturnAndShutdownDispatcher<T>(Func<T> func)
		{
			using (GetShutdownDispatcherDisposer())
			{
				return func();
			}
		}

		public static Task BackgroundAsync(Func<Task> func)
		{
			return OnUI() ? Task.Run(func) : ReturnAndShutdownDispatcher(func);
		}

		public static Task<T> BackgroundAsync<T>(Func<Task<T>> func)
		{
			return OnUI() ? Task.Run(func) : ReturnAndShutdownDispatcher(func);
		}

		public static Task BackgroundAsync(Action action)
		{
			return BackgroundAsync(() => TaskUtil.WaitAsync(action));
		}

		public static Task<T> ExecuteOnBackground<T>(Func<T> func)
		{
			return BackgroundAsync(() => TaskUtil.WaitAsync(func));
		}

		/// <summary>
		/// 画面ｲﾍﾞﾝﾄをすべて実行します。
		/// </summary>
		public static void DoEvents()
		{
			DispatcherFrame frame = new DispatcherFrame();
			var callback = new DispatcherOperationCallback(obj =>
			{
				((DispatcherFrame)obj).Continue = false;
				return null;
			});
			Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, callback, frame);
			Dispatcher.PushFrame(frame);
		}

		/// <summary>
		/// ﾃﾞｻﾞｲﾝﾓｰﾄﾞかどうか確認します。
		/// </summary>
		/// <returns></returns>
		public static bool IsDesignMode()
		{
			// Check for design mode.
			return (bool)DesignerProperties
				.IsInDesignModeProperty
				.GetMetadata(typeof(DependencyObject))
				.DefaultValue;
		}

	}
}