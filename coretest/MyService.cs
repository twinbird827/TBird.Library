using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;
using TBird.Service;

namespace coretest
{
    public class MyService : ServiceManager
    {
        public MyService() : base(Tick, Start, null)
        {

        }

        private static bool Start()
        {
            if (_start)
            {
                return true;
            }
            else
            {
                _start = true;
                return false;
            }
        }
        private static bool _start = false;

        private static async Task Tick()
        {
            ServiceFactory.MessageService.Info("B:" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            await Task.Delay(new Random().Next(100, 900));
            ServiceFactory.MessageService.Info("E:" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        }
    }
}
