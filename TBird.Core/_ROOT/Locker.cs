using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TBird.Core
{
	public class Locker : IDisposable
	{
		public Locker(int pararell = 1, bool sync = false)
		{
			_pararell = pararell;
			_sync = sync;
		}

		private bool _sync;

		private int _pararell;

		public int WaitingCount
		{
			get => _WaitingCount;
		}
		private int _WaitingCount;

		public async Task<IDisposable> LockAsync()
		{
			// 破棄時の対応
			if (disposedValue) throw new ObjectDisposedException(nameof(Locker));
			// ｾﾏﾌｫ取得
			var slim = GetSlim();
			// 待機開始
			Interlocked.Increment(ref _WaitingCount);
			// ﾛｯｸ取得
			await slim.WaitAsync().ConfigureAwait(false);
			// 待機終了
			Interlocked.Decrement(ref _WaitingCount);
			// ﾛｯｸ開放(処理終了後)
			return slim.Disposer(x => x.Release());
		}

		private SemaphoreSlim GetSlim()
		{
			return LockSync(() => _slim ??= new SemaphoreSlim(_pararell, _pararell));
		}

		private SemaphoreSlim? _slim;

		public void LockSync(Action action)
		{
			using (_lock.Lock())
			{
				action();
			}
		}

		public T LockSync<T>(Func<T> func)
		{
			using (_lock.Lock())
			{
				return func();
			}
		}

		private FastSpinLock _lock;

		private bool disposedValue;

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					// TODO: マネージド状態を破棄します (マネージド オブジェクト)
					if (_slim != null)
					{
						var task = Task.Run(async () =>
						{
							await _slim.WaitAsync().ConfigureAwait(false);
							_slim.Release();
							_slim.Dispose();
						});
						if (_sync) task.GetAwaiter().GetResult();
					}
				}

				// TODO: アンマネージド リソース (アンマネージド オブジェクト) を解放し、ファイナライザーをオーバーライドします
				// TODO: 大きなフィールドを null に設定します
				disposedValue = true;
			}
		}

		// // TODO: 'Dispose(bool disposing)' にアンマネージド リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします
		// ~Locker()
		// {
		//     // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
		//     Dispose(disposing: false);
		// }

		public void Dispose()
		{
			// このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}

		private struct FastSpinLock
		{
			private const int SYNC_ENTER = 1;
			private const int SYNC_EXIT = 0;
			private int _syncFlag;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private void Enter()
			{
				if (Interlocked.CompareExchange(ref _syncFlag, SYNC_ENTER, SYNC_EXIT) == SYNC_ENTER)
				{
					Spin();
				}
				return;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private void Exit() => Volatile.Write(ref _syncFlag, SYNC_EXIT);

			[MethodImpl(MethodImplOptions.NoInlining)]
			private void Spin()
			{
				var spinner = new SpinWait();
				spinner.SpinOnce();
				while (Interlocked.CompareExchange(ref _syncFlag, SYNC_ENTER, SYNC_EXIT) == SYNC_ENTER)
				{
					spinner.SpinOnce();
				}
			}

			public IDisposable Lock()
			{
				Enter();
				return this.Disposer(x => x.Exit());
			}
		}
	}
}