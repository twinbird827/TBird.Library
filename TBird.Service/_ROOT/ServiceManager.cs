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
        /// <summary>
        /// ｻｰﾋﾞｽの実体を初期化します。
        /// </summary>
        public ServiceManager()
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
            ServiceFactory.MessageService.Info("サービスのコンストラクタが呼び出されました。");

            _timer = new IntervalTimer(async () =>
            {
                using (await this.LockAsync())
                {
                    // 開始処理を行っていない場合は開始処理を行い、正常終了したら後続処理を行う。
                    if (_startasync = _startasync || await StartProcess())
                    {
                        try
                        {
                            await TickProcess();
                        }
                        catch (Exception ex)
                        {
                            ServiceFactory.MessageService.Info("処理されていない例外をキャッチしたため、停止処理を実行した後、開始処理を実行します。");
                            ServiceFactory.MessageService.Exception(ex);
                            StopProcess();
                            _startasync = false;
                        }
                    }
                }
            });
            _timer.Interval = TimeSpan.FromMilliseconds(ServiceSetting.Instance.Interval);
        }

        /// <summary>時間間隔のﾀｲﾏｰ</summary>
        private IntervalTimer _timer;

        /// <summary>開始処理が成功したかどうか</summary>
        private bool _startasync = false;

        /// <summary>ﾛｯｸｷｰ(ｲﾝｽﾀﾝｽ毎に固有)</summary>
        public string Lock { get; private set; }

        /// <summary>開始処理</summary>
        protected virtual Task<bool> StartProcess()
        {
            return Task.Run(() => true);
        }

        protected Task<bool> ToStartResult(bool value)
        {
            return Task.Run(() => value);
        }
        
        /// <summary>時間間隔の処理</summary>
        protected abstract Task TickProcess();

        /// <summary>停止処理</summary>
        protected virtual void StopProcess()
        {

        }

        /// <summary>
        /// ｻｰﾋﾞｽを開始します。
        /// </summary>
        /// <param name="args"></param>
        protected override void OnStart(string[] args)
        {
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

            StopProcess();

            // 正常終了を通知
            ExitCode = 0;
        }

        /// <summary>
        /// ｼｽﾃﾑｼｬｯﾄﾀﾞｳﾝ時の処理を実行します。
        /// </summary>
        protected override void OnShutdown()
        {
            ServiceFactory.MessageService.Info("サービスがシステムの終了を検知しました。");
            _timer.Dispose();
        }
    }
}
