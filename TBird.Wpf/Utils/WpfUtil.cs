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
		/// 現在のﾃﾞｨｽﾊﾟｯﾁｬがUI上かどうか確認します。
		/// </summary>
		/// <returns></returns>
		public static bool OnUI()
		{
			// UIとｶﾚﾝﾄのﾃﾞｨｽﾊﾟｯﾁｬを比較した結果を返却
			return OnUI(Thread.CurrentThread);
		}

		/// <summary>
		/// 指定したｽﾚｯﾄﾞがUI上であるか確認します。
		/// </summary>
		/// <param name="thread"></param>
		/// <returns></returns>
		public static bool OnUI(Thread thread)
		{
			while (_dispatcher == null && Application.Current == null)
			{
				Thread.Sleep(1);
			}

			// UIのﾃﾞｨｽﾊﾟｯﾁｬ取得
			_dispatcher = _dispatcher ?? Application.Current.Dispatcher;
			// UIとｶﾚﾝﾄのﾃﾞｨｽﾊﾟｯﾁｬを比較した結果を返却
			return _dispatcher.Thread == thread;
		}

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
				_dispatcher.Invoke(action);
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
				return _dispatcher.Invoke(action);
			}
			else
			{
				return action();
			}
		}

		public static Task ExecuteOnBACK(Func<Task> func)
		{
			if (OnUI())
			{
				return Task.Run(() => ExecuteOnBACK(func));
			}
			using (GetDispatcherShutdownDisposable())
			{
				return func();
			}
		}

		public static Task<T> ExecuteOnBACK<T>(Func<Task<T>> func)
		{
			if (OnUI())
			{
				return Task.Run(() => ExecuteOnBACK(func));
			}
			using (GetDispatcherShutdownDisposable())
			{
				return func();
			}
		}

		public static Task ExecuteOnBACK(Action action)
		{
			return ExecuteOnBACK(() => TaskUtil.WaitAsync(action));
		}

		public static Task<T> ExecuteOnBACK<T>(Func<T> func)
		{
			return ExecuteOnBACK(() => TaskUtil.WaitAsync(func));
		}

		public static IDisposable GetDispatcherShutdownDisposable()
		{
			return Dispatcher.CurrentDispatcher.Disposer(x =>
			{
				if (x != null && !OnUI(x.Thread))
				{
					if (!x.HasShutdownStarted)
					{
						DispatcherFrame frame = new DispatcherFrame();
						var callback = new DispatcherOperationCallback(obj =>
						{
							((DispatcherFrame)obj).Continue = false;
							return null;
						});
						x.BeginInvoke(DispatcherPriority.Background, callback, frame);
						Dispatcher.PushFrame(frame);
						x.InvokeShutdown();
					}
				}
			});
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

		/// <summary>
		/// 画面ｲﾍﾞﾝﾄをすべて実行します。
		/// </summary>
		public static void DoEvents()
		{
			ExecuteOnUI(() =>
			{
				DispatcherFrame frame = new DispatcherFrame();
				var callback = new DispatcherOperationCallback(obj =>
				{
					((DispatcherFrame)obj).Continue = false;
					return null;
				});
				Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, callback, frame);
				Dispatcher.PushFrame(frame);
			});
		}

	}
}