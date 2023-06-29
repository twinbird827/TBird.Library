using System;

namespace TBird.Core
{
    public static class Directories
    {
        /// <summary>ｱﾌﾟﾘｹｰｼｮﾝ実行ﾃﾞｨﾚｸﾄﾘ</summary>
        public static string Root => AppDomain.CurrentDomain.BaseDirectory;
    }
}