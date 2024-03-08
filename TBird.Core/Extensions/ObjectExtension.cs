using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TBird.Core
{
	public static class ObjectExtension
	{
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
		/// <param name="func"></param>
		/// <returns></returns>
		private static T Get<T>(this object value, T def, Func<object, T> func)
		{
			if (value is T val)
			{
				return val;
			}

			if (value is double x1)
			{
				return func(x1);
			}
			else if (value is float x2)
			{
				return func(x2);
			}
			else if (value is int x3)
			{
				return func(x3);
			}
			else if (value is long x4)
			{
				return func(x4);
			}
			else if (value is short x5)
			{
				return func(x5);
			}
			else if (value is uint x6)
			{
				return func(x6);
			}
			else if (value is string && decimal.TryParse((string)value, out decimal x7))
			{
				return func(x7);
			}
			else if (value != null && decimal.TryParse(value.ToString(), out decimal x8))
			{
				return func(x8);
			}
			else
			{
				return def;
			}
		}

		public static double GetDouble(this object value, double def = 0D)
		{
			return value.Get(def, x => (double)x);
		}

		public static float GetSingle(this object value, float def = 0F)
		{
			return value.Get(def, x => (float)x);
		}

		public static int GetInt32(this object value, int def = 0)
		{
			return value.Get(def, x => (int)x);
		}

		public static long GetInt64(this object value, long def = 0L)
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

		public static async Task<TResult> RunAsync<T, TResult>(this Task<T> target, Func<T, TResult> action)
		{
			return action(await target);
		}
	}
}