using System;
using System.Security;
using System.ServiceProcess;
using System.Threading;
using IdleCs.GameLogic;
using IdleCs.Logger;
using IdleCs.Network.NetLib;
using IdleCs.ServerContents;
using IdleCs.ServerUtils;
using IdleCs.Utils;



namespace IdleCs.CombatServer
{
    internal class Program
    {
        public const string ServiceName = "CombatServer";
        public static bool IsProgramStop = false;
        public static bool IsServiceMode = false;
        
        public static void Main(string[] args)
        {
            if (Environment.UserInteractive)
            {
                //-console mode
                Start(args);
            }
            else
            {
                //-service mode
                using (var service = new MyService())
                {
                    IsServiceMode = true;
                    ServiceBase.Run(service);
                }
            }
        }

        public static void Start(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += MiniDumper.Current_UnhandledExceptionEventHandler;
            AppDomain.CurrentDomain.UnhandledException += Current_UnhandledExceptionEventHandler;
            
            IsProgramStop = false;
            CorgiLog.IsServer = true;
            #if DEBUG
			CorgiCombatLog.Initialize(new CombatLogCategory[] {CombatLogCategory.System, CombatLogCategory.Dungeon});
            #endif

            var server = new CombatServerApp();
            if (false == server.Initialize(args))
            {
                Console.WriteLine("[error] server.Initialize failed. press any key. exit process");
                Console.ReadKey();
                return;
            }
            
            try
            {
                TestOrder.Initalize();    
                server.StartServer();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                CorgiLog.Log(CorgiLogType.Fatal, "Server will terminated. occur exception : {0}", e.ToString());
            }
            
            CorgiLog.Log(CorgiLogType.Info, "End CombatServer");
        }

        public static void Stop()
        {
            IsProgramStop = true;
        }

        public static void Current_UnhandledExceptionEventHandler(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                var ex = (Exception) e.ExceptionObject;
                string errorMsg = "An application error occurred.";

                LogHelper.LogException(ex, errorMsg);
            }
            catch (Exception exc)
            {
                // do nothing
            }
            finally
            {
                Environment.Exit(0);
            }
            
 
        }
    }
}