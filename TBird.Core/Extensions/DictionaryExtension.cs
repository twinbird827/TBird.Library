using System.Collections.Generic;

namespace TBird.Core
{
    public static class DictionaryExtension
    {
        public static TValue Get<TKey, TValue>(this Dictionary<TKey, TValue> dic, TKey key)
        {
            return dic.Get(key, default(TValue));
        }

        public static TValue Get<TKey, TValue>(this Dictionary<TKey, TValue> dic, TKey key, TValue def)
        {
            return dic.ContainsKey(key) ? dic[key] : def;
        }
    }
}