using System.Collections.Generic;
using System.Security.Cryptography;

namespace TBird.Core
{
	public static class DictionaryExtension
	{
		public static TValue? Get<TKey, TValue>(this Dictionary<TKey, TValue> dic, TKey key)
		{
			return dic.ContainsKey(key) ? dic[key] : default;
		}

		public static TValue Get<TKey, TValue>(this Dictionary<TKey, TValue> dic, TKey key, TValue def)
		{
			return dic.ContainsKey(key) ? dic[key] : def;
		}
	}
}