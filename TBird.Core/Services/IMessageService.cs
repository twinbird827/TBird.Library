using System;
using System.Runtime.CompilerServices;

namespace TBird.Core
{
    public interface IMessageService
    {
        /// <summary>
        /// ｴﾗｰをﾒｯｾｰｼﾞ処理します。
        /// </summary>
        /// <param name="message">ﾒｯｾｰｼﾞ</param>
        void Error(string message,
                [CallerMemberName] string callerMemberName = "",
                [CallerFilePath] string callerFilePath = "",
                [CallerLineNumber] int callerLineNumber = 0);

        /// <summary>
        /// 情報をﾒｯｾｰｼﾞ処理します。
        /// </summary>
        /// <param name="message">ﾒｯｾｰｼﾞ</param>
        void Info(string message,
                [CallerMemberName] string callerMemberName = "",
                [CallerFilePath] string callerFilePath = "",
                [CallerLineNumber] int callerLineNumber = 0);

        /// <summary>
        /// 確認をﾒｯｾｰｼﾞ処理します。
        /// </summary>
        /// <param name="message">ﾒｯｾｰｼﾞ</param>
        bool Confirm(string message,
                [CallerMemberName] string callerMemberName = "",
                [CallerFilePath] string callerFilePath = "",
                [CallerLineNumber] int callerLineNumber = 0);

        /// <summary>
        /// ﾃﾞﾊﾞｯｸﾞﾒｯｾｰｼﾞ処理します。
        /// </summary>
        /// <param name="message">ﾒｯｾｰｼﾞ</param>
        void Debug(string message,
                [CallerMemberName] string callerMemberName = "",
                [CallerFilePath] string callerFilePath = "",
                [CallerLineNumber] int callerLineNumber = 0);

        /// <summary>
        /// 例外をﾒｯｾｰｼﾞ処理します。
        /// </summary>
        /// <param name="message">ﾒｯｾｰｼﾞ</param>
        void Exception(Exception exception,
                [CallerMemberName] string callerMemberName = "",
                [CallerFilePath] string callerFilePath = "",
                [CallerLineNumber] int callerLineNumber = 0);
    }
}