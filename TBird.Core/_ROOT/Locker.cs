using System;
using System.Collections.Generic;
using System.Text;
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
        private static SemaphoreSlim _lock { get; } = new SemaphoreSlim(1, 1);

        /// <summary>
        /// 内部用ﾏﾈｰｼﾞｬ作成ﾒｿｯﾄﾞ
        /// </summary>
        /// <returns></returns>
        private static async Task CreateManager(string key)
        {
            if (_manages.ContainsKey(key)) return;

            using (await _lock.LockAsync())
            {
                if (_manages.ContainsKey(key)) return;

                _manages.Add(key, new Manager());
            }
        }

        /// <summary>
        /// 処理を待機します。
        /// </summary>
        /// <returns></returns>
        public static async Task WaitAsync(string key)
        {
            await CreateManager(key);
            await _manages[key].WaitAsync();
        }

        /// <summary>
        /// 処理を開放します。
        /// </summary>
        /// <returns></returns>
        public static int Release(string key)
        {
            return _manages[key].Release();
        }

        /// <summary>
        /// 処理を待機し、Disposeすることで処理を開放できるｲﾝｽﾀﾝｽを取得します。
        /// </summary>
        /// <returns></returns>
        public static async Task<IDisposable> LockAsync(string key)
        {
            await CreateManager(key);
            return await _manages[key].LockAsync();
        }

        /// <summary>
        /// 処理を待機し、Disposeすることで処理を開放できるｲﾝｽﾀﾝｽを取得します。
        /// </summary>
        /// <returns></returns>
        public static IDisposable Lock(string key)
        {
            _lock.Wait();
            try
            {
                if (!_manages.ContainsKey(key))
                {
                    _manages.Add(key, new Manager());
                }
            }
            finally
            {
                _lock.Release();
            }

            return _manages[key].Lock();
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

            private SemaphoreSlim _slim;

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
