using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

using IdleCs.Library;
using IdleCs.Utils;

using IdleCs.GameLogic;
using IdleCs.Logger;
using IdleCs.ServerCore;
using UnityEngine.PlayerLoop;

namespace IdleCs.CombatServer
{
    public class CombatServerConfigOwn
    {
        public EnvMode EnvMode { get; set; }
        public JsonServer Server { get; set; }
    }
    
    public class CombatServerConfigConst
    {
        
        //-사용자 변수에서 값을 읽어 온다.
        //public static EnvironmentVariableTarget ENVIRONMENT_VARIABLES_TARGET = EnvironmentVariableTarget.User;
        
        //-시스템 변수에서 값을 읽어 온다. 
        public static EnvironmentVariableTarget ENVIRONMENT_VARIABLES_TARGET = EnvironmentVariableTarget.Machine;
        
        public static string ENVIRONMENT_VARIABLES_KEY = "CombatServerIndex";
        public static string CONFIG_FILE_PATH = "../Config/";
        public static string CONFIG_FILE_NAME = "CombatServerConfig.json";
        
        public static ulong GAME_TICK_INTERVAL_MS = 100;
        public static ulong EMPTY_ROOM_ALIVE_TIME_MS = 3000;
        public static ulong EMPTY_ROOM_FINISH_ALIVE_TIME_MS = 1000;
        public static ulong ROOM_STATE_UPDATE_INTERVAL = 60000; // ms 1min
        //public static ulong WILL_BE_DESTROY_ROOM_LAST_ALIVE_TIME_MS = 3000; 

        public static int MAX_PACKET_SIZE = 8096;
        public static uint SOCKET_POOL_SIZE = 100;
        //public static uint ACCEPT_SOCKET_COUNT = 10;
        
        public static string REDIS_RECV_COMMAND_QUEUE = "queue-web-to-combat-";
        public static string REDIS_SEND_COMMAND_QUEUE = "queue-combat-to-web-";
        public static string REDIS_SEND_HACK_QUEUE = "queue-combat-to-web-hack";
        public static string REDIS_SERVER_STATISTIC = "cb-server-statistic";
        public static string REDIS_SERVER_ROOMLIST = "cb-server-current-roomlist-";
        public static int REDIS_DB_INDEX = 0;
        public static string REDIS_SERVER_REGIST_HASH_KEY = "cb-server-list";
        public static string REDIS_SERVER_KEEP_ALIVE_KEY = "cb-keep-";

        public static string REDIS_SEND_LOG = "queue-log-combat-to-web";

        public static int CHATTING_CHANNEL_COUNT = 10;
        public static int CHATTING_MESSAGE_MAX_COUNT = 100;
    }
    
    
    public class CombatServerConfig : Singleton<CombatServerConfig>
    {
        public bool Initialize(string root, string fileName)
        {
            _serverGroup = new Dictionary<EnvMode, List<JsonServer>>();
            _configOwn = new CombatServerConfigOwn();

            //-loading json
            if (false == LoadFile(root, fileName))
            {
                Console.WriteLine("Can't load file[{0}/{1}]", root, fileName);
                return false;
            }
           
            int serverIndex = 0;
            EnvMode envMode = EnvMode.none;

            //-force set config without environment variable (use for test) 
            // if (IsForceSelect(out serverIndex, out envMode))
            // {
            //     CorgiLog.Log(CorgiLogType.Info, "Force set config without environment variable. ServerMode[{0}] ServerIndex[{1}] for test", 
            //         envMode.ToString(), serverIndex);
            //     //return SetConfigOwn(serverMode, serverIndex);
            //     ServerIndex = serverIndex;
            //     EnvMode = envMode;
            //     return true;
            // }
            
            //-loading environment variable
            if (false == LoadEnvironmentVariable(out serverIndex, out envMode))
            {
                Console.WriteLine("Can't load environment variables. target[{0}], key[{1}]",
                    CombatServerConfigConst.ENVIRONMENT_VARIABLES_TARGET.ToString(), CombatServerConfigConst.ENVIRONMENT_VARIABLES_KEY);
                              
                return false;
            }

            //return SetConfigOwn(serverMode, serverIndex);
            ServerIndex = serverIndex;
            EnvMode = envMode;
            //ServerMode = ServerMode.Console;
            return true;
        }

        public int ServerIndex = 1;
        public EnvMode EnvMode = EnvMode.development;
        public ServerMode ServerMode = ServerMode.None;
        public bool IsTest = false;
        public JsonServer Server => _configOwn.Server;
        
        private bool LoadEnvironmentVariable(out int outServerIndex, out EnvMode outEnvMode)
        {
            outServerIndex = 0;
            outEnvMode = EnvMode.none;

            try
            {
                var variables = Environment.GetEnvironmentVariable(CombatServerConfigConst.ENVIRONMENT_VARIABLES_KEY, CombatServerConfigConst.ENVIRONMENT_VARIABLES_TARGET);
                
                CorgiLog.Log(CorgiLogType.Info, "Get variables[{0}], from [{1}][{2}]", variables,
                    CombatServerConfigConst.ENVIRONMENT_VARIABLES_KEY,
                    CombatServerConfigConst.ENVIRONMENT_VARIABLES_TARGET);

                var separated = variables.Split('-').Select(element => element.Trim())
                    .Where(element => !string.IsNullOrEmpty(element)).ToList();
                if (2 > separated.Count)
                {
                    CorgiLog.Log(CorgiLogType.Error, "there are less parameter at {0} in {1}",
                        CombatServerConfigConst.ENVIRONMENT_VARIABLES_KEY,
                        CombatServerConfigConst.ENVIRONMENT_VARIABLES_TARGET);
                    return false;
                }

                if (false == EnvMode.TryParse(separated.ElementAt(0), out outEnvMode))
                {
                    CorgiLog.Log(CorgiLogType.Error, "ServerMode[{0}] is wrong at [{1}]",
                        CombatServerConfigConst.ENVIRONMENT_VARIABLES_KEY);
                    return false;
                }

                outServerIndex = Int32.Parse(separated.ElementAt(1));

                return true;
            }
            catch (Exception e)
            {
                CorgiLog.Log(CorgiLogType.Error, "Occur exception : {0}", e.ToString());
                return false;
            }
        }

        private bool LoadFile(string path, string fileName)
        {
            CorgiLog.Log(CorgiLogType.Info, "Ready Load config file. path[{0}] file[{1}]", path, fileName);
            
            try
            {
                using (StreamReader stream = new StreamReader(path + fileName))
                {
                    string strJson = stream.ReadToEnd();
                    List<JsonServerGroup> elementList = JsonConvert.DeserializeObject<List<JsonServerGroup>>(strJson);

                    var findError = elementList.Any(element => {
                        if (false == SetServerGroup(element))
                        {
                            return true;
                        }
                        return false;
                    });
                    return (false == findError);
                }
            }
            catch (IOException e)
            {
                CorgiLog.Log(CorgiLogType.Error, "Occur exceptoin : {0}", e.ToString());
                return false;
            }
        }

        public bool OnInitialize()
        {
            Console.WriteLine("Server mode[{0}]/[{1}]", ServerMode.ToString(), Program.IsServiceMode);
            if (ServerMode == ServerMode.None)
            {
                if (Program.IsServiceMode)
                {
                    ServerMode = ServerMode.Service;
                }
                else
                {
                    ServerMode = ServerMode.Console;
                }
            }
            Console.WriteLine( "Server mode[{0}]", ServerMode.ToString());
            
            List<JsonServer> list = null;
            if (_serverGroup.TryGetValue(EnvMode, out list))
            {
                var find = list.Find(element => element.Index == ServerIndex);
                if (null != find)
                {
                    _configOwn.EnvMode = EnvMode;
                    _configOwn.Server = find;
                    Console.WriteLine( "Env mode[{0}] and index[{1}] was Set", EnvMode.ToString(), ServerIndex);

                    return true;
                }
            }

            
            Console.WriteLine( "Can't find server mode[{0}] and index[{1}] on memory", EnvMode.ToString(), ServerIndex);
            return false;
        }

        private bool SetServerGroup(JsonServerGroup group)
        {
            if (FindDuplicateIndex(group.ServerList))
            {
                CorgiLog.Log(CorgiLogType.Error, "Server type[{0}] has duplicate server index from {1}", group.EnvMode, 
                    CombatServerConfigConst.CONFIG_FILE_NAME);
                return false;
            }
            
            List<JsonServer> serverList = null;
            if (_serverGroup.TryGetValue(group.EnvMode, out serverList))
            {
                CorgiLog.Log(CorgiLogType.Error, "Server type[{0}] has declared duplicated from {1}", group.EnvMode,
                    CombatServerConfigConst.CONFIG_FILE_NAME);
                return false;
            }
            
            _serverGroup.Add(group.EnvMode, group.ServerList);
            return true;

        }

        private bool FindDuplicateIndex(List<JsonServer> list)
        {
            var anyDuplicte = list.GroupBy(element => element.Index).Any(g => g.Count() > 1);
            return anyDuplicte;
        }

        private bool IsForceSelect(out int outServerIndex, out EnvMode outEnvMode)
        {
            outServerIndex = 0;
            outEnvMode = EnvMode.none;
            
            int selectedServerIndex = 0;
            EnvMode selectedEnvMode = EnvMode.none;

            bool find = _serverGroup.Any(pair =>
            {
                bool findIndex = pair.Value.Any(value =>
                {
                    if (true == value.ForceSelect)
                    {
                        selectedServerIndex = value.Index;
                        return true;    
                    }

                    return false;
                });

                if (findIndex)
                {
                    selectedEnvMode = pair.Key;
                }

                return findIndex;
            });

            if (find)
            {
                outServerIndex = selectedServerIndex;
                outEnvMode = selectedEnvMode;
            }

            return find;
        }
        
        private Dictionary<EnvMode, List<JsonServer>> _serverGroup = null;
        private CombatServerConfigOwn _configOwn = null;
    }
}
