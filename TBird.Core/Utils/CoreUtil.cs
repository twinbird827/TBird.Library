using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TBird.Core
{
    public static class CoreUtil
    {
        /// <summary>
        /// 対象文字配列のうち最初の空文字以外の文字を取得します。
        /// </summary>
        /// <param name="args">対象文字配列</param>
        public static string Nvl(params string[] args)
        {
            return args.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? string.Empty;
        }

        /// <summary>
        /// 対象文字配列のうち最初のｾﾞﾛ以外の数値を取得します。
        /// </summary>
        /// <param name="args">対象文字配列</param>
        public static double Nvl(params double[] args)
        {
            return args.FirstOrDefault(s => s != 0);
        }

        public static string Nvl(params object[] args)
        {
            return Nvl(args.Select(x => x is string s ? s : x.ToString()).ToArray());
        }

        /// <summary>
        /// 非同期でｷｬﾝｾﾙ可能な待機処理を行います。
        /// </summary>
        /// <param name="delay">待機時間(ﾐﾘ秒)</param>
        /// <param name="token">ｷｬﾝｾﾙﾄｰｸﾝ</param>
        public static async Task<bool> Delay(int delay, CancellationToken token)
        {
            if (delay == 0)
            {
                return true;
            }

            try
            {
                await Task.Delay(delay, token);
                return true;
            }
            catch (TaskCanceledException)
            {
                return false;
            }
        }

        /// <summary>
        /// 非同期でｷｬﾝｾﾙ可能な待機処理を行います。
        /// </summary>
        /// <param name="delay">待機時間(ﾐﾘ秒)</param>
        public static async Task<bool> Delay(int delay)
        {
            return await Delay(delay, CancellationToken.None);
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
        /// 指定した同期処理を非同期で実行し、結果を取得します。
        /// </summary>
        /// <typeparam name="T">結果のﾃﾞｰﾀ型</typeparam>
        /// <param name="func">同期処理</param>
        /// <returns></returns>
        public static Task<T> WaitAsync<T>(Func<T> func)
        {
            // 結果を返却
            return Task.Run(func);
        }

        /// <summary>
        /// 空のｺｰﾙﾊﾞｯｸ
        /// </summary>
        public static AsyncCallback AsyncCallbackEmpty = tmp => { };

        /// <summary>
        /// ﾌﾟﾛｾｽを実行します。実行するﾌﾟﾛｾｽが複数存在する場合ﾊﾟｲﾌﾟします。
        /// </summary>
        /// <param name="pis">ﾌﾟﾛｾｽ実行情報</param>
        public static void Execute(params ProcessStartInfo[] pis)
        {
            Process process = null;
            foreach (var pi in pis)
            {
                pi.CreateNoWindow = true;
                pi.UseShellExecute = false;
                pi.RedirectStandardInput = process != null;
                pi.RedirectStandardOutput = true;

                var now = Process.Start(pi);

                if (process != null)
                {
                    using (process)
                    using (var reader = process.StandardOutput)
                    using (var writer = now.StandardInput)
                    {
                        writer.AutoFlush = true;
                        string line = reader.ReadToEnd();

                        writer.Write(line);
                    }
                }

                process = now;
            }

            using (process)
            {
                process.WaitForExit();
            }
        }

    }
}
