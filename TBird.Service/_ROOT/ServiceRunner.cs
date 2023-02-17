using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace TBird.Service
{
    [RunInstaller(true)]
    public class ServiceRunner : Installer
    {
        public ServiceRunner()
        {
            var spi = new ServiceProcessInstaller();
            spi.Username = ServiceSetting.Instance.Username;
            spi.Account = ServiceSetting.Instance.Account;

            var si = new ServiceInstaller();
            si.ServiceName = ServiceSetting.Instance.ServiceName;
            si.DisplayName = ServiceSetting.Instance.DisplayName;
            si.Description = ServiceSetting.Instance.Description;

            //自動起動を指定
            si.StartType = ServiceSetting.Instance.StartType;

            this.Installers.Add(spi);
            this.Installers.Add(si);
        }

        public static void Run(ServiceBase service, params string[] args)
        {
            if (Environment.UserInteractive)
            {
                if (args.Length == 1)
                {
                    var mode = args[0].ToLower();
                    var path = System.Reflection.Assembly.GetCallingAssembly().Location;
                    switch (mode)
                    {
                        case "/i":
                            if (IsServiceExists(service.ServiceName))
                            {
                                Console.WriteLine("既にインストールされています。");
                            }
                            else
                            {
                                ManagedInstallerClass.InstallHelper(new[] { path });
                            }
                            return;

                        case "/u":
                            if (IsServiceExists(service.ServiceName))
                            {
                                ManagedInstallerClass.InstallHelper(new[] { "/u", path });
                            }
                            else
                            {
                                Console.WriteLine("インストールされていません。");
                            }
                            return;

                    }
                }

                // ｺﾝｿｰﾙでﾃｽﾄ実行
                OnStart(service, args);
                Console.WriteLine("Press any key to stop program");
                Console.Read();
                service.Stop();
                Console.Read();
            }
            else
            {
                ServiceBase.Run(new ServiceBase[] { service });
            }

        }

        private static bool IsServiceExists(string name)
        {
            return ServiceController.GetServices().Any(s => s.ServiceName == name);
        }

        private static void OnStart(ServiceBase service, string[] args)
        {
            var type = service.GetType();
            var info = type.GetMethod("OnStart", BindingFlags.InvokeMethod | BindingFlags.NonPublic | BindingFlags.Instance);
            info.Invoke(service, new object[] { args });
        }

    }
}
