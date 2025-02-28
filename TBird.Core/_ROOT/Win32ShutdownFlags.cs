namespace TBird.Core
{
    internal enum Win32ShutdownFlags
    {
        // ﾛｸﾞｵﾌ(ｻｲﾝｱｳﾄ)
        Logoff = 0,

        // ｼｬｯﾄﾀﾞｳﾝ
        Shutdown = 1,

        // 再起動
        Reboot = 2,

        // 電源ｵﾌ
        PowerOff = 8,

        // 強制的に実行
        Forced = 4,
    }
}