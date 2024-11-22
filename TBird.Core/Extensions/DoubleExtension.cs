using System;
using System.Collections.Generic;
using System.Linq;

namespace TBird.Core
{
	public static class DoubleExtension
	{
		public static double Average(this IEnumerable<double> arr, double def)
		{
			return arr.Any() ? arr.Average() : def;
		}

		/// <summary>
		/// 指定した数の累乗を積算します。
		/// </summary>
		/// <param name="value">浮動小数点数</param>
		/// <param name="x"><see cref="Math.Pow(double, double)"/>に渡す第一引数</param>
		/// <param name="y"><see cref="Math.Pow(double, double)"/>に渡す第二引数</param>
		/// <returns></returns>
		public static double Pow(this double value, double x, double y)
		{
			return value.Multiply(Math.Pow(x, y));
		}

		/// <summary>
		/// 指定した数を加算します。
		/// </summary>
		/// <param name="value">浮動小数点数</param>
		/// <param name="add">加算する値</param>
		/// <returns></returns>
		public static double Add(this double value, double add)
		{
			var v = new decimal(value);
			var a = new decimal(add);
			return decimal.Add(v, a).ToDouble();
		}

		/// <summary>
		/// 指定した数を減算します。
		/// </summary>
		/// <param name="value">浮動小数点数</param>
		/// <param name="subtract">減算する値</param>
		/// <returns></returns>
		public static double Subtract(this double value, double subtract)
		{
			var v = new decimal(value);
			var s = new decimal(subtract);
			return decimal.Subtract(v, s).ToDouble();
		}

		/// <summary>
		/// 指定した数を積算します。
		/// </summary>
		/// <param name="value">浮動小数点数</param>
		/// <param name="multiply">積算する値</param>
		/// <returns></returns>
		public static double Multiply(this double value, double multiply)
		{
			var v = new decimal(value);
			var m = new decimal(multiply);
			return decimal.Multiply(v, m).ToDouble();
		}

		/// <summary>
		/// 指定した数を除算します。
		/// </summary>
		/// <param name="value">浮動小数点数</param>
		/// <param name="divide">除算する値</param>
		/// <returns></returns>
		public static double Divide(this double value, double divide)
		{
			var v = new decimal(value);
			var d = new decimal(divide);
			return decimal.Divide(v, d).ToDouble();
		}

		/// <summary>
		/// 指定した数を除算した余りを返却します。
		/// </summary>
		/// <param name="value">浮動小数点数</param>
		/// <param name="modulus">除算する値</param>
		/// <returns></returns>
		public static double Modulus(this double value, double modulus)
		{
			var v = new decimal(value);
			var m = new decimal(modulus);
			return (v % m).ToDouble();
		}

		private static double ToDouble(this decimal value) => decimal.ToDouble(value);

	}
}