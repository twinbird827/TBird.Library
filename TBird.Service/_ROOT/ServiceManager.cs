using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;

namespace TBird.Service
{
    public abstract class ServiceManager : ServiceBase, ILocker
    {
        public ServiceManager(Func<Task> tick, Func<bool> start, Action stop) : this (tick, () => start == null ? null : CoreUtil.WaitAsync(start), stop)
        {

        }

        public ServiceManager(Func<Task> tick, Func<Task<bool>> start, Action stop)
        {
            Lock = this.CreateLock4Instance();

            //自動ﾛｸﾞ採取を有効
            AutoLog = true;

            // ｻｰﾋﾞｽの終了、停止可能
            CanShutdown = true;
            CanStop = true;

            // このｻｰﾋﾞｽの名前
            ServiceName = ServiceSetting.Instance.ServiceName;

            ServiceFactory.MessageService = new ServiceMessageService(EventLog);
            ServiceFactory.MessageService.Info("コンストラクタが呼び出されました。");

            _startfunc = start;
            _tickfunc = tick;
            _stop = stop;
            _timer = new IntervalTimer(async () =>
            {
                using (await this.LockAsync())
                {
                    // 開始処理を行っていない場合は開始処理を行い、正常終了したら後続処理を行う。
                    if (_startasync = _startasync || _startfunc == null || await _startfunc())
                    {
                        await _tickfunc();
                    }
                }
            });
            _timer.Interval = TimeSpan.FromMilliseconds(ServiceSetting.Instance.Interval);
        }

        private IntervalTimer _timer;

        private Func<Task<bool>> _startfunc;

        private Func<Task> _tickfunc;

        private Action _stop;

        private bool _startasync = false;

        public string Lock { get; private set; }

        /// <summary>
        /// ｻｰﾋﾞｽを開始します。
        /// </summary>
        /// <param name="args"></param>
        protected override void OnStart(string[] args)
        {
            ServiceFactory.MessageService.Info("テストサービスを開始します。");
            _startasync = false;
            _timer.Start();
        }

        /// <summary>
        /// ｻｰﾋﾞｽを停止します。
        /// </summary>
        protected override void OnStop()
        {
            _timer.Stop();

            //お作法らしい
            RequestAdditionalTime(2000);

            ServiceFactory.MessageService.Info("テストサービスを停止します。");

            if (_stop != null) _stop();

            // 正常終了を通知
            ExitCode = 0;
        }

        /// <summary>
        /// ｼｽﾃﾑｼｬｯﾄﾀﾞｳﾝ時の処理を実行します。
        /// </summary>
        protected override void OnShutdown()
        {
            ServiceFactory.MessageService.Info("テストサービスがシステムの終了を検知しました。");
            _timer.Dispose();
        }
    }
}
