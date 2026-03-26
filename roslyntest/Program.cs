using System;
using TBird.Core;
using TBird.Roslyn;

namespace roslyntest
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			Test();
		}

		private static void Test()
		{
			using (MessageService.Measure())
			using (var manager = RoslynManager.Instance)
			{
				manager.Initialize(new Target());
				manager.Add("Roslyn.csx", new Target());

				using (MessageService.Measure("manager.Run"))
				{
					manager.RunBackground();
				}
				Console.ReadLine();
			}

			Console.ReadLine();
		}
	}
}