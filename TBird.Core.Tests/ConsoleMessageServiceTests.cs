using System.Diagnostics;
using NUnit.Framework;

namespace TBird.Core.Tests
{
	public class ConsoleMessageServiceTests
	{
		// #238: ConsoleMessageService を作るたびに Console.Out 向けﾄﾚｰｽﾘｽﾅｰが足されﾛｸﾞが重複するため、2 つ目を作っても件数が増えないことを固定する。

		[Test]
		public void SecondInstanceDoesNotAddTraceListener()
		{
			new ConsoleMessageService();
			var count = Trace.Listeners.Count;

			new ConsoleMessageService();

			Assert.That(Trace.Listeners.Count, Is.EqualTo(count));
		}
	}
}
