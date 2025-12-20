using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.Remoting.Messaging;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StackExchange.Redis;

using Corgi.Protocol;
using IdleCs.CombatServer;
using IdleCs.CombatServer.ServerCommand;
using IdleCs.GameLogic;
using IdleCs.GameLogic.SharedInstance;
using IdleCs.Library;
using IdleCs.Network;
using IdleCs.Network.NetLib;
using IdleCs.ServerCore;
using IdleCs.Utils;

using IdleCs.Logger;
using IdleCs.ServerContents;
using IdleCs.ServerUtils;
using ServerMode = IdleCs.CombatServer.ServerMode;


namespace IdleCs.Managers
{
    
    public partial class RedisManager : CorgiServerObjectSingleton<RedisManager>
    {
        private string _address = string.Empty;
        private string _redisConfig = string.Empty;
        private ConnectionMultiplexer _redis = null;
        private string _recvQueueKey = string.Empty;
        private string _sendQueueKey = string.Empty;
        private string _sendHackQueueKey = string.Empty;
        private string _serverStatisticKey = string.Empty;
        private string _serverRoomListKey = string.Empty;
        private string _logKey = string.Empty;

        private bool _isReady = false;
        public bool IsReady => _isReady;
        
        Dictionary<CommandType, RedisCommand> _commandMap = new Dictionary<CommandType, RedisCommand>();

        private ChannelMessageQueue _redisChannel = null;
        private List<ChannelMessageQueue> _chattingChannelList = null;

        
        protected override bool Init()
        {
            _address = CombatServerConfig.Instance.Server.RedisIP;
            _redisConfig = CombatServerConfig.Instance.Server.RedisOption;
            _recvQueueKey = CombatServerConfigConst.REDIS_RECV_COMMAND_QUEUE + CombatServerConfig.Instance.Server.Index.ToString();

            var commandIndex = (int)(CombatServerConfig.Instance.Server.Index % 
                               CombatServerConfig.Instance.Server.CommandCount) + 1;
            _sendQueueKey = CombatServerConfigConst.REDIS_SEND_COMMAND_QUEUE + commandIndex;
            _sendHackQueueKey = CombatServerConfigConst.REDIS_SEND_HACK_QUEUE;
            
            _serverStatisticKey = CombatServerConfigConst.REDIS_SERVER_STATISTIC +
                                   CombatServerConfig.Instance.Server.Index.ToString();
            _serverRoomListKey = CombatServerConfigConst.REDIS_SERVER_ROOMLIST +
                                   CombatServerConfig.Instance.Server.Index.ToString();

            _logKey = CombatServerConfigConst.REDIS_SEND_LOG;
            
            _commandMap.Add(CommandType.Log, new LogCommand());
            _commandMap.Add(CommandType.Revision, new RevisionCommand());
            
            _commandMap.Add(CommandType.StageCompleted, new StageCompletedCommand());
            _commandMap.Add(CommandType.ChallengeStart, new ChallengeStartCommand());
            _commandMap.Add(CommandType.ChallengeCompleted, new ChallengeCompletedCommand());
            _commandMap.Add(CommandType.AutoHuntingStart, new AutoHuntingStartCommand());
            
            _commandMap.Add(CommandType.InstanceDungeonStart, new InstanceDungeonStartCommand());
            _commandMap.Add(CommandType.InstanceDungeonStop, new InstanceDungeonStopCommand());
            _commandMap.Add(CommandType.InstanceDungeonCompleted, new InstanceDungeonCompletedCommand());
            
            _commandMap.Add(CommandType.PartyJoin, new PartyJoinCommand());
            _commandMap.Add(CommandType.PartyLeave, new PartyLeaveCommand());
            _commandMap.Add(CommandType.PartyExile, new PartyExileCommand());
            
            _commandMap.Add(CommandType.EventAcquireSkillItem, new EventAcquireSkillItemCommand());
            _commandMap.Add(CommandType.EventAcquireEquipItem, new EventAcquireEquipItemCommand());
            
            _commandMap.Add(CommandType.RoomStatus, new RoomStatusCommand());
            _commandMap.Add(CommandType.RoomKill, new RoomKillCommand());
            
            _commandMap.Add(CommandType.WorldBossCompleted, new WorldBossCompletedCommand());
            _commandMap.Add(CommandType.WorldBossStop, new WorldBossStopCommand());
            _commandMap.Add(CommandType.RiftOpen, new RiftOpenCommand());
            _commandMap.Add(CommandType.RiftCompleted, new RiftCompletedCommand());
            _commandMap.Add(CommandType.RiftStop, new RiftStopCommand());
            _commandMap.Add(CommandType.PvpCompleted, new ArenaCompletedCommand());
            _commandMap.Add(CommandType.PvpStop, new ArenaStopCommand());

            //-partial 
            _taskRecords = new ConcurrentDictionary<string, RedisManager.Record>();
            
            return true;
        }

        public async void Connect()
        {
            try
            {
                CorgiCombatLog.Log(CombatLogCategory.System, "Try to connect redis : {0}", _address);     
                
                var connectStr = string.Format("{0},{1}", _address, _redisConfig);
                var redisTask = ConnectionMultiplexer.ConnectAsync(connectStr);
                var result = await redisTask;

                if (null == result)
                {
                    throw new CorgiException("failed to connect redis : {0}", _address);
                }
                
                _redis = result;

                _isReady = true;
                
                CorgiCombatLog.Log(CombatLogCategory.System, "Redis connect[{0}] completed, config[{1}], receive que[{2}], send que[{3}]", _address, _redisConfig, _recvQueueKey, _sendQueueKey);
                
                // web cmd channel sub channel
                var subscriber = _redis.GetSubscriber();
                var channelName = string.Format("server-cmd-channel-{0}",
                    CombatServerConfig.Instance.EnvMode.ToString().ToLower());
                if (CombatServerConfig.Instance.EnvMode == EnvMode.standalone)
                {
                    // 땜빵...
                    channelName += "Alter";
                }
                
                _redisChannel = subscriber.Subscribe(channelName);
                CorgiCombatLog.Log(CombatLogCategory.System, "Redis Subsribe : {0}", channelName);
                
                _redisChannel.OnMessage(OnMessage);
                
                // global chatting channel
                _chattingChannelList = new List<ChannelMessageQueue>();
                 for (var i = 0; i < CombatServerConfigConst.CHATTING_CHANNEL_COUNT; i++)
                 {
                     var channelKey = $"channel_general_{i}";
                     
                     AddChattingSubscriber(channelKey);
                 }
            }
            catch (Exception e)
            {
                CorgiLog.Log(CorgiLogType.Error, "Occur exception : {0}", e.ToString());
                throw;
            }
        }

        // serialized by chatting manager
        public void AddChattingSubscriber(string channelKey)
        {
             var subscriber = _redis.GetSubscriber();
             var chattingChannelName = string.Format("server-chatting-channel-{0}-{1}",
                 CombatServerConfig.Instance.EnvMode.ToString().ToLower() , channelKey);
        
             subscriber.Subscribe(chattingChannelName, OnChattingMessage);
             CorgiCombatLog.Log(CombatLogCategory.Chatting, $"[Chat] Regist Chatting Channel {chattingChannelName})");
        }

        
        
        // command processor in pub/sub
        void OnMessage(ChannelMessage message)
        {
            try
            {
                var commandJson = JObject.Parse((string)message.Message);
                if ((false == CorgiJson.IsValid(commandJson,"command"))
                && (false == CorgiJson.IsValid(commandJson, "sender"))
                && (false == CorgiJson.IsValid(commandJson, "data")))
                {
                    CorgiLog.Log(CorgiLogType.Error, "Invalid command json string");                  
                    return;
                }

                var commandStr = CorgiJson.ParseString(commandJson, "command");
                var senderStr = CorgiJson.ParseString(commandJson, "sender");
                var data = CorgiJson.ParseObject(commandJson, "data");

                if (data == null)
                {
                    CorgiLog.Log(CorgiLogType.Error, "Invalid command1 : ({0})\n", commandStr);
                    return;
                }

                var command = CorgiEnum.ParseEnum<CommandType>(commandStr, true);
                if (command == CommandType.None)
                {
                    CorgiLog.Log(CorgiLogType.Error, "Invalid command2 :  ({0})\n", commandStr);
                    return;
                }

                if (_commandMap.ContainsKey(command) == false)
                {
                    CorgiLog.Log(CorgiLogType.Error, "Invalid command3 : ({0})\n", commandStr);
                    return;
                }

                var commandInst = _commandMap[command];
                if (commandInst == null)
                {
                    CorgiLog.Log(CorgiLogType.Error, "Can't find command on map : ({0})\n", commandStr);
                    return;
                }
                commandInst.Invoke(data);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
        
        void OnChattingMessage(RedisChannel channel,  RedisValue message)
        {
            //CorgiLog.Log(CorgiLogType.Info, "Channel Subscribe {0}, {1}", channel, message);
            try
            {
                var prefixStr = string.Format("server-chatting-channel-{0}-",
                    CombatServerConfig.Instance.EnvMode.ToString().ToLower());
                var channelKey = channel.ToString().Remove(0, prefixStr.Length);
                //var channelIndex = Convert.ToInt32(channelStr);
                var chattingMessage = ChattingMessage.DeserializeJson(message);
                
                ChattingManager.Instance.SerializeMethod("NtfChannelChatting", channelKey, chattingMessage);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        
        void Tick_Serialized()
        {
            if (_redis == null)
            {
                return;
            }
            
            // todo tick
            //StatDataManager.Instance.Increment(StatisticType.TickCount, 1);

            try
            {
                TickServerCommand();
            }
            catch (Exception e)
            {
                CorgiLog.Log(CorgiLogType.Fatal, "TickServerCommand occur exception[{0}]", e.ToString());
            }
        }

        void Log_Serialized(JObject jsonObject)
        {
            if (jsonObject == null)
            {
                return;
            }
            
            var db = _redis.GetDatabase(CombatServerConfigConst.REDIS_DB_INDEX);

            var logStr = jsonObject.ToString();
            var task = db.ListRightPushAsync(_logKey, logStr);
            Task.Run(async () =>
            {
                var ayncResult = await task;
                CorgiCombatLog.Log(CombatLogCategory.System, "Send Log to redis que[size : {0}] completed.", logStr.Length);
            });
        }

        async void TickServerCommand()
        {
            // Check command
            var db = _redis.GetDatabase(CombatServerConfigConst.REDIS_DB_INDEX);
            var asyncState = db.ListLeftPopAsync(_recvQueueKey);//-event방식인데 polling 방식으로 처리한다고?!... 흠...

            string result = await asyncState;

            if (string.IsNullOrEmpty(result))
            {
                return;
            }
            
            CorgiCombatLog.Log(CombatLogCategory.System, "Received command from web server via redis que");
            
            var commandJson = JObject.Parse(result);
            if (CorgiJson.IsValid(commandJson,"command") == false)
            {
                CorgiCombatLog.LogError(CombatLogCategory.System, "Unknown result[{0}] from web server command", result);
                return;
            }

            var commandStr = CorgiJson.ParseString(commandJson, "command");

            var command = CorgiEnum.ParseEnum<CommandType>(commandStr, false);
            if (command == CommandType.None)
            {
                CorgiLog.Log(CorgiLogType.Error, "Unknown command[{0}] from web server command", commandStr);
                return;
            }

            if (_commandMap.ContainsKey(command) == false)
            {
                CorgiLog.Log(CorgiLogType.Error, "Can't find command[{0}] on map", commandStr);
                return;
            }

            var commandInst = _commandMap[command];
            if (commandInst == null)
            {
                CorgiLog.LogError("Command inst is nulll ");
                return;
            }

            try
            {
                if (false == ValidCommandTime(commandJson, command))
                {
                    CorgiLog.Log(CorgiLogType.Fatal, "So, This command[{0}] will be skipped. bcuz so lazy", command.ToString()); 
                    return;
                }
            }
            catch (Exception e)
            {
                CorgiLog.Log(CorgiLogType.Error, "Occur exception when check command time. exception[{0}]", e.ToString());
            }
            
            commandInst.Invoke(commandJson);
        }

        // request room info
        public Task<RedisValue[]> RequestRoomCoordinateInfo(string roomId)
        {
            var db = _redis.GetDatabase(CombatServerConfigConst.REDIS_DB_INDEX);

            if (string.IsNullOrEmpty(roomId))
            {
                return null;
            }

            string roomKey = string.Format("rm-{0}", roomId);
            var roomState = db.SetMembersAsync(roomKey);
            
            return roomState;
        }
        
        public Task<RedisValue> RequestRoomInfo(string roomId)
        {
            var db = _redis.GetDatabase(CombatServerConfigConst.REDIS_DB_INDEX);
            
            if (string.IsNullOrEmpty(roomId))
            {
                return null;
            }
            
            var roomInfokey = string.Format("rs-{0}", roomId);
            var roomInfoState = db.StringGetAsync(roomInfokey);

            return roomInfoState;
        }
        
        public Task<RedisValue> RequestRoomStatus(string roomId)
        {
            var db = _redis.GetDatabase(CombatServerConfigConst.REDIS_DB_INDEX);
            
            if (string.IsNullOrEmpty(roomId))
            {
                return null;
            }
            
            var roomInfokey = string.Format("rf-{0}", roomId);
            var roomInfoState = db.StringGetAsync(roomInfokey);

            return roomInfoState;
        }
        
        public Task<RedisValue> RequestRiftInfo(string roomId)
        {
            var db = _redis.GetDatabase(CombatServerConfigConst.REDIS_DB_INDEX);
            
            if (string.IsNullOrEmpty(roomId))
            {
                return null;
            }
            
            var infoKey = string.Format("rift-{0}", roomId);
            var infoState = db.StringGetAsync(infoKey);

            return infoState;
        }

        // request character infos
        public List<Task<RedisValue>> RequestCharInfo(List<string> charIds)
        {
            var db = _redis.GetDatabase(CombatServerConfigConst.REDIS_DB_INDEX);

            if(charIds.Count <= 0)
            {
                return null;
            }

            List<Task<RedisValue>> retTasks = new List<Task<RedisValue>>();
            foreach (var charId in charIds)
            {
                string key = string.Format("usr-{0}", charId);
                var asyncState = db.StringGetAsync(key);
                retTasks.Add(asyncState);
            }

            return retTasks;
        }

        public Task<RedisValue> RequestCharInfo(string charId)
        {
            var db = _redis.GetDatabase(CombatServerConfigConst.REDIS_DB_INDEX);
            
            string key = string.Format("usr-{0}", charId);
            
            var asyncState = db.StringGetAsync(key);

            return asyncState;
        }
        
        public Task<RedisValue> RequestRoomDeckInfo(string roomId)
        {
            var db = _redis.GetDatabase(CombatServerConfigConst.REDIS_DB_INDEX);
            
            string key = string.Format("rd-{0}", roomId);
            
            var asyncState = db.StringGetAsync(key);

            return asyncState;
        }
        
        public Task<RedisValue> RequestPartyLogAll(string roomId)
        {
            var db = _redis.GetDatabase(CombatServerConfigConst.REDIS_DB_INDEX);
            
            string key = string.Format("rPartyLogAll-{0}", roomId);
            
            var asyncState = db.StringGetAsync(key);

            return asyncState;
        }

        public Task<RedisValue> RequestDungeonAuth(string dungeonKey)
        {
            var db = _redis.GetDatabase(CombatServerConfigConst.REDIS_DB_INDEX);
            
            string key = string.Format("{0}", dungeonKey);
            
            var asyncState = db.StringGetAsync(key);

            return asyncState;
            
        }
        
        public Task<RedisValue> RequestWorldBossCurHP(string dayNum)
        {
            var db = _redis.GetDatabase(CombatServerConfigConst.REDIS_DB_INDEX);
            
            string key = string.Format("worldboss-cur-{0}", dayNum);
            
            var asyncState = db.StringGetAsync(key);

            return asyncState;
        }
        
        public Task<RedisValue> RequestWorldBossMaxHP(string dayNum)
        {
            var db = _redis.GetDatabase(CombatServerConfigConst.REDIS_DB_INDEX);
            
            string key = string.Format("worldboss-max-{0}", dayNum);
            
            var asyncState = db.StringGetAsync(key);

            return asyncState;
        }

        public Task<long> RequestWorldBossDamage(string dayNum, long damage)
        {
            var db = _redis.GetDatabase(CombatServerConfigConst.REDIS_DB_INDEX);
            
            string key = string.Format("worldboss-cur-{0}", dayNum);

            var asyncState = db.StringDecrementAsync(key, damage);

            return asyncState;
        }

        public async void DeleteDungeonKey(string dungeonKey)
        {
            var db = _redis.GetDatabase(CombatServerConfigConst.REDIS_DB_INDEX);

            var result = await db.KeyDeleteAsync(dungeonKey);

            if (result == false)
            {
                CorgiCombatLog.LogError(CombatLogCategory.System, "Cannot remove dungeonkey[{0}] from redis", dungeonKey);
                return;
            }
            CorgiCombatLog.Log(CombatLogCategory.System, "successfully remove dungeonkey[{0}] from redis", dungeonKey);
        }
        
        // Send StageFinish CMD
        public void SendStageFinish(string roomId, ulong stageUid, bool stageResult, List<string> characterIds)
        {
            JObject newJson = new JObject();
            newJson.Add("roomId", new JValue(roomId));
            newJson.Add("serverIndex", new JValue(CombatServerConfig.Instance.ServerIndex));
            newJson.Add("stageUid", new JValue(stageUid));
            newJson.Add("stageResult", new JValue(stageResult));
            newJson.Add("characters", new JArray(characterIds));

            SendCmd(CommandType.StageFinish, newJson);
        }
        
        public void SendChallengeFinish(string roomId, string characterId, ulong stageUid, bool result)
        {
            JObject newJson = new JObject();
            newJson.Add("roomId", new JValue(roomId));
            newJson.Add("serverIndex", new JValue(CombatServerConfig.Instance.ServerIndex));
            newJson.Add("characterId", new JValue(characterId));
            newJson.Add("stageUid", new JValue(stageUid));
            newJson.Add("challengeResult", new JValue(result));

            SendCmd(CommandType.ChallengeFinish, newJson);
        }

        public void SendInstanceDungeonFinish(string roomId, string characterId, string dungeonId, ulong dungeonUid, ulong stageUid, bool result)
        {
            JObject newJson = new JObject();
            newJson.Add("roomId", new JValue(roomId));
            newJson.Add("serverIndex", new JValue(CombatServerConfig.Instance.ServerIndex));
            newJson.Add("characterId", new JValue(characterId));
            newJson.Add("dungeonId", new JValue(dungeonId));
            newJson.Add("dungeonUid", new JValue(dungeonUid));
            newJson.Add("stageUid", new JValue(stageUid));
            newJson.Add("challengeResult", new JValue(result));

            SendCmd(CommandType.InstanceDungeonFinish, newJson);
        }
        
        public void SendWorldBossFinish(string roomId, string characterId, string dungeonKey, long totalDamage, bool result)
        {
            JObject newJson = new JObject();
            newJson.Add("roomId", new JValue(roomId));
            newJson.Add("serverIndex", new JValue(CombatServerConfig.Instance.ServerIndex));
            newJson.Add("characterId", new JValue(characterId));
            newJson.Add("dungeonKey", new JValue(dungeonKey));
            newJson.Add("totalDamage", new JValue(totalDamage));
            newJson.Add("challengeResult", new JValue(result));

            SendCmd(CommandType.WorldBossFinish, newJson);
        }
        
        public void SendWorldBossDead(string roomId, string dayNum)
        {
            JObject newJson = new JObject();
            newJson.Add("roomId", new JValue(roomId));
            newJson.Add("serverIndex", new JValue(CombatServerConfig.Instance.ServerIndex));
            newJson.Add("dayNum", new JValue(dayNum));

            SendCmd(CommandType.WorldBossDead, newJson);
        }
        
        public void SendRiftFinish(string roomId, string dungeonId, string characterId, string dungeonKey, long totalDamage, bool result)
        {
            JObject newJson = new JObject();
            newJson.Add("roomId", new JValue(roomId));
            newJson.Add("serverIndex", new JValue(CombatServerConfig.Instance.ServerIndex));
            newJson.Add("dungeonId", new JValue(dungeonId));
            newJson.Add("characterId", new JValue(characterId));
            newJson.Add("dungeonKey", new JValue(dungeonKey));
            newJson.Add("totalDamage", new JValue(totalDamage));
            newJson.Add("challengeResult", new JValue(result));

            SendCmd(CommandType.RiftFinish, newJson);
        }
        
        public void SendRiftDead(string roomId, string dungeonId, string characterId)
        {
            JObject newJson = new JObject();
            newJson.Add("roomId", new JValue(roomId));
            newJson.Add("serverIndex", new JValue(CombatServerConfig.Instance.ServerIndex));
            newJson.Add("dungeonId", new JValue(dungeonId));
            newJson.Add("characterId", new JValue(characterId));

            SendCmd(CommandType.RiftDead, newJson);
        }
        
        public void SendArenaFinish(string roomId, string characterId, string targetId, string dungeonKey, string winnerId)
        {
            JObject newJson = new JObject();
            newJson.Add("roomId", new JValue(roomId));
            newJson.Add("serverIndex", new JValue(CombatServerConfig.Instance.ServerIndex));
            newJson.Add("characterId", new JValue(characterId));
            newJson.Add("targetCharacterId", new JValue(targetId));
            newJson.Add("dungeonKey", new JValue(dungeonKey));
            newJson.Add("winner", new JValue(winnerId));

            SendCmd(CommandType.PvpFinish, newJson);
        }

        void SendCmd(CommandType commandType, JObject json)
        {
            var db = _redis.GetDatabase(CombatServerConfigConst.REDIS_DB_INDEX);
            
            json.Add("command", new JValue(commandType.ToString()));
            json.Add("timestamp", new JValue(CorgiTime.UtcNowULong));

            CorgiCombatLog.Log(CombatLogCategory.System, "Send command[{0}] to web server via redis que", commandType.ToString());
            
            var task = db.ListRightPushAsync(_sendQueueKey, json.ToString());
            Task.Run(async () =>
            {
                var ayncResult = await task;
                CorgiCombatLog.Log(CombatLogCategory.System, "Send command[{0}] to web server via redis que[size : {1}] completed.", json.ToString(),
                    ayncResult);
            });
        }

        public void SendStatistic(JObject json)
        {
            var db = _redis.GetDatabase(CombatServerConfigConst.REDIS_DB_INDEX);
            
            var task = db.StringSetAsync(_serverStatisticKey, json.ToString());
            Task.Run(async () =>
            {
                await task;
            });
        }

        public void SendHackLog(HackType hackType, string roomId, string characterId, uint count = 1)
        {
            var db = _redis.GetDatabase(CombatServerConfigConst.REDIS_DB_INDEX);

            JObject json = new JObject();
            
            json.Add("timestamp", new JValue(CorgiTime.UtcNowULong));
            json.Add("hackType", new JValue((int) hackType));
            json.Add("roomId", new JValue(roomId));
            json.Add("characterId", new JValue(characterId));
            json.Add("count", new JValue(count));

            var task = db.ListRightPushAsync(_sendHackQueueKey, json.ToString());
            CorgiLog.LogLine("{0}",  json.ToString());
            Task.Run(async () =>
            {
                var ayncResult = await task;
                CorgiCombatLog.Log(CombatLogCategory.System, "Send command[{0}] to web server via redis que[size : {1}] completed.", json.ToString(),
                    ayncResult);
            });
        }
        
        public void SendRoomList(JArray json)
        {
            var db = _redis.GetDatabase(CombatServerConfigConst.REDIS_DB_INDEX);
            
            var task = db.StringSetAsync(_serverRoomListKey, json.ToString());
            Task.Run(async () =>
            {
                await task;
            });

            CorgiLog.Log(CorgiLogType.Info, json.ToString());
        }
        
        

        // send adventure dungeon reward
        public void SendRewardAdventure(List<string> charIds)
        {
        }
        
        public string GetValue(string key)
        {
            // var redisDB = _redis.GetDatabase();
            //
            // var retValue = redisDB.StringGet(key);
            //
            
            // return retValue;
            return null;
        }

        public string GetRevisionInfo()
        {
            var db = _redis.GetDatabase(CombatServerConfigConst.REDIS_DB_INDEX);
            var revInfoString = db.StringGet("revisionInfo");
            return revInfoString.IsNull ? null : revInfoString.ToString();
        }
        
        // save party logs & partyChatting
        public void SavePartyLogAll(string roomId, PartyLog partyLog, ChattingChannel partyChatting)
        {
            var db = _redis.GetDatabase(CombatServerConfigConst.REDIS_DB_INDEX);
            
            string key = string.Format("rPartyLogAll-{0}", roomId);
            
            var writer = new StringPacketWriter();
            
            partyLog.Serialize(writer);
            
            partyChatting.Serialize(writer);

            db.StringSetAsync(key, writer.PullOut());


        }

        public void SaveWorldBossDeadTime(string dayNum, ulong deadTime)
        {
            var db = _redis.GetDatabase(CombatServerConfigConst.REDIS_DB_INDEX);

            string key = $"worldboss-{dayNum}-complete";
            
            db.StringSetAsync(key, deadTime.ToString());
        }
        
        public void SaveRiftInfo(string roomId, SharedRift rift)
        {
            var db = _redis.GetDatabase(CombatServerConfigConst.REDIS_DB_INDEX);

            string key = string.Format("rift-{0}", roomId);

            var json = rift.ToJson();

            db.StringSetAsync(key, json.ToString());
        }

        public void SendChattingMessage(string channel, ChattingMessage message)
        {
            // if (channel < 0 || channel >= CombatServerConfigConst.CHATTING_CHANNEL_COUNT)
            // {
            //     CorgiLog.Log(CorgiLogType.Warn, "[Chatting] Invalid Chatting Channel {0}", channel);
            //     return;
            // }
            var sub = _redis.GetSubscriber();
            var chattingChannelName = string.Format("server-chatting-channel-{0}-{1}",
                 CombatServerConfig.Instance.EnvMode.ToString().ToLower() , channel);
            sub.Publish(chattingChannelName, message.SerializeJson());
        }
        
    }
}