using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TBird.Core
{
	public static class Locker
	{
		/// <summary>
		/// 内部用ｾﾏﾌｫﾏﾈｰｼﾞｬ
		/// </summary>
		private static Dictionary<string, Manager> _manages { get; set; } = new Dictionary<string, Manager>();

		/// <summary>
		/// 内部用ﾛｯｸｲﾝｽﾀﾝｽ
		/// </summary>
		private static Manager _lock { get; } = new Manager();

		private static void AddManager(string key)
		{
			if (_manages.ContainsKey(key)) return;

			using (_lock.Lock())
			{
				if (_manages.ContainsKey(key)) return;

				_manages.Add(key, new Manager());
			}
		}

		private static async Task AddManagerAsync(string key)
		{
			if (_manages.ContainsKey(key)) return;

			using (await _lock.LockAsync())
			{
				if (_manages.ContainsKey(key)) return;

				_manages.Add(key, new Manager());
			}
		}

		public static string GetNewLockKey()
		{
			return Guid.NewGuid().ToString();
		}

		public static string GetNewLockKey(object x)
		{
			return GetNewLockKey(x.GetType());
		}

		public static string GetNewLockKey(Type x)
		{
			return $"[{x.FullName}] {GetNewLockKey()}";
		}

		/// <summary>
		/// 処理を待機し、Disposeすることで処理を開放できるｲﾝｽﾀﾝｽを取得します。
		/// </summary>
		/// <returns></returns>
		public static async Task<IDisposable> LockAsync(string key)
		{
			await AddManagerAsync(key);
			return await _manages[key].LockAsync();
		}

		/// <summary>
		/// 処理を待機し、Disposeすることで処理を開放できるｲﾝｽﾀﾝｽを取得します。
		/// </summary>
		/// <returns></returns>
		public static IDisposable Lock(string key)
		{
			AddManager(key);
			return _manages[key].Lock();
		}

		/// <summary>
		/// 処理が解放されるまで非同期で待機します。
		/// </summary>
		/// <returns></returns>
		public static void Dispose(string key)
		{
			if (key == null) return;

			if (!_manages.ContainsKey(key)) return;

			using (_lock.Lock())
			{
				if (!_manages.ContainsKey(key)) return;

				using (_manages[key].Lock()) { }

				_manages[key]._slim.Dispose();
				_manages.Remove(key);
			}
		}

		/// <summary>
		/// 待機中の処理数をｶｳﾝﾄします。
		/// </summary>
		/// <returns></returns>
		public static int Count(string key)
		{
			return _manages.ContainsKey(key)
				? _manages[key]._cnt
				: 0;
		}

		private class Manager
		{
			public Manager()
			{
				_slim = new SemaphoreSlim(1, 1);
				_cnt = 0;
			}

			internal SemaphoreSlim _slim;

			public int _cnt;

			public Task WaitAsync()
			{
				Interlocked.Increment(ref _cnt);
				return _slim.WaitAsync();
			}

			public void Wait()
			{
				Interlocked.Increment(ref _cnt);
				_slim.Wait();
			}

			public int Release()
			{
				Interlocked.Decrement(ref _cnt);
				return _slim.Release();
			}

			public async Task<IDisposable> LockAsync()
			{
				await WaitAsync();
				return new Disposer<Manager>(this, arg => arg.Release());
			}

			public IDisposable Lock()
			{
				Wait();
				return new Disposer<Manager>(this, arg => arg.Release());
			}
		}
	}
}