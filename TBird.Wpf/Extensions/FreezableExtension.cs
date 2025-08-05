using System.Windows;

namespace TBird.Wpf
{
	public static class FreezableExtension
	{
		/// <summary>
		/// FreezableをFrozenした値を取得します。
		/// </summary>
		/// <typeparam name="T">返却する際のFreezable</typeparam>
		/// <param name="target">Freezableｲﾝｽﾀﾝｽ</param>
		/// <returns></returns>
		public static T Frozen<T>(this T target) where T : Freezable
		{
			if (!target.IsFrozen)
			{
				target.Freeze();
			}
			return target;
		}
	}
}