using System;
using System.Collections.Generic;
using System.Text;
using TBird.Core;

namespace TBird.Roslyn
{
    public partial class RoslynObject<T>
    {
        public RoslynObject(T target)
        {
            _target = target;
            _timer = new IntervalTimer(() => Ticks.ExecuteAsync(_target));
            _timer.Interval = TimeSpan.FromMilliseconds(RoslynSetting.Instance.Interval);
            _timer.Start();
        }
        private T _target;

        /// <summary>ﾀｲﾏｰ</summary>
        private IntervalTimer _timer;

        public TaskManager<T> Ticks
        {
            get => _Ticks = _Ticks ?? new TaskManager<T>();
        }
        private TaskManager<T> _Ticks;

        /// <summary>
        /// ｽｸﾘﾌﾟﾄを起動して1回だけ実行する処理を追加します。
        /// </summary>
        /// <param name="action">処理内容</param>
        public void Run(Action<T> action)
        {
            action(_target);
        }

        /// <summary>
        /// ﾃﾞﾊﾞｯｸﾞﾒｯｾｰｼﾞを出力します。
        /// </summary>
        /// <param name="message">ﾒｯｾｰｼﾞ</param>
        public void Debug(string message)
        {
            ServiceFactory.MessageService.Debug(message);
        }

        /// <summary>
        /// 情報ﾒｯｾｰｼﾞを出力します。
        /// </summary>
        /// <param name="message">ﾒｯｾｰｼﾞ</param>
        public void Info(string message)
        {
            ServiceFactory.MessageService.Info(message);
        }

        /// <summary>
        /// ｴﾗｰﾒｯｾｰｼﾞを出力します。
        /// </summary>
        /// <param name="message">ﾒｯｾｰｼﾞ</param>
        public void Error(string message)
        {
            ServiceFactory.MessageService.Error(message);
        }

        /// <summary>
        /// 例外ﾒｯｾｰｼﾞを出力します。
        /// </summary>
        /// <param name="ex">例外</param>
        public void Exception(Exception ex)
        {
            ServiceFactory.MessageService.Exception(ex);
        }
    }
}
