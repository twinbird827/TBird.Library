using System;
using System.Collections.Generic;
using System.Text;

namespace TBird.Plugin
{
    public interface IPlugin : IDisposable
    {
        /// <summary>
        /// ﾌﾟﾗｸﾞｲﾝの処理間隔
        /// </summary>
        int Interval { get; }

        /// <summary>
        /// ﾌﾟﾗｸﾞｲﾝの初期化処理
        /// </summary>
        void Initialize();

        /// <summary>
        /// 一定期間で実行するﾌﾟﾗｸﾞｲﾝの処理
        /// </summary>
        void Run();
    }
}
