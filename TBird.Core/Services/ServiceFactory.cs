using System;
using System.Collections.Generic;
using System.Text;

namespace TBird.Core
{
    public static class ServiceFactory
    {
        /// <summary>
        /// ﾒｯｾｰｼﾞ表示用ｻｰﾋﾞｽ
        /// </summary>
        public static IMessageService MessageService { get; set; } = new ConsoleMessageService();
    }
}
