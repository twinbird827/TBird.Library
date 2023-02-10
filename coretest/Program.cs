using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;

namespace coretest
{
    class Program
    {
        private class TEST
        {

        }

        static void Main(string[] args)
        {
            var x = new Dictionary<string, string>();

            Console.WriteLine(x["test"]);
            //var path = @"C:\Work\common-language.csv";

            //foreach (var lines in FileUtil.CsvLoad(path))
            //{
            //    foreach (var line in lines)
            //    {
            //        Console.WriteLine(line);
            //        Console.WriteLine("***");
            //    }
            //    Console.WriteLine("---");
            //}
            Console.ReadLine();
        }
    }
}
