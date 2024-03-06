using System;
using System.CodeDom;

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

        public static double GetDoubleNaN(this object value)
        {
			if (value is double val)
			{
				return val;
			}
            decimal result;
            if (value.TryDecimal(out result))
            {
                return (double)result;
            }
            else
            {
                return double.NaN;
            }
        }

        public static double GetDouble(this object value)
        {
            double x = GetDoubleNaN(value);

            return double.IsNaN(x) ? 0d : x;
        }

        public static float GetSingleNaN(this object value)
        {
			if (value is float val)
			{
				return val;
			}
            decimal result;
            if (value.TryDecimal(out result))
            {
                return (float)result;
            }
            else
            {
                return float.NaN;
            }
        }

        public static float GetSingle(this object value)
        {
            float x = GetSingleNaN(value);

            return float.IsNaN(x) ? 0F : x;
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

        public static long GetInt64(this object value)
        {
            decimal result;
            if (value.TryDecimal(out result))
            {
                return (long)result;
            }
            else
            {
                return 0;
            }
        }

    }
}