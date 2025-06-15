using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TBird.Core
{
	public static class ObjectExtension
	{
		/// <summary>
		/// <see cref="KeyValuePair{TKey, TValue}"/>を作成します。
		/// </summary>
		/// <typeparam name="TKey">ｷｰの型</typeparam>
		/// <typeparam name="TValue">値の型</typeparam>
		/// <param name="key">ｷｰ</param>
		/// <param name="value">値</param>
		/// <returns></returns>
		public static KeyValuePair<TKey, TValue> Kvp<TKey, TValue>(this TKey key, TValue value) => new KeyValuePair<TKey, TValue>(key, value);

		/// <summary>
		/// <see cref="object"/>型のｲﾝｽﾀﾝｽを<see cref="string"/>型に変換します。
		/// </summary>
		/// <param name="value">元となる値</param>
		/// <returns></returns>
		public static string Str(this object value) => value is string s ? s : $"{value}";

		/// <summary>
		/// <see cref="object"/>型のｲﾝｽﾀﾝｽを<see cref="bool"/>型に変換します。
		/// </summary>
		/// <param name="value">元となる値</param>
		/// <param name="def">変換できない場合のﾃﾞﾌｫﾙﾄ値</param>
		/// <returns></returns>
		public static bool Bool(this object value, bool def = false) => bool.TryParse(value.Str(), out bool o) ? o : def;

		/// <summary>
		/// <see cref="object"/>型のｲﾝｽﾀﾝｽを<see cref="double"/>型に変換します。
		/// </summary>
		/// <param name="value">元となる値</param>
		/// <param name="def">変換できない場合のﾃﾞﾌｫﾙﾄ値</param>
		/// <returns></returns>
		public static double Double(this object? value, double def = 0D) => GetDouble(value, def);

		/// <summary>
		/// <see cref="object"/>型のｲﾝｽﾀﾝｽを<see cref="float"/>型に変換します。
		/// </summary>
		/// <param name="value">元となる値</param>
		/// <param name="def">変換できない場合のﾃﾞﾌｫﾙﾄ値</param>
		/// <returns></returns>
		public static float Single(this object? value, float def = 0F) => GetSingle(value, def);

		/// <summary>
		/// <see cref="object"/>型のｲﾝｽﾀﾝｽを<see cref="int"/>型に変換します。
		/// </summary>
		/// <param name="value">元となる値</param>
		/// <param name="def">変換できない場合のﾃﾞﾌｫﾙﾄ値</param>
		/// <returns></returns>
		public static int Int32(this object? value, int def = 0) => GetInt32(value, def);

		/// <summary>
		/// <see cref="object"/>型のｲﾝｽﾀﾝｽを<see cref="long"/>型に変換します。
		/// </summary>
		/// <param name="value">元となる値</param>
		/// <param name="def">変換できない場合のﾃﾞﾌｫﾙﾄ値</param>
		/// <returns></returns>
		public static long Int64(this object? value, long def = 0L) => GetInt64(value, def);

		/// <summary>
		/// 指定したｵﾌﾞｼﾞｪｸﾄがIDisposableを実装しているなら破棄します。
		/// </summary>
		/// <param name="value">ｵﾌﾞｼﾞｪｸﾄ</param>
		public static void TryDispose(this object value)
		{
			if (value is IDisposable disposable)
			{
				disposable.Dispose();
			}
		}

		/// <summary>
		///
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="value"></param>
		/// <param name="def"></param>
		/// <param name="func1"></param>
		/// <returns></returns>
		private static T Get<T>(this object? value, T def, Func<decimal, T> func1)
		{
			if (value is T val)
			{
				return val;
			}

			if (value is double x1)
			{
				return func1((decimal)x1);
			}
			else if (value is float x2)
			{
				return func1((decimal)x2);
			}
			else if (value is int x3)
			{
				return func1((decimal)x3);
			}
			else if (value is long x4)
			{
				return func1((decimal)x4);
			}
			else if (value is short x5)
			{
				return func1((decimal)x5);
			}
			else if (value is uint x6)
			{
				return func1((decimal)x6);
			}
			else if (value is string && decimal.TryParse((string)value, out decimal x7))
			{
				return func1(x7);
			}
			else if (value != null && decimal.TryParse(value.ToString(), out decimal x8))
			{
				return func1(x8);
			}
			else
			{
				return def;
			}
		}

		public static double GetDouble(this object? value, double def = 0D)
		{
			return value.Get(def, x => (double)x);
		}

		public static float GetSingle(this object? value, float def = 0F)
		{
			return value.Get(def, x => (float)x);
		}

		public static int GetInt32(this object? value, int def = 0)
		{
			return value.Get(def, x => (int)x);
		}

		public static long GetInt64(this object? value, long def = 0L)
		{
			return value.Get(def, x => (long)x);
		}

		public static T Run<T>(this T target, Action<T> action)
		{
			action(target);
			return target;
		}

		public static TResult Run<T, TResult>(this T target, Func<T, TResult> action)
		{
			return action(target);
		}

		public static async Task<T> RunAsync<T>(this Task<T> target, Action<T> action)
		{
			var x = await target;
			action(x);
			return x;
		}

		public static async Task<T> RunAsync<T>(this Task<T> target, Func<T, Task> action)
		{
			var x = await target;
			await action(x);
			return x;
		}

		public static async Task<TResult> RunAsync<T, TResult>(this Task<T> target, Func<T, TResult> action)
		{
			return action(await target);
		}

		public static async Task<TResult> RunAsync<T, TResult>(this Task<T> target, Func<T, Task<TResult>> action)
		{
			return await action(await target);
		}

		public static T NotNull<T>(this T? value, string message = "value can not null.") => value ?? throw new ArgumentNullException(message);

		public static Disposer<T> Disposer<T>(this T value, Action<T> action)
		{
			return new Disposer<T>(value, action);
		}
	}
}