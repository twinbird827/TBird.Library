using System;

namespace TBird.Core
{
	public static class SingleExtension
	{
		public static float Pow(this float val, float pow)
		{
			return Math.Pow(val, pow).GetSingle();
		}
	}
}