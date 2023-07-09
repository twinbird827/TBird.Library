using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TBird.Core
{
    public static class DynamicUtil
    {
        public static int I(dynamic value)
        {
            return value is int i
                ? i
                : int.Parse(S(value));
        }

        public static long L(dynamic value)
        {
            return value is long i
                ? i
                : long.Parse(S(value));
        }

        public static string S(dynamic value)
        {
            return value is string s
                ? s
                : $"{value}";
        }
    }
}
