using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Corgi.GameData;
using IdleCs.CombatServer;
using IdleCs.Logger;
using IdleCs.Managers;
using IdleCs.ServerUtils;
using IdleCs.Network;
using IdleCs.ServerContents;
using IdleCs.Utils;


namespace IdleCs.ServerUtils
{

    public class ValidTag
    {
        private string _myText;

        public string MyText
        {
            get { return MyText; }
            set { _myText = value; }
        }
    }

    public enum TesterOrderKey
    {
        None = 0,
        Order1,
        Order2,
        Order3,
        Order4,
        Order5,
        Order6,
        Order7,
        Order8,
        Order9
    }
    
    public class TestOrder
    {
        public static void Initalize()
        {
            var orders = new ConcurrentDictionary<TesterOrderKey, Action>();
            orders.TryAdd(TesterOrderKey.Order1, OnOrder1);
            orders.TryAdd(TesterOrderKey.Order2, OnOrder2);
            orders.TryAdd(TesterOrderKey.Order3, OnOrder3);
            orders.TryAdd(TesterOrderKey.Order4, OnOrder4);
            orders.TryAdd(TesterOrderKey.Order5, OnOrder5);
            orders.TryAdd(TesterOrderKey.Order6, OnOrder6);
            orders.TryAdd(TesterOrderKey.Order7, OnOrder7);
            orders.TryAdd(TesterOrderKey.Order8, OnOrder8);
            orders.TryAdd(TesterOrderKey.Order9, OnOrder9);

            var watchDogPort = CombatServerConfig.Instance.Server.WatchDogPort;
            var watchDogStatus = (0 >= watchDogPort) ? "not operated" : "will operating"; 
            
            CorgiLog.Log(CorgiLogType.Info, "* Watch dog port[{0}]. {1}", watchDogPort, watchDogStatus);

            if (0 >= watchDogPort)
            {
                return;
            }
            
            
            WatchDog.Instance.Initialize(watchDogPort, orders);
            WatchDog.Instance.Operate();
        }

        private static void OnOrder1()
        {
            Console.WriteLine(MethodBase.GetCurrentMethod().Name);
            //-do something
            
            
//            RoomManager.Instance.SerializeMethod("Test", 0);
//            CorgiLog.Log(CorgiLogType.Info, "Do Test by index 0");
        }
        
        private static void OnOrder2()
        {
            Console.WriteLine(MethodBase.GetCurrentMethod().Name);
            //-do something
            
//            RoomManager.Instance.SerializeMethod("Test", 1);
//            CorgiLog.Log(CorgiLogType.Info, "Do Test by index 1");
        }
        
        private static void OnOrder3()
        {
            Console.WriteLine(MethodBase.GetCurrentMethod().Name);
            //-do something
            
            //-stack over flow crash.
            /*
             ValidTag tag = new ValidTag();
             string str = tag.MyText;
             */


//            var before = CorgiTime.UtcNowULong;
//            Thread.Sleep(1500);
//            var after = CorgiTime.UtcNowULong;


            return;
        }
        
        private static void OnOrder4()
        {
            Console.WriteLine(MethodBase.GetCurrentMethod().Name);
            //-do something

            int index = 10;
            string data1 = "usercount:0";
            string data2 = "roomcount:0";
            RedisManager.Instance.Test_SendServerInfo(index, data1, data2);
        }
        
        private static void OnOrder5()
        {
            Console.WriteLine(MethodBase.GetCurrentMethod().Name);
            //-do something

            var task = RedisManager.Instance.Test_RecvServerInfo();
            var thisTask = new Task(async () =>
            {
                var result = await task;
                return;
            });
            thisTask.Start();            
            return;
        }

        private static void OnOrder6()
        {
            Console.WriteLine(MethodBase.GetCurrentMethod().Name);
            
            Console.WriteLine("All game, will be win!!!");
            RoomManager.Instance.SerializeMethod("Test_BooValue", true);
            
        }
        
        private static void OnOrder7()
        {
            Console.WriteLine(MethodBase.GetCurrentMethod().Name);
            
            Console.WriteLine("All game, will be Lose!!!");
            RoomManager.Instance.SerializeMethod("Test_BooValue", false);
        }
        
        private static void OnOrder8()
        {
            Console.WriteLine(MethodBase.GetCurrentMethod().Name);
            
            //TestHolder.Hold(8, 5000);
            //TestHolderManager.Instance.Process(1, 3000);

            int holdTimeMs = 0;
            CorgiLog.Log(CorgiLogType.Warning, "Test, set hold time MS[{0}]", holdTimeMs);
            TestHolderManager.Instance.Reset(0);
        }
        
        private static void OnOrder9()
        {
            Console.WriteLine(MethodBase.GetCurrentMethod().Name);
            
            //TestHolder.Hold(9, 2000);
            //TestHolderManager.Instance.Process(2, 6000);


            int holdTimeMs = CombatServerConfig.Instance.Server.TestValue1;
            CorgiLog.Log(CorgiLogType.Warning, "Test, set hold time MS[{0}]", holdTimeMs);
            TestHolderManager.Instance.Reset(holdTimeMs);
        }
        
    }
}