using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;
using TBird.DB.SQLite;

namespace coretest
{
    class Program
    {
        private class TEST
        {

        }

        static async void Async()
        {
            using (var x = new SQLiteControl(@"DataSource=database.sqlite3;password=sN!dty9*!9MW"))
            {
                Console.WriteLine(await x.ExecuteScalarAsync("SELECT 1"));
            }
        }
        public static bool IsIncluded<T>(T a, T b) where T : Enum
        {
            return a is object oa && oa is int ia && b is object ob && ob is int ib
                ? (ia & ib) == ib
                : false;
        }

        private static bool IsIncluded(int a, int b)
        {
            return (a & b) == b;
        }

        static void Main(string[] args)
        {
            Console.WriteLine(IsIncluded(ConsoleColor.Red | ConsoleColor.Green, ConsoleColor.Green));
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
