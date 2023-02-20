using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using TBird.Core;
using TBird.DB.SQLite;
using System.Threading;
using TBird.Service;

namespace coretest
{
    class Program
    {
        //private class TEST
        //{

        //}

        //static void Async()
        //{
        //    var r = new Random();
        //    var t = new IntervalTimer(() =>
        //    {
        //        var time = r.Next(1, 5000);
        //        Console.WriteLine("b:"+DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        //        Thread.Sleep(time);
        //        Console.WriteLine("e:" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "time: " + time);
        //    });
        //    t.Interval = TimeSpan.FromMilliseconds(2000);
        //    t.Start();
        //}
        //public static bool IsIncluded<T>(T a, T b) where T : Enum
        //{
        //    return a is object oa && oa is int ia && b is object ob && ob is int ib
        //        ? (ia & ib) == ib
        //        : false;
        //}

        //private static bool IsIncluded(int a, int b)
        //{
        //    return (a & b) == b;
        //}

        static void Main(string[] args)
        {
            ServiceRunner.Run(new MyService(), args);
            //Async();
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
