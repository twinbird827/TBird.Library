using System;
using System.IO;
using TBird.Core;

namespace coretest
{
	internal class Program
	{

		private static void Main(string[] args)
		{
			var x = @"c:\aaa\bbb\ccc.eee";
			Console.WriteLine(Path.GetExtension(x));
			Console.WriteLine(FileUtil.GetFullPathWithoutExtension(x));
			Console.ReadLine();
		}
	}
}