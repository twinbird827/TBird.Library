using System;

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
		/// ｵﾌﾞｼﾞｪｸﾄを倍精度浮動小数点数に変換し、変換できたかどうかを取得します。
		/// </summary>
		/// <param name="value">対象ｵﾌﾞｼﾞｪｸﾄ</param>
		/// <param name="result">変換できた場合は変換値を格納する参照変数</param>
		public static bool TryDecimal(this object value, out decimal result)
		{
			decimal x;
			if (value is double)
			{
				result = (decimal)(double)value;
			}
			else if (value is float)
			{
				result = (decimal)(float)value;
			}
			else if (value is int)
			{
				result = (int)value;
			}
			else if (value is long)
			{
				result = (long)value;
			}
			else if (value is short)
			{
				result = (short)value;
			}
			else if (value is uint)
			{
				result = (short)value;
			}
			else if (value is string && decimal.TryParse((string)value, out x))
			{
				result = x;
			}
			else if (value != null && decimal.TryParse(value.ToString(), out x))
			{
				result = x;
			}
			else
			{
				result = 0;
				return false;
			}

			return true;
		}

		public static double GetDouble(this object value)
		{
			decimal result;
			if (value.TryDecimal(out result))
			{
				return (double)result;
			}
			else
			{
				return 0d;
			}
		}

		public static int GetInt32(this object value)
		{
			decimal result;
			if (value.TryDecimal(out result))
			{
				return (int)result;
			}
			else
			{
				return 0;
			}
		}
	}
}