using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using IdleCs.GameLog;
using IdleCs.GameLogic;
using IdleCs.GameLogic.SharedInstance;
using IdleCs.Managers;
using IdleCs.Network;
using IdleCs.Network.NetLib;
using IdleCs.ServerContents;
using IdleCs.Utils;

using IdleCs.Logger;
using IdleCs.ServerSystem;
using IdleCs.ServerUtils;

namespace IdleCs.CombatServer
{
    public class CombatServerApp
    {
        private int _serverIndex;
        private EnvMode _envMode;
        
        public CombatServerApp()
        {
        }
        
        public bool Initialize(string[] args)
        {
            try
            {
                // Initialize Log System
                //CorgiCombatLog.Initialize(new CombatLogCategory[] {CombatLogCategory.System});


                // Parsing Parameter
                int curIndex = 0;
                var curFilePath = AppDomain.CurrentDomain.BaseDirectory;
                var configFilePath = curFilePath + "\\"+CombatServerConfigConst.CONFIG_FILE_PATH;
                if (Program.IsServiceMode)
                {
                    // if (0 >= args.Count())
                    // {
                    //     CorgiLog.Log(CorgiLogType.Error, "Service mode should have argument what has [{0}] path", CombatServerConfigConst.CONFIG_FILE_NAME);
                    //     return false;
                    // }
                    // configFilePath = args[0];
                    //curIndex++;
                }
                
                // load Config
                if (CombatServerConfig.Instance.Initialize(configFilePath, CombatServerConfigConst.CONFIG_FILE_NAME) == false)
                {
                    return false;
                }
                

                // check parameter for config
                // 같은 옵션이면 parameter 우선
                for (; curIndex < args.Length; curIndex++)
                {
                    var curArg = args[curIndex];
                    if (curArg.StartsWith("--") == false)
                    {
                        continue;
                    }

                    var optionStrs = curArg.Substring(2).Split('=');
                    if (optionStrs.Length <= 1)
                    {
                        // need more option
                        continue;
                    }

                    var optionKey = optionStrs[0];
                    var optionValue = optionStrs[1];

                    if (optionKey == "index")
                    {
                        // server index
                        var index = Int32.Parse(optionValue);
                        CombatServerConfig.Instance.ServerIndex = index;
                    }else if(optionKey == "env")
                    {
                        var envMode = CorgiEnum.ParseEnum<EnvMode>(optionValue);
                        CombatServerConfig.Instance.EnvMode = envMode;
                    }else if (optionKey == "mode")
                    {
                        // server mode
                        var serverMode = CorgiEnum.ParseEnum<ServerMode>(optionValue);
                        CombatServerConfig.Instance.ServerMode = serverMode;
                    }else if (optionKey == "test")
                    {
                        // server mode
                        var isTest = Boolean.Parse(optionValue);
                        CombatServerConfig.Instance.IsTest= isTest;
                    }

                }
                
                if (CombatServerConfig.Instance.OnInitialize() == false)
                {
                    return false;
                }
                
                CorgiLogServer log = new CorgiLogServer();
                if (false == log.Initialize(typeof(CombatServerApp), CombatServerConfig.Instance.Server.LogPath))
                {
                    Console.WriteLine("[error] Log4NetLog Initialize failed");
                    return false;
                }
                if (false == CorgiLog.Initialize(log))
                {
                    Console.WriteLine("[error] CorgiLog Initialize failed");
                    return false;
                }

                CorgiLog.Log(CorgiLogType.Info, "Begin CombatServer. Mode[{0}] Config file path[{1}]", Program.IsServiceMode ? "Service" : "Console", configFilePath);
                
            }
            catch (Exception e)
            {
                Console.Write("Occur exception : {0}", e.ToString());
                CorgiLog.Log(CorgiLogType.Error, "Occur exception : {0}", e.ToString());
                return false;
            }
            
            if (false == InitServer())
            {
                Thread.Sleep(10);
                return false;
            }

            CorgiLog.Log(CorgiLogType.Info, "Server initialize complated Mode[{0}], Index[{1}]", 
                CombatServerConfig.Instance.EnvMode.ToString(), CombatServerConfig.Instance.Server.Index);
            
            // todo : download sheet
            ServerGameDataManager.Instance.LoadData();

            if (false == InitContents())
            {
                return false;
            }
            
            return true;
        } 
        
        public void StartServer()
        {
            bool isReady = false;
            while (isReady == false)
            {
                Thread.Sleep(500);
                //isReady = RedisManager.Instance.IsReady && GameDataManager.Instance.GameDataReady;
                isReady = RedisManager.Instance.IsReady;
            }

            var serverMode = CombatServerConfig.Instance.ServerMode;
            
            CorgiLog.Log(CorgiLogType.Info, "* Server Start in {0} mode", serverMode.ToString());
            
            // Get Server Config
            var userBindIP = CombatServerConfig.Instance.Server.UserBindIP;
            var userBindPort = CombatServerConfig.Instance.Server.UserBindPort; 
            
            // regist server info to REDIS
            RedisManager.Instance.SerializeMethod("RegistServer", userBindIP, userBindPort);


            if (serverMode != ServerMode.Service)
            {
                Console.Title = String.Format("combat server index[{0}] ip[{1}] port:[{2}], revision[{3}]",
                    CombatServerConfig.Instance.Server.Index,
                    CombatServerConfig.Instance.Server.UserBindIP,
                    CombatServerConfig.Instance.Server.UserBindPort,
                    GameDataManager.Instance.Revision);
            }
            
            if (serverMode != ServerMode.StressTest)
            {
                // Listening
                var socketFactory = new AsyncTcpSocketFactory<CorgiServerConnection>(CombatServerConfigConst.SOCKET_POOL_SIZE);
                var server = new AsyncTcpAcceptor(socketFactory);

                server.Accept(userBindPort);
            }
            else
            {
                StressTestManager.Instance.InitializeStressTest();
                
                var socketFactory = new AsyncTcpSocketFactory<CorgiServerTestConnection>(CombatServerConfigConst.SOCKET_POOL_SIZE);
                var server = new AsyncTcpAcceptor(socketFactory);

                server.Accept(userBindPort);
            }

            CorgiLog.Log(CorgiLogType.Info, "* Keep alive time interval MS : {0}", CombatServerConfig.Instance.Server.AliveSignalTimeIntervalMS);
            CorgiLog.Log(CorgiLogType.Info, "* Keep alive valid wait time MS : {0}", CombatServerConfig.Instance.Server.AliveSignalValidWaitTimeMS);
            CorgiLog.Log(CorgiLogType.Info, "* Redis time out MS : {0}", CombatServerConfig.Instance.Server.RedisRequestTimeOutMS);
            CorgiLog.Log(CorgiLogType.Info, "* Server Start. port : {0}", userBindPort);

            
            var count = 0L;
            var curTick = CorgiTime.UtcNowULong;
            
            
            StressTestManager.Instance.StartStressTest();

            AliveSignalManager.Instance.Start();

            var lastGCTime = CorgiTime.UtcNowULong;
            
            while (false == Program.IsProgramStop)
            {
                if (count % 100 == 0)
                {
                    Console.Write(".");
                }

                var nextTick = CorgiTime.UtcNowULong;
                if (nextTick - curTick > CombatServerConfigConst.GAME_TICK_INTERVAL_MS)
                {
                    RoomManager.Instance.SerializeMethod("Tick");

                    StatDataManager.Instance.SerializeMethod("Tick");


                    if (serverMode == ServerMode.StressTest)
                    {
                        StressTestManager.Instance.SerializeMethod("Tick");
                    }
                    
                    //CorgiLog.LogLine("BaseTick {0}/{1} = {2}", nextTick , curTick, nextTick - curTick);
                    curTick += CombatServerConfigConst.GAME_TICK_INTERVAL_MS;
                }
                else
                {
                    // room tick 외의 타이밍에 처리
                    RedisManager.Instance.SerializeMethod("Tick");
                    
                    var diffTime = CorgiTime.UtcNowULong - lastGCTime;
                    if (diffTime > 60000 )
                    {
                        GC.Collect();
                        lastGCTime = CorgiTime.UtcNowULong;
                    }
                    else
                    {
                        Thread.Sleep(10);
                    }
                }

                if (count > Int32.MaxValue)
                {
                    count = 0;
                }
                else
                {
                    count++;
                }
            }
        }

        bool InitServer()
        {
            int PROCESSOR_COUNT = Environment.ProcessorCount;
            
            CorgiLog.Log(CorgiLogType.Info, "This system has {0} process", PROCESSOR_COUNT);

            var threadCount = PROCESSOR_COUNT;

            if (threadCount < 8)
            {
                threadCount = 8;
            }
            
            // if (ThreadPool.SetMaxThreads(threadCount, threadCount) == false)
            // {
            //     throw new CorgiException("can't set max threads");
            // }

            if (ThreadPool.SetMinThreads(threadCount, threadCount) == false)
            {
                throw new CorgiException("can't set min threads");
            }
            
            ShowAvailableThreads();
            
            CorgiServerConnection.InitProtocol();
            
            try
            {
                RedisManager.Instance.InitSingleton();
                RedisManager.Instance.Connect();
            }
            catch (Exception e)
            {
                CorgiLog.Log(CorgiLogType.Error, "Occur execption : {0}", e.ToString());
                return false;
            }
            
            while (RedisManager.Instance.IsReady == false)
            {
                Thread.Sleep(10);
            }
            
            return true;
        }

        bool InitContents()
        {
            if (RoomManager.Instance.InitSingleton() == false)
            {
                return false;
            }
            if (ChattingManager.Instance.InitSingleton() == false)
            {
                return false;
            }
            if (StatDataManager.Instance.InitSingleton() == false)
            {
                return false;
            }
            
            //StatDataManager.Instance.InitSingleton();
            
            // GameLogic
            SkillFactory.Init();
            SkillCompFactory.Init();
            SkillConditionCompFactory.Init();
            SkillTargetCompFactory.Init();
            AliveSignalManager.Instance.InitSingleton();
            return true;
        }

        
        static void ShowAvailableThreads()
        {
            int workerThreads = 0;
            int completionPortThreads = 0;
         
            ThreadPool.GetAvailableThreads(out workerThreads, out completionPortThreads);
            CorgiLog.Log(CorgiLogType.Info, "WorkerThreads: {0}, CompletionPortThreads: {1}", workerThreads, completionPortThreads);
        }
    }
}