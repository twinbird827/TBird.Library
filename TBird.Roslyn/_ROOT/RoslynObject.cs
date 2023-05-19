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

        /// <summary>
        /// 時間間隔処理ﾘｽﾄ
        /// </summary>
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
    }
}
