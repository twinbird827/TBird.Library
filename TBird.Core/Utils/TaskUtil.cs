using System;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace TBird.Core
{
	public static class TaskUtil
	{
		/// <summary>
		/// 非同期でｷｬﾝｾﾙ可能な待機処理を行います。
		/// </summary>
		/// <param name="delay">待機時間(ﾐﾘ秒)</param>
		/// <param name="token">ｷｬﾝｾﾙﾄｰｸﾝ</param>
		public static async Task<bool> Delay(int delay, CancellationTokenSource cts)
		{
			if (delay == 0)
			{
				return true;
			}

			return await Task.Delay(delay, cts != null ? cts.Token : CancellationToken.None).TryCatch();
		}

		/// <summary>
		/// 非同期でｷｬﾝｾﾙ可能な待機処理を行います。
		/// </summary>
		/// <param name="delay">待機時間(ﾐﾘ秒)</param>
		public static async Task<bool> Delay(int delay)
		{
			return await Delay(delay, null);
		}

		/// <summary>
		/// 指定した同期処理を非同期で実行します。
		/// </summary>
		/// <param name="action">同期処理</param>
		public static async void Background(Action action)
		{
			await WaitAsync(action).ConfigureAwait(false);
		}

		/// <summary>
		/// 指定した同期処理を非同期で実行し、処理を待機します。
		/// </summary>
		/// <param name="action">同期処理</param>
		/// <returns></returns>
		public static Task WaitAsync(Action action)
		{
			return Task.Run(action);
		}

		/// <summary>
		/// 指定した同期処理を非同期で実行し、処理を待機します。
		/// </summary>
		/// <typeparam name="TParam">引数のﾃﾞｰﾀ型</typeparam>
		/// <param name="value">同期処理の引数</param>
		/// <param name="action">同期処理</param>
		/// <returns></returns>
		public static Task WaitAsync<TParam>(TParam value, Action<TParam> action)
		{
			return Task.Run(() => action(value));
		}

		/// <summary>
		/// 指定した同期処理を非同期で実行し、結果を取得します。
		/// </summary>
		/// <typeparam name="TResult">結果のﾃﾞｰﾀ型</typeparam>
		/// <param name="func">同期処理</param>
		/// <param name="def">処理失敗時のﾃﾞﾌｫﾙﾄ値</param>
		/// <returns></returns>
		public static Task<TResult> WaitAsync<TResult>(Func<TResult> func)
		{
			return Task.Run(func);
		}

		/// <summary>
		/// 指定した同期処理を非同期で実行し、結果を取得します。
		/// </summary>
		/// <typeparam name="TParam">引数のﾃﾞｰﾀ型</typeparam>
		/// <typeparam name="TResult">結果のﾃﾞｰﾀ型</typeparam>
		/// <param name="value">同期処理の引数</param>
		/// <param name="func">同期処理</param>
		/// <param name="def">処理失敗時のﾃﾞﾌｫﾙﾄ値</param>
		/// <returns></returns>
		public static Task<TResult> WaitAsync<TParam, TResult>(TParam value, Func<TParam, TResult> func)
		{
			return Task.Run(() => func(value));
		}

		public static async Task<bool> WaitAsync(IAsyncResult iar)
		{
			while (!iar.IsCompleted)
			{
				await Task.Delay(16);
			}
			return iar.IsCompleted;
		}

		public static Task<bool> WaitAsync(IAsyncResult iar, TimeSpan timeout, CancellationTokenSource cts = null)
		{
			return WaitAsync(iar).Timeout(timeout, cts);
		}

		public static Task<bool> WaitAsync(IAsyncResult iar, CancellationTokenSource cts)
		{
			return WaitAsync(iar).Cts(cts);
		}

		public static void Wait(Task task)
		{
			while (!task.IsCompleted) Thread.Sleep(10);

			if (task.Exception != null)
			{
				throw new SystemException("An exception occurred in Task.", task.Exception);
			}
		}

		public static T Wait<T>(Task<T> task)
		{
			Wait((Task)task);
			return task.Result;
		}

	}
}