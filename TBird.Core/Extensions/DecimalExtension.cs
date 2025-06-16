namespace TBird.Core
{
	public static class DecimalExtension
	{
		public static string ToString(this int value, int digit)
		{
			return string.Format($"{{0,0:D{digit}}}", value);
		}
	}
}