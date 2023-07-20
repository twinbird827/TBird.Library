using Codeplex.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace TBird.Core
{
    public static class DynamicUtil
    {
        public static object O(dynamic value, string key)
        {
            var keyarr = key.Split('.');
            var keyfst = keyarr[0];
            if (!value.IsDefined(keyfst))
            {
                return null;
            }
            var keyvalue = value[keyfst];
            return keyarr.Length == 1
                ? keyvalue
                : O(keyvalue, keyarr.Skip(1).GetString("."));
        }

        public static T T<T>(dynamic value, string key, Func<string, T> func)
        {
            var keyvalue = O(value, key);
            return keyvalue == null
                ? default(T)
                : keyvalue is T t
                ? t
                : func(keyvalue is string s ? s : $"{keyvalue}");
        }

        public static T T<T>(dynamic value, string key)
        {
            Func<string, T> func = s => default;
            return T<T>(value, key, func);
        }

        public static double D(dynamic value, string key)
        {
            Func<string, double> func = s => double.Parse(s);
            return T<double>(value, key, func);
        }

        public static int I(dynamic value, string key)
        {
            Func<string, int> func = s => int.Parse(s);
            return T<int>(value, key, func);
        }

        public static long L(dynamic value, string key)
        {
            Func<string, long> func = s => long.Parse(s);
            return T<long>(value, key, func);
        }

        public static string S(dynamic value, string key)
        {
            Func<string, string> func = s => s;
            return T<string>(value, key, func);
        }
    }
}