using System;
using System.Collections.Generic;
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

    }
}
