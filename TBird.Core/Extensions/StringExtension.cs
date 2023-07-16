using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace TBird.Core
{
    public static class StringExtension
    {
        /// <summary>
        /// 左辺から指定した長さの文字を取得します。
        /// </summary>
        /// <param name="s">対象文字</param>
        /// <param name="length">長さ</param>
        /// <param name="padding">長さが足りない場合に埋める文字(ﾃﾞﾌｫﾙﾄ空白)</param>
        /// <returns></returns>
        public static string Left(this string s, int length, char padding = ' ')
        {
            return s.Mid(0, length, padding);
        }

        /// <summary>
        /// 取得する位置と長さを指定して文字を取得します。
        /// </summary>
        /// <param name="s">対象文字</param>
        /// <param name="start">取得する開始位置</param>
        /// <param name="length">取得する文字の長さ</param>
        /// <param name="padding">長さが足りない場合に埋める文字(ﾃﾞﾌｫﾙﾄ空白)</param>
        /// <returns></returns>
        public static string Mid(this string s, int start, int length, char padding = ' ')
        {
            var array = Enumerable.Repeat(padding, length).ToArray();

            for (var i = start; s != null && i < start + length && i < s.Length; i++)
            {
                array[i - start] = s[i];
            }

            return array.GetString();
        }

        /// <summary>
        /// 取得する位置と長さを指定して文字を取得します。
        /// </summary>
        /// <param name="s">対象文字</param>
        /// <param name="start">取得する開始位置</param>
        /// <param name="padding">長さが足りない場合に埋める文字(ﾃﾞﾌｫﾙﾄ空白)</param>
        /// <returns></returns>
        public static string Mid(this string s, int start, char padding = ' ')
        {
            return s.Mid(start, s.Length - start, padding);
        }

        /// <summary>
        /// 右辺から指定した長さの文字を取得します。
        /// </summary>
        /// <param name="s">対象文字</param>
        /// <param name="length">長さ</param>
        /// <param name="padding">長さが足りない場合に埋める文字(ﾃﾞﾌｫﾙﾄ空白)</param>
        /// <returns></returns>
        public static string Right(this string s, int length, char padding = ' ')
        {
            if (string.IsNullOrEmpty(s))
            {
                return new string(padding, length);
            }

            var start = s.Length - length;
            if (start < 0)
            {
                return new string(padding, length - start) + s;
            }
            else
            {
                return s.Mid(start, length, padding);
            }
        }

        /// <summary>
        /// 文字を指定した区切り文字で配列に分割します。
        /// </summary>
        /// <param name="s">文字</param>
        /// <param name="split">区切り文字</param>
        /// <returns></returns>
        public static IEnumerable<string> Split(this string s, string split)
        {
            if (string.IsNullOrEmpty(s)) yield break;

            var prev = 0;
            int next;
            while ((next = s.IndexOf(split, prev)) != -1)
            {
                yield return s.Substring(prev, next - prev);
                prev = next + split.Length;
            }
        }
    }
}