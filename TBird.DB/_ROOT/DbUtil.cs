using System;
using System.Collections.Generic;

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
            if (value != null)
            {
                switch (value)
                {
                    case DBNull _:
                        return default;
                    case T x:
                        return x;
                }

                return (T)_typeconverters[typeof(T)](value);
            }
            else
            {
                return default;
            }
        }

        private static object ToInt16(object value) => value is short x ? x : short.Parse(value.ToString());

        private static object ToInt32(object value) => value is int x ? x : int.Parse(value.ToString());

        private static object ToInt64(object value) => value is long x ? x : long.Parse(value.ToString());

        private static object ToByte(object value) => value is byte x ? x : byte.Parse(value.ToString());

        private static object ToBytes(object value) => value is byte[] x ? x : value;

        private static object ToSingle(object value) => value is float x ? x : float.Parse(value.ToString());

        private static object ToDouble(object value) => value is double x ? x : double.Parse(value.ToString());

        private static object ToDecimal(object value) => value is decimal x ? x : decimal.Parse(value.ToString());

        private static object ToString(object value) => value is string x ? x : value.ToString();

        private static readonly Dictionary<Type, Func<object, object>> _typeconverters = new Dictionary<Type, Func<object, object>>()
        {
            { typeof(short), ToInt16 },
            { typeof(int), ToInt32 },
            { typeof(long), ToInt64 },
            { typeof(byte), ToByte },
            { typeof(byte[]), ToBytes },
            { typeof(float), ToSingle },
            { typeof(double), ToDouble },
            { typeof(string), ToString },
            { typeof(decimal), ToDecimal }
        };
    }
}