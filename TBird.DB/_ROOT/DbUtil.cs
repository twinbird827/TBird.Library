using System;

namespace TBird.DB
{
    internal static class DbUtil
    {
        /// <summary>
        /// DBから取得した値を指定した型に変換して取得します。
        /// </summary>
        /// <typeparam name="T">変換後の型</typeparam>
        /// <param name="value">DBから取得した値</param>
        /// <returns></returns>
        public static T GetValue<T>(object value)
        {
            if (value is null || value is DBNull)
            {
                return default(T);
            }
            else if (value is long l)
            {
                return (T)LongConvert<T>(l);
            }
            else if (value is double d)
            {
                return (T)DoubleConvert<T>(d);
            }
            else
            {
                return (T)value;
            }
        }

        /// <summary>
        /// DBから取得した数値を指定した型に変換して取得します。
        /// </summary>
        /// <typeparam name="T">変換後の型</typeparam>
        /// <param name="value">DBから取得した数値</param>
        /// <returns></returns>
        private static object LongConvert<T>(long value)
        {
            if (typeof(T) == typeof(long))
            {
                return value;
            }
            else if (typeof(T) == typeof(int))
            {
                return (int)value;
            }
            else if (typeof(T) == typeof(short))
            {
                return (short)value;
            }
            else
            {
                return value;
            }
        }

        /// <summary>
        /// DBから取得した浮動小数点数を指定した型に変換して取得します。
        /// </summary>
        /// <typeparam name="T">変換後の型</typeparam>
        /// <param name="value">DBから取得した浮動小数点数</param>
        /// <returns></returns>
        private static object DoubleConvert<T>(double value)
        {
            if (typeof(T) == typeof(double))
            {
                return value;
            }
            else if (typeof(T) == typeof(float))
            {
                return (float)value;
            }
            else
            {
                return LongConvert<T>((long)value);
            }
        }
    }
}