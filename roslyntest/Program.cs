using System;
using System.Diagnostics;
using System.Threading.Tasks;
using TBird.Core;
using TBird.Roslyn;

namespace roslyntest
{
    class Program
    {
        static void Main(string[] args)
        {
            Test();
        }

        static void Test()
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
