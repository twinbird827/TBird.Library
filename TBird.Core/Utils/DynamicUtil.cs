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

        public static T T<T>(dynamic value, string key)
        {
            var keyvalue = O(value, key);
            return keyvalue is T t ? t : default(T);
        }

        public static int I(dynamic value, string key)
        {
            return T<int>(value, key);
        }

        public static long L(dynamic value, string key)
        {
            return T<long>(value, key);
        }

        public static string S(dynamic value, string key)
        {
            var keyvalue = O(value, key);
            return keyvalue == null
                ? null
                : keyvalue is string s ? s : $"{value}";
        }
    }
}