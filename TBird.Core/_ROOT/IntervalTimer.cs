using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TBird.Core
{
    public partial class IntervalTimer : ILocker
    {
        public IntervalTimer(Action action) : this(() => CoreUtil.WaitAsync(action))
        {

        }

        public IntervalTimer(Func<Task> func)
        {
            Lock = this.CreateLock4Instance();

            _func = func;
            _timer = new Timer(Tick);
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>ｲﾝﾀｰﾊﾞﾙ処理</summary>
        private Func<Task> _func;

        /// <summary>内部ﾀｲﾏｰ</summary>
        private Timer _timer;

        /// <summary>処理開始時間</summary>
        private DateTime _starttime;

        private bool _isprocessing;

        /// <summary>破棄時のｷｬﾝｾﾙﾄｰｸﾝ</summary>
        private CancellationTokenSource _cts = new CancellationTokenSource();

        /// <summary>ﾛｯｸｷｰ</summary>
        public string Lock { get; private set; }

        /// <summary>処理間隔を設定、または取得します。</summary>
        public TimeSpan Interval { get; set; }

        /// <summary>
        /// 一定間隔の処理を実行します。
        /// </summary>
        private async void Tick(object sender)
        {
            // 処理の重複禁止
            using (await Locker.LockAsync(Lock))
            {
                // ﾀｲﾏｰ停止
                _timer.Change(Timeout.Infinite, Timeout.Infinite);

                try
                {
                    // 非同期処理を実行
                    await _func().Cts(_cts);
                }
                catch (TimeoutException)
                {
                    // ｷｬﾝｾﾙ時はｽｷｯﾌﾟ
                }
                catch (Exception ex)
                {
                    MessageService.Exception(ex);
                }
                finally
                {
                    if (_isprocessing && !disposedValue && !_cts.IsCancellationRequested)
                    {
                        var now = DateTime.Now;
                        // 処理時間を算出
                        var milliseconds = (now - _starttime).TotalMilliseconds;
                        // 処理開始時間を進めるｶｳﾝﾄを算出
                        var nextcount = Math.Ceiling(milliseconds / Interval.TotalMilliseconds);
                        // 処理開始時間を再計算
                        _starttime += TimeSpan.FromMilliseconds(Interval.TotalMilliseconds * nextcount);
                        // 次回開始時刻
                        var nexttime = (_starttime - now).TotalMilliseconds; nexttime = 0 < nexttime ? nexttime : 1d;
                        // 次回開始時刻設定(ｾﾞﾛ不可)
                        _timer.Change((int)nexttime, Timeout.Infinite);
                    }
                }
            }
        }

        /// <summary>
        /// 一定間隔の処理を開始します。
        /// </summary>
        public void Start()
        {
            if (disposedValue) throw new ObjectDisposedException("This timer has been destroyed.");
            // 処理開始ﾌﾗｸﾞ
            _isprocessing = true;
            // 処理開始時間設定
            _starttime = DateTime.Now;
            // ﾀｲﾏｰを1回だけ即時実行
            _timer.Change(0, Timeout.Infinite);
        }

        /// <summary>
        /// 一定間隔の処理を停止します。
        /// </summary>
        public void Stop()
        {
            if (disposedValue) throw new ObjectDisposedException("This timer has been destroyed.");
            // 処理開始ﾌﾗｸﾞ
            _isprocessing = false;
            // ﾀｲﾏｰ停止
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }
}