using System;
using System.Collections.Generic;
using System.Text;

namespace TBird.Core
{
    public static class EnumUtil
    {
        /// <summary>
        /// Enum値へ変換します。
        /// </summary>
        /// <typeparam name="T">変換後のEnum型</typeparam>
        /// <param name="value">Enum型の文字列表現</param>
        /// <returns></returns>
        public static T ToEnum<T>(string value) where T : struct
        {
            if (Enum.TryParse(value, out T result))
            {
                return result;
            }
            return (T)Enum.Parse(typeof(T), value, true);
        }

        /// <summary>
        /// Enum値へ変換します。
        /// </summary>
        /// <typeparam name="T">変換後のEnum型</typeparam>
        /// <param name="value">Enum型の文字列表現</param>
        /// <returns></returns>
        public static T ToEnum<T>(int value) where T : struct
        {
            return ToEnum<T>(value.ToString());
        }

        /// <summary>
        /// 全列挙値を取得します。
        /// </summary>
        /// <typeparam name="T">取得したい列挙定義</typeparam>
        /// <returns></returns>
        public static IEnumerable<T> GetValues<T>() where T : Enum
        {
            foreach (T value in Enum.GetValues(typeof(T)))
            {
                yield return value;
            }
        }
    }
}
