using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TBird.Core
{
	public static class IEnumerableExtension
	{
		///// <summary>
		///// 指定した配列を<code>chunkSize</code>区切りのIEnumerable&lt;IEnumerable&lt;T&gt;&gt;として取得します。
		///// </summary>
		///// <typeparam name="T">配列内のｲﾝｽﾀﾝｽ型</typeparam>
		///// <param name="source">元配列</param>
		///// <param name="chunkSize">元配列を分割する区切り数</param>
		///// <returns></returns>
		//public static IEnumerable<IEnumerable<T>> Chunk<T>(this IEnumerable<T> source, int chunkSize)
		//{
		//    if (chunkSize <= 0)
		//        throw new ArgumentException("Chunk size must be greater than 0.", nameof(chunkSize));

		//    while (source.Any())
		//    {
		//        yield return source.Take(chunkSize);
		//        source = source.Skip(chunkSize);
		//    }
		//}

		///// <summary>
		///// 最大値、またはﾃﾞﾌｫﾙﾄ値を取得します。
		///// </summary>
		///// <typeparam name="T">配列の型</typeparam>
		///// <typeparam name="TResult">返却する値の型</typeparam>
		///// <param name="source">配列</param>
		///// <param name="func">返却する値を取得するﾛｼﾞｯｸ</param>
		///// <returns></returns>
		//public static TResult MaxOrDefault<T, TResult>(this IEnumerable<T> source, Func<T, TResult> func)
		//{
		//	return source.MaxOrDefault(func, default(TResult));
		//}

		/// <summary>
		/// 最大値、またはﾃﾞﾌｫﾙﾄ値を取得します。
		/// </summary>
		/// <typeparam name="T">配列の型</typeparam>
		/// <typeparam name="TResult">返却する値の型</typeparam>
		/// <param name="source">配列</param>
		/// <param name="func">返却する値を取得するﾛｼﾞｯｸ</param>
		/// <param name="def">ﾃﾞﾌｫﾙﾄ値</param>
		/// <returns></returns>
		public static TResult MaxOrDefault<T, TResult>(this IEnumerable<T> source, Func<T, TResult> func, TResult def)
		{
			return source.Any() ? source.Max(func) : def;
		}

		///// <summary>
		///// 最大値、またはﾃﾞﾌｫﾙﾄ値を取得します。
		///// </summary>
		///// <typeparam name="T">配列の型</typeparam>
		///// <typeparam name="TResult">返却する値の型</typeparam>
		///// <param name="source">配列</param>
		///// <param name="func">返却する値を取得するﾛｼﾞｯｸ</param>
		///// <returns></returns>
		//public static TResult MinOrDefault<T, TResult>(this IEnumerable<T> source, Func<T, TResult> func)
		//{
		//	return source.MinOrDefault(func, default(TResult));
		//}

		/// <summary>
		/// 最大値、またはﾃﾞﾌｫﾙﾄ値を取得します。
		/// </summary>
		/// <typeparam name="T">配列の型</typeparam>
		/// <typeparam name="TResult">返却する値の型</typeparam>
		/// <param name="source">配列</param>
		/// <param name="func">返却する値を取得するﾛｼﾞｯｸ</param>
		/// <param name="def">ﾃﾞﾌｫﾙﾄ値</param>
		/// <returns></returns>
		public static TResult MinOrDefault<T, TResult>(this IEnumerable<T> source, Func<T, TResult> func, TResult def)
		{
			return source.Any() ? source.Min(func) : def;
		}

		/// <summary>
		/// 指定した値の配列ｲﾝﾃﾞｯｸｽを取得します。
		/// </summary>
		/// <typeparam name="T">配列の型</typeparam>
		/// <param name="source">配列</param>
		/// <param name="item">検索値</param>
		/// <returns></returns>
		public static int IndexOf<T>(this IEnumerable<T> source, T item)
		{
			var i = 0;
			return source.GetOrDefault(
				// 一致したらｲﾝﾃﾞｯｸｽを返却 -> 不一致ならfalseになるようにi++
				x => (item != null && item.Equals(x)) || i++ < 0,
				x => i,
				-1
			);
		}

		/// <summary>
		/// 配列の指定した位置の値を取得します。
		/// </summary>
		/// <typeparam name="T">配列の型</typeparam>
		/// <param name="array">配列</param>
		/// <param name="index">位置</param>
		/// <param name="def">配列が指定した数までなかった場合のﾃﾞﾌｫﾙﾄ値</param>
		/// <returns></returns>
		public static T IndexOf<T>(this T[] array, int index, T def)
		{
			return index < array.Length ? array[index] : def;
		}

		/// <summary>
		/// 文字配列を連結文字で結合して一つの文字列として返却します。
		/// </summary>
		/// <param name="s">対象文字配列</param>
		/// <param name="separator">連結する文字</param>
		/// <returns></returns>
		public static string GetString<T>(this IEnumerable<T> s, string separator = "")
		{
			return string.Join(separator, s);
		}

		/// <summary>
		/// 配列から条件に合致する値を取得します。合致する値がない場合、ﾃﾞﾌｫﾙﾄ値を取得します。
		/// </summary>
		/// <typeparam name="T">配列の型</typeparam>
		/// <typeparam name="TResult">戻り値の型</typeparam>
		/// <param name="array">配列</param>
		/// <param name="where">条件</param>
		/// <param name="get">戻り値の取得方法</param>
		/// <param name="def">ﾃﾞﾌｫﾙﾄ値</param>
		/// <returns></returns>
		public static TResult GetOrDefault<T, TResult>(this IEnumerable<T> array, Func<T, bool> where, Func<T, TResult> get, TResult def)
		{
			var target = array.FirstOrDefault(row => where(row));

			return target != null
				? get(target)
				: def;
		}

		///// <summary>
		///// 配列から条件に合致する値を取得します。合致する値がない場合、ﾃﾞﾌｫﾙﾄ値を取得します。
		///// </summary>
		///// <typeparam name="T">配列の型</typeparam>
		///// <typeparam name="TResult">戻り値の型</typeparam>
		///// <param name="array">配列</param>
		///// <param name="where">条件</param>
		///// <param name="get">戻り値の取得方法</param>
		///// <returns></returns>
		//public static TResult GetOrDefault<T, TResult>(this IEnumerable<T> array, Func<T, bool> where, Func<T, TResult> get)
		//{
		//	return GetOrDefault(array, where, get, default(TResult));
		//}

		/// <summary>
		/// 配列の各行に対して、並列処理を実行します。
		/// </summary>
		/// <typeparam name="T">配列の型</typeparam>
		/// <param name="array">配列</param>
		/// <param name="action">各行に対して実行する処理</param>
		public static void ForParallel<T>(this IEnumerable<T> array, Action<T> action)
		{
			array.AsParallel().ForAll(action);
		}

		/// <summary>
		/// 配列の各行に対して、直列処理を実行します。
		/// </summary>
		/// <typeparam name="T">配列の型</typeparam>
		/// <param name="array">配列</param>
		/// <param name="action">各行に対して実行する処理</param>
		public static void ForEach<T>(this IEnumerable<T> array, Action<T> action)
		{
			if (array == null) return;

			foreach (var row in array)
			{
				action(row);
			}
		}

		/// <summary>
		/// 配列の各行に対して、直列処理を実行します。
		/// </summary>
		/// <typeparam name="T">配列の型</typeparam>
		/// <param name="array">配列</param>
		/// <param name="action">各行に対して実行する処理</param>
		public static void ForEach<T>(this IEnumerable<T> array, Action<T, int> action)
		{
			if (array == null) return;

			int i = 0;
			foreach (var row in array)
			{
				action(row, i++);
			}
		}

		/// <summary>
		/// 指定した非同期配列を別の型で取得し直します。
		/// </summary>
		/// <typeparam name="TSource">変更前の型</typeparam>
		/// <typeparam name="TResult">変更後の型</typeparam>
		/// <param name="array">変更前の型を持つ非同期配列</param>
		/// <param name="func">変更前の型を変更後の方へ変更する処理</param>
		/// <returns></returns>
		public static async Task<IEnumerable<TResult>> Select<TSource, TResult>(this Task<IEnumerable<TSource>> array, Func<TSource, TResult> func)
		{
			return (await array).Select(func);
		}

		/// <summary>
		/// Null許容型の配列からNullを除外します。
		/// </summary>
		/// <typeparam name="T">配列の型</typeparam>
		/// <param name="array">Nullを含む配列</param>
		/// <returns></returns>
		public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> array)
		{
			return array.OfType<T>();
		}

		/// <summary>
		/// 配列を、非同期条件を満たすﾃﾞｰﾀのみに絞り込みます。
		/// </summary>
		/// <typeparam name="T">配列の型</typeparam>
		/// <param name="array">配列</param>
		/// <param name="func">非同期条件</param>
		/// <returns></returns>
		public static async Task<IEnumerable<T>> WhereAsync<T>(this IEnumerable<T> array, Func<T, Task<bool>> func)
		{
			var _dummy = new object();
			return await array
				.Select(async x => await func(x) ? x : _dummy)
				.WhenAll()
				.RunAsync(x => x.OfType<T>());
		}
	}
}