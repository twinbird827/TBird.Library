using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

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
        public static async Task WaitAsync(Action action)
        {
            var iar = action.BeginInvoke(null, null);
            await WaitAsync(iar);
            action.EndInvoke(iar);
        }

        /// <summary>
        /// 指定した同期処理を非同期で実行し、処理を待機します。
        /// </summary>
        /// <typeparam name="TParam">引数のﾃﾞｰﾀ型</typeparam>
        /// <param name="value">同期処理の引数</param>
        /// <param name="action">同期処理</param>
        /// <returns></returns>
        public static async Task WaitAsync<TParam>(TParam value, Action<TParam> action)
        {
            var iar = action.BeginInvoke(value, null, null);
            await WaitAsync(iar);
            action.EndInvoke(iar);
        }

        /// <summary>
        /// 指定した同期処理を非同期で実行し、結果を取得します。
        /// </summary>
        /// <typeparam name="TResult">結果のﾃﾞｰﾀ型</typeparam>
        /// <param name="func">同期処理</param>
        /// <param name="def">処理失敗時のﾃﾞﾌｫﾙﾄ値</param>
        /// <returns></returns>
        public static async Task<TResult> WaitAsync<TResult>(Func<TResult> func, TResult def = default(TResult))
        {
            var iar = func.BeginInvoke(null, null);
            return await WaitAsync(iar)
                ? func.EndInvoke(iar)
                : def;
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
        public static async Task<TResult> WaitAsync<TParam, TResult>(TParam value, Func<TParam, TResult> func, TResult def = default(TResult))
        {
            var iar = func.BeginInvoke(value, null, null);
            return await WaitAsync(iar)
                ? func.EndInvoke(iar)
                : def;
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

    }
}