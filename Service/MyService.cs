using System;
using System.ServiceProcess;
using System.Threading;
using IdleCs.Utils;


namespace IdleCs.CombatServer
{
    class MyService : ServiceBase
    {
        private Thread _serviceThread = null;
        public MyService()
        {
            ServiceName = Program.ServiceName;
        }

        protected override void OnStart(string[] args)
        {
            base.OnStart(args);
            _serviceThread = new Thread(() =>
            {
                Program.Start(args);
                CorgiLog.Log(CorgiLogType.Info, "Service thread terminate completed.");
            });
            
            _serviceThread.Start();
        }

        protected override void OnStop()
        {
            base.OnStop();
            Program.Stop();
        }

        protected override void OnPause()
        {
            base.OnPause();
        }
    }   
}