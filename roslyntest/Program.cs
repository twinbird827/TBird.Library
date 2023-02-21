using System;
using System.Threading.Tasks;
using TBird.Roslyn;

namespace roslyntest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            Test();
        }

        static async void Test()
        {
            using (var manager = RoslynManager.Instance)
            {
                manager.Initialize(new Target());
                manager.Add("Roslyn.ctx", new Target());
                await manager.RunAsync();
                Console.ReadLine();
            }
            //using (var exec = new RoslynExecuter<Target>("Roslyn.ctx", new Target()))
            //{
            //    await exec.RunAsync();
            //    Console.ReadLine();
            //}
            Console.ReadLine();
        }
    }
}
