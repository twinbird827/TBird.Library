using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace TBird.Core
{
    public static class Directories
    {
        /// <summary>ｱﾌﾟﾘｹｰｼｮﾝ実行ﾃﾞｨﾚｸﾄﾘ</summary>
        public static string Root => AppDomain.CurrentDomain.BaseDirectory;
    }
}
