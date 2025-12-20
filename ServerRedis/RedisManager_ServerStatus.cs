using System.Collections.Generic;
using System.Threading.Tasks;

using StackExchange.Redis;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using IdleCs.CombatServer.ServerCommand;
using IdleCs.ServerContents;
using IdleCs.Network;
using IdleCs.Logger;
using IdleCs.CombatServer;
using IdleCs.GameLogic;
using IdleCs.Utils;


namespace IdleCs.Managers
{
    public partial class RedisManager
    {
        public void Test_SendServerInfo(int index, string data1, string data2)
        {
            JObject jo = new JObject();
            jo.Add("index", new JValue(index));
            jo.Add("data1", new JValue(data1));
            jo.Add("data2", new JValue(data2));

            string key = "test-server-info-key";
            
            var db = _redis.GetDatabase(CombatServerConfigConst.REDIS_DB_INDEX);
            db.StringSetAsync(key, jo.ToString());
        }

        //public Task<RedisValue[]> StringGetAsync(RedisKey[] keys, CommandFlags flags = CommandFlags.None)
        public Task<RedisValue> Test_RecvServerInfo(CommandFlags flags = CommandFlags.None)
        {
            string key = "test-server-info-key";
            var db = _redis.GetDatabase(CombatServerConfigConst.REDIS_DB_INDEX);
            return db.StringGetAsync(key, flags);
        }

        void Ping_Serialized()
        {
            var db = _redis.GetDatabase(CombatServerConfigConst.REDIS_DB_INDEX);
            db.CreateBatch();
            var asyncRet = db.ExecuteAsync("ping");

            var task = new Task(async () =>
            {
                var result = await asyncRet;
                RoomManager.Instance.SerializeMethod("PongFromRedis");
                return;
            });
            
            task.Start();            
        }
        
        void RegistServer_Serialized(string userBindIP, ushort userBindPort)
        {
            var db = _redis.GetDatabase(CombatServerConfigConst.REDIS_DB_INDEX);

            var hashKey = CombatServerConfigConst.REDIS_SERVER_REGIST_HASH_KEY;
            var serverIndex = CombatServerConfig.Instance.Server.Index;
            var host = userBindIP;
            var port = userBindPort;
            
            JObject json = new JObject();
            json.Add("index", new JValue(serverIndex));
            json.Add("host", new JValue(host));
            json.Add("port", new JValue(port));
            if (CombatServerConfig.Instance.IsTest)
            {
                json.Add("test", new JValue(true));
            }
            var value = json.ToString().Replace("\r\n", string.Empty).Replace("  ", string.Empty).Replace(",", ", ");
            
            /*
            var asyncTaskGet = new Task(async () =>
            {
                var result = await db.HashSetAsync(serverIndex.ToString(), CombatServerConfigConst.REDIS_SERVER_REGIST_HASH_FIELD, json.ToString());
                CorgiLog.Log(CorgiLogType.Info, "Regist server to redis result[{0}]", result);
                
            });
            asyncTaskGet.Start();
            */
            Task.Run(async () =>
            {
                await db.HashSetAsync(hashKey, serverIndex.ToString(), value);
                CorgiCombatLog.Log(CombatLogCategory.System, "* Regist this server, index[{0}] host[{1}] port[{2}] to redis[{3}]", serverIndex, host, port, hashKey); 
            });   
        }
        
        void RecordServerStatus_Serialized(List<string> roomIdList)
        {
            //-ref : https://stackoverflow.com/questions/1208030/can-json-net-handle-a-listobject
            
            //-test
            //List<string> roomIdList = new List<string>();
            //roomIdList.Add("123");
            //roomIdList.Add("456");
            //roomIdList.Add("789");
  
            JObject json = new JObject();
            json.Add("TimeStamp", new JValue(CorgiTime.UtcNowULong));
            json.Add("RoomIdList", new JValue(JsonConvert.SerializeObject(roomIdList)));

            string key = string.Format("CombatServer-{0}", CombatServerConfig.Instance.Server.Index);
            var db = _redis.GetDatabase(CombatServerConfigConst.REDIS_DB_INDEX);
            db.StringSetAsync(key, json.ToString());
            
            //Console.WriteLine(json.ToString());
        }
        
        void KeepAlive_Serialized()
        {
            string key = CombatServerConfigConst.REDIS_SERVER_KEEP_ALIVE_KEY + CombatServerConfig.Instance.Server.Index.ToString();
            var value = CorgiTime.UtcNowULong;
            
            var db = _redis.GetDatabase(CombatServerConfigConst.REDIS_DB_INDEX);
            
            Task.Run(async () =>
            {
                var asyncResult = await db.StringSetAsync(key, value);
                //CorgiLog.Log(CorgiLogType.Info, "Send keep alive, key[{0}] value[{1}] result[{2}]", key, value, asyncResult);
            });
        }

        void RoomDeleted_Serialized(string roomId)
        {
            JObject json = new JObject();
            json.Add("roomId", new JValue(roomId));
            
            SendCmd(CommandType.RoomDeleted, json);
        }
    }
}