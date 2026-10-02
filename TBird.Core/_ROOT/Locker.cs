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
		private static Dictionary<string, Locker> _locker = new();

		public static Locker Create(int pararell = 1, bool sync = false) => Create(Guid.NewGuid().ToString(), pararell, sync);

		public static Locker Create(string key, int pararell = 1, bool sync = false)
		{
			lock (_locker)
			{
				var instance = _locker.TryGetValue(key, out Locker locker)
					? locker
					: _locker[key] = new Locker(key, pararell, sync);

				Interlocked.Increment(ref instance._createcount);

				return instance;
			}
		}

		private Locker(string key, int pararell, bool sync)
		{
			_key = key;
			_pararell = pararell;
			_sync = sync;
		}

		private int _createcount;

		private string _key;

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
			lock (_lock)
			{
				action();
			}
		}

		public T LockSync<T>(Func<T> func)
		{
			lock (_lock)
			{
				return func();
			}
		}

		private object _lock = new object();

		private bool disposedValue;

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					lock (_lock)
					{
						Interlocked.Decrement(ref _createcount);
						// 参照がまだ残っている場合は中断
						if (_createcount > 0) return;
					}

					// TODO: マネージド状態を破棄します (マネージド オブジェクト)
					if (_slim != null)
					{
						var task = Task.Run(async () =>
						{
							await _slim.WaitAsync().ConfigureAwait(false);
							_slim.Release();
							_slim.Dispose();
							_slim = null;
						});
						if (_sync) task.GetAwaiter().GetResult();
					}

					lock (_locker)
					{
						_locker.Remove(_key);
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
	}
}