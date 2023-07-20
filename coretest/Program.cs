using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;
using System.Threading;
using System.IO;
using System.Text.RegularExpressions;

namespace coretest
{
    internal class Program
    {

        private static void Main(string[] args)
        {
            var htmlString = File.ReadAllText(@"C:\Work\Temp\tube-home-rss.html");
            var jsonString = Regex.Match(htmlString, @"(?<=var ytInitialData =)[^;]+");
            Console.ReadLine();
        }
    }
}