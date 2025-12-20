using System;
using System.Collections.Generic;

namespace IdleCs.CombatServer
{
    public enum EnvMode
    {
        //-warning! keep text to lower case character. don't make upper case. don't appy camel notation. 
        none = 0,
        standalone = 1,
        development = 2,
        qa = 3,
        test = 4,
        review = 5,
        review2 = 6,
        fgt = 7,
        global = 10
    }

    public enum ServerMode
    {
        None = 0
        , Console = 1
        , Service = 2
        , StressTest = 3
    }

    public enum HackType
    {
        None = 0
        , NoConnected_CombatServer = 100
        , NoConnected_CombatServer_AcquireSkill = 110
        , NoConnected_CombatServer_AcquireEquip = 120
    }

    public class JsonServer
    {
        public int Index { get; set; }
        public string UserBindIP { get; set; }
        public ushort UserBindPort { get; set; }
        public int CommandCount { get; set; } // WebServer CommandProcessor 개수
        public string RedisIP { get; set; }
        public string RedisOption { get; set; }
        public ulong RedisRequestTimeOutMS { get; set; }
        
        public ulong AliveSignalTimeIntervalMS { get; set; }

        public ulong AliveSignalValidWaitTimeMS { get; set; }
        
        public bool AllowNoConnectionHunting { get; set; }
        
        public bool ForceSelect { get; set; }
        
        public int WatchDogPort { get; set; }
        
        public int TestValue1 { get; set; }
        public string LogPath{ get; set; }
        
        public JsonServer()
        {
            Index = 0;
            UserBindIP = string.Empty;
            UserBindPort = 0;
            CommandCount = 1; 
            RedisIP = string.Empty;
            RedisOption = string.Empty;
            RedisRequestTimeOutMS = 0;
            AliveSignalTimeIntervalMS = 0;
            AliveSignalValidWaitTimeMS = 0;
            AllowNoConnectionHunting = false;
            ForceSelect = false;
            WatchDogPort = 0;
            TestValue1 = 0;
            LogPath = "./Log/";
        }
    }

    public class JsonServerGroup
    {
        public EnvMode EnvMode { get; set; }
        public List<JsonServer> ServerList { get; set; }
    }
}


