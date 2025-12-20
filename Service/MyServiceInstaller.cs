using System;
using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;
using IdleCs.Logger;
using IdleCs.Utils;

namespace IdleCs.CombatServer
{
    [RunInstaller(true)]
    public class MyServiceInstaller : Installer
    {
        public MyServiceInstaller()
        {
            var spi = new ServiceProcessInstaller();
            var si = new ServiceInstaller();

            //spi.Account = ServiceAccount.LocalSystem;
            spi.Account = ServiceAccount.User;
            spi.Username = null;
            spi.Password = null;

            si.DisplayName = Program.ServiceName;
            si.ServiceName = Program.ServiceName;
            si.StartType = ServiceStartMode.Manual;
            si.Description = "Provided by Com2usHoldings";

            Installers.Add(spi);
            Installers.Add(si);

            //CorgiLog.Log(CorgiLogType.Fatal, "Called???");
        }
    }
}