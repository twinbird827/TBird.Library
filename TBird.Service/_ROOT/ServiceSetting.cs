using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;

namespace TBird.Service
{
    public class ServiceSetting : JsonBase<ServiceSetting>
    {
        private const string _path = @"lib\service-setting.json";

        public static ServiceSetting Instance
        {
            get => _Instance = _Instance ?? new ServiceSetting();
        }
        private static ServiceSetting _Instance;

        public ServiceSetting() : base(_path)
        {
            if (!Load())
            {
                Interval = 1000;

                ServiceName = "TBird.Service";
                DisplayName = "TBird.Service display name";
                Description = "TBird.Service default description";
                StartType = ServiceStartMode.Automatic;
                Username = Environment.UserName;
                Account = ServiceAccount.LocalSystem;

                WriteInformationEventLog = true;
            }
        }

        /// <summary>
        /// 処理間隔(ms)
        /// </summary>
        public int Interval
        {
            get => GetProperty(_Interval);
            set => SetProperty(ref _Interval, value);
        }
        private int _Interval;

        /// <summary>
        /// ｻｰﾋﾞｽ名
        /// </summary>
        public string ServiceName
        {
            get => GetProperty(_ServiceName);
            set => SetProperty(ref _ServiceName, value);
        }
        private string _ServiceName;

        /// <summary>
        /// ｻｰﾋﾞｽ表示名
        /// </summary>
        public string DisplayName
        {
            get => GetProperty(_DisplayName);
            set => SetProperty(ref _DisplayName, value);
        }
        private string _DisplayName;

        /// <summary>
        /// ｻｰﾋﾞｽの説明
        /// </summary>
        public string Description
        {
            get => GetProperty(_Description);
            set => SetProperty(ref _Description, value);
        }
        private string _Description;

        /// <summary>
        /// ｻｰﾋﾞｽの開始ﾓｰﾄﾞ
        /// </summary>
        public ServiceStartMode StartType
        {
            get => GetProperty(_StartType);
            set => SetProperty(ref _StartType, value);
        }
        private ServiceStartMode _StartType;

        /// <summary>
        /// ｻｰﾋﾞｽの実行ﾕｰｻﾞ
        /// </summary>
        public string Username
        {
            get => GetProperty(_Username);
            set => SetProperty(ref _Username, value);
        }
        private string _Username;

        /// <summary>
        /// ｻｰﾋﾞｽの実行権限
        /// </summary>
        public ServiceAccount Account
        {
            get => GetProperty(_Account);
            set => SetProperty(ref _Account, value);
        }
        private ServiceAccount _Account;

        /// <summary>
        /// Informationﾛｸﾞをｲﾍﾞﾝﾄﾛｸﾞに出力するかどうか
        /// </summary>
        public bool WriteInformationEventLog
        {
            get => GetProperty(_WriteInformationEventLog);
            set => SetProperty(ref _WriteInformationEventLog, value);
        }
        private bool _WriteInformationEventLog;

    }
}
