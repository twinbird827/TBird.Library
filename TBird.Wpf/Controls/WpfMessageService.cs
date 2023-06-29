using System;
using System.Runtime.CompilerServices;
using TBird.Core;

namespace TBird.Wpf.Controls
{
    public class WpfMessageService : ConsoleMessageService
    {
        /// <summary>
        /// ｲﾝﾌｫﾒｰｼｮﾝﾒｯｾｰｼﾞを画面に表示します。
        /// </summary>
        /// <param name="message">ﾒｯｾｰｼﾞ</param>
        public override void Info(string message, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            base.Info(message, callerMemberName, callerFilePath, callerLineNumber);

            new WpfMessageViewModel(WpfMessageType.Information, message).ShowDialog(() => new WpfMessageWindow());
        }

        /// <summary>
        /// ｴﾗｰﾒｯｾｰｼﾞを画面に表示します。
        /// </summary>
        /// <param name="message">ﾒｯｾｰｼﾞ</param>
        public override void Error(string message, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            base.Error(message, callerMemberName, callerFilePath, callerLineNumber);

            new WpfMessageViewModel(WpfMessageType.Error, message).ShowDialog(() => new WpfMessageWindow());
        }

        /// <summary>
        /// 確認ﾒｯｾｰｼﾞを画面に表示します。
        /// </summary>
        /// <param name="message">ﾒｯｾｰｼﾞ</param>
        public override bool Confirm(string message, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            base.Confirm(message, callerMemberName, callerFilePath, callerLineNumber);

            return (bool)new WpfMessageViewModel(WpfMessageType.Confirm, message).ShowDialog(() => new WpfMessageWindow());
        }

        /// <summary>
        /// 例外ﾒｯｾｰｼﾞを画面に表示します。
        /// </summary>
        /// <param name="exception">例外</param>
        public override void Exception(Exception exception, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            base.Exception(exception, callerMemberName, callerFilePath, callerLineNumber);
        }
    }
}