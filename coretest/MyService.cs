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
        public MyService()
        {

        }

        protected override Task<bool> StartProcess()
        {
            ServiceFactory.MessageService.Info("開始処理");
            return ToStartResult(true);
        }

        protected override async Task TickProcess()
        {
            ServiceFactory.MessageService.Info("B:" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            await Task.Delay(new Random().Next(100, 900));
            ServiceFactory.MessageService.Info("E:" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        }

        protected override void StopProcess()
        {
            ServiceFactory.MessageService.Info("停止処理");
        }
    }
}
