using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using Google.Protobuf;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

using Corgi.DBSchema;
using Corgi.GameData;

using IdleCs.CombatServer;
using IdleCs.CombatServer.ServerRedis;
using IdleCs.GameLog;
using IdleCs.GameLogic;
using IdleCs.GameLogic.SharedInstance;
using IdleCs.Managers;
using IdleCs.Network;
using IdleCs.Network.NetLib;
using IdleCs.ServerCore;
using IdleCs.Utils;

using IdleCs.Logger;
using IdleCs.ServerUtils;
//using UnityEngine.Assertions.Must;

namespace IdleCs.ServerContents
{
    public partial class Room : CorgiServerObject, ICombatBridge
    {
        public enum  RoomDestroyReason
        {
            None = 0,
            TimeOut = 1,
            Exception = 2,
            CantCreateRoom = 3,
            CantGetPartyMember = 4,
            ServerGetProblem = 5,
            NoWebServerResponse = 6,
            RoomKillCommand = 7
        }
        
        /// <summary>
        /// static & redis data
        /// </summary>
        private string _roomId;

        private Party _party = new Party();

        /// <summary>
        /// dynamic data
        /// </summary>
        ///
        //private RedisRequest _redisRequest;

        private RoomState _roomState = RoomState.Created;
        private volatile bool _isLoadRoomInfos = false;
        private volatile RoomDestroyReason _destroyReason = RoomDestroyReason.None; 
        
        private CorgiTimer _destroyTimer = new CorgiTimer();
        
        private List<CorgiServerConnection> _connectionList = new List<CorgiServerConnection>();

        private List<LogNode> _cachedLogNodes = new List<LogNode>();
        
        // for redis request
        private volatile int _isRequestingCount;

        // party list
        private List<string> _charIds = new List<string>();// room 최초 생성시 세팅되는 캐릭터(유저)들. 중간에 파티조인 하는 캐릭터는 업데이트 안됨.
        //private List<Unit> _partyList;
        
        // party status
        private PartyStatus _partyStatus = new PartyStatus();
        
        // party log
        private PartyLog _partyLog = new PartyLog();
        private ChattingChannel _partyChatting = new ChattingChannel();

        // adventure dungeon 
        private DungeonAdventure _adventureDungeon;
        
        // <user id, instance dungeon> map
        // chapter&bastion&labyrinth dungeon
        private Dictionary<string, DungeonInstance> _instanceDungeonMap = new Dictionary<string, DungeonInstance>();
        // world boss
        private Dictionary<string, DungeonWorldBoss> _worldBossDungeonMap = new Dictionary<string, DungeonWorldBoss>();
        // arena
        private Dictionary<string, DungeonArena> _arenaDungeonMap = new Dictionary<string, DungeonArena>();
        // rift
        private Rift _rift;
        
        // auto hunting buff timestamp
        private ulong _buffEndTimestamp = 0;

        public string RoomId => _roomId;
        
        private ulong _stageCompletedCount = 0;

        public ulong StageCompletedCount => _stageCompletedCount;

        public new string Id()
        {
            return _roomId;
        }

        public RoomState RoomState => _roomState;
        
        // Write by Room / Read by RoomManager
        public ulong LastTickTimestamp;
        
        public int ConnectionCount {get{
            if (_connectionList != null)
            {
                return _connectionList.Count;
            }
            else
            {
                return 0;
            }
        }}

        public Room(string roomId)
        {
            _roomId = roomId;
            var channelKey = $"channel_party_{roomId}";
            _partyChatting.Initialize(ChattingType.Party, channelKey, 0, CombatServerConfigConst.CHATTING_MESSAGE_MAX_COUNT);
            LastTickTimestamp = CorgiTime.UtcNowULong;
        }

        public void DoDestroy(int reason)
        {
            DoDestroy();
            _destroyReason = (RoomDestroyReason)reason;
            CorgiLog.Log(CorgiLogType.Warning, "Room will be destroy. id[{0}], reason[{1}], Count[{2}]", _roomId, _destroyReason.ToString(), _stageCompletedCount);
        }

        public bool HaveToDestroy()
        {
            return (RoomDestroyReason.None != _destroyReason);
        }
        
        public void Destroy()
        {
            RedisManager.Instance.SerializeMethod("RoomDeleted", RoomId);
            
            foreach (var curConn in _connectionList)
            {
                if (null != curConn)
                {
                    curConn.Disconnect();
                }
            }
            _connectionList.Clear();
        }

        CorgiServerConnection GetConnection(string characterId)
        {
            foreach (var conn in _connectionList)
            {
                if (string.IsNullOrEmpty(conn.CharacterId))
                {
                    continue;
                }

                if (conn.CharacterId == characterId)
                {
                    return conn;
                }
            }

            return null;
        }
        
        void Tick_Serialized()
        {
            LastTickTimestamp = CorgiTime.UtcNowULong;
            
            if (_isLoadRoomInfos == false)
            {
                return;
            }
            //StatDataManager.Instance.Increment(StatisticType.TickCount, 1);

            try
            {
                _destroyTimer.Tick();
                if (_destroyTimer.IsOver())
                {
                    DoDestroy((int)RoomDestroyReason.TimeOut);
                    return;
                }

                TickAdventureDungeon();
                TickInstanceDungeon();
                TickWorldBossDungeon();
                TickRiftDungeon();
                TickArenaDungeon();

                // safe code
                var remainConnectionCount = _connectionList.Count;

                if (0 == remainConnectionCount && _destroyTimer.IsActive == false && (_adventureDungeon == null || _adventureDungeon.IsFailed == false))
                {
                    OnDestroyCallback();
                    StartDestoryTimer(CombatServerConfigConst.EMPTY_ROOM_ALIVE_TIME_MS);
                    LogHelper.LogRoom(LogType.RoomNoConnenction, _roomId, String.Empty, String.Empty, "Invalid Room Status No Connection");
                }
                else if(_destroyTimer.IsActive == false)
                {
                    var isValid = false;
                    foreach (var conn in _connectionList)
                    {
                        if(conn == null)
                        {
                            continue;
                        }

                        if (conn.IsConnected() == false )
                        {
                            continue;
                        }

                        isValid = true;
                    }

                    if (isValid == false && (_adventureDungeon == null || _adventureDungeon.IsFailed == false))
                    {
                        OnDestroyCallback();
                        StartDestoryTimer(CombatServerConfigConst.EMPTY_ROOM_ALIVE_TIME_MS);
                        LogHelper.LogRoom(LogType.RoomConnectionInvalid, _roomId, String.Empty, String.Empty, "Invalid Room Status Connection Invalid");
                    }
                }
                    
            }
            catch (Exception e)
            {
                LogHelper.LogException(e, "Occur exception when room tick");
                CorgiCombatLog.LogFatal(CombatLogCategory.User,"occur exception when room tick({0})\n{1}", RoomId, e);
                DoDestroy((int)RoomDestroyReason.Exception);//-예외시 방삭제 하지 말고, client 의 disconn 에 맡긴다.                    
            }
        }
        
        void TickAdventureDungeon()
        {
            if (_adventureDungeon == null)
            {
                return;
            }
#if COMBAT_SERVER_DEBUG
			var stopWatch = new Stopwatch();
			stopWatch.Start();
#endif
            var logNodeList = _adventureDungeon.UpdateState();
            
#if COMBAT_SERVER_DEBUG
			stopWatch.Stop();
            
            CorgiLog.LogLine("TickLog {0}/{1}/{2}", logNode.DungeonState.ToString(), _adventureDungeon.OriginalState.IsUpdated(),stopWatch.Elapsed);
#endif
            
            if ((logNodeList != null && logNodeList.Count > 0) || _adventureDungeon.OriginalState.IsUpdated())
            {
#if COMBAT_SERVER_DEBUG
                CorgiLog.LogLine("Broadcasintg logs {0}:{1}", _adventureDungeon.OriginalState.Value.ToString(),stopWatch.Elapsed);
#endif
                
                // broadcasting log
                foreach (var conn in _connectionList)
                {
                    if (conn.UserState != UserState.Active)
                    {
                        continue;
                    }

                    conn.SC_ADVENTURE_COMBATLOG(_roomId, logNodeList);
                }
                
                NtfUpdateUnit();
            }

            //DoCacheLogNode(logNode);
        }

        void TickInstanceDungeon()
        {
            var removedList = new List<string>();
            foreach (var dungeon in _instanceDungeonMap.Values)
            {
                if (dungeon == null)
                {
                    continue;
                }

                if (dungeon.State == DungeonState.Destroy)
                {
                    removedList.Add(dungeon.CharacterId);
                    continue;
                }

                var logNodeList = dungeon.UpdateState();
                
                if ((logNodeList != null && logNodeList.Count>0) || dungeon.OriginalState.IsUpdated())
                {
                    var conn = GetConnection(dungeon.CharacterId);
                    if (conn != null)
                    {
                        conn.SC_INSTANCE_COMBATLOG(_roomId, dungeon.Uid, 0, logNodeList);
                    }
                }
            }

            foreach (var characterId in removedList)
            {
                _instanceDungeonMap.Remove(characterId);
                CorgiCombatLog.Log(CombatLogCategory.Dungeon, "Remove InstanceDungeon for {0}", characterId);
            }
        }
        
        void TickWorldBossDungeon()
        {
            var removedList = new List<string>();
            foreach (var dungeon in _worldBossDungeonMap.Values)
            {
                if (dungeon == null)
                {
                    continue;
                }

                if (dungeon.State == DungeonState.Destroy)
                {
                    removedList.Add(dungeon.CharacterId);
                    continue;
                }

                var logNodeList = dungeon.UpdateState();
                
                if ((logNodeList != null && logNodeList.Count>0) || dungeon.OriginalState.IsUpdated())
                {
                    var conn = GetConnection(dungeon.CharacterId);
                    if (conn != null)
                    {
                        conn.SC_WORLD_BOSS_COMBATLOG(_roomId, dungeon.DungeonKey, logNodeList);
                    }
                }
            }

            foreach (var characterId in removedList)
            {
                _worldBossDungeonMap.Remove(characterId);
                CorgiCombatLog.Log(CombatLogCategory.Dungeon, "Remove WorldBossDungeon for {0}", characterId);
            }
        }
        
        void TickRiftDungeon()
        {
            if (_rift == null)
            {
                return;
            }

            if (_rift.IsOver)
            {
                // Destroy
                _rift = null;
                return;
            }

            // 1. process dungeon tick 
            var isUpdatedRift = false;
            foreach (var conn in _connectionList)
            {
                if (conn == null)
                {
                    continue;
                }

                var dungeonKey = _rift.GetDungeonKey(conn.CharacterId);

                if (dungeonKey == null)
                {
                    continue;
                }
                    
                var logNodeList = _rift.UpdateState(conn.CharacterId);
                
                if (logNodeList != null)
                {
                    conn.SC_RIFT_COMBATLOG(_roomId, dungeonKey, logNodeList);
                    isUpdatedRift = true;
                }
            }
            
            _rift.ClearPartyMemberEnterQueue();

            if (isUpdatedRift == false)
            {
                return;
            }
            
            // 2. update shared dungeon
            var shared = _rift.UpdateRiftInfo();
            foreach (var conn in _connectionList)
            {
                if (conn == null)
                {
                    continue;
                }
                if (shared != null)
                {
                    // share RiftInfo
                    conn.SC_UPDATE_RIFT_INFO(shared);
                }
            }
            
            // 3. update rift hp
            _rift.OnUpdateRiftInfo();
        }

        void TickArenaDungeon()
        {
            var removedList = new List<string>();
            foreach (var dungeon in _arenaDungeonMap.Values)
            {
                if (dungeon == null)
                {
                    continue;
                }

                if (dungeon.State == DungeonState.Destroy)
                {
                    removedList.Add(dungeon.CharacterId);
                    continue;
                }

                var logNodeList = dungeon.UpdateState();
                
                if ((logNodeList != null && logNodeList.Count>0) || dungeon.OriginalState.IsUpdated())
                {
                    var conn = GetConnection(dungeon.CharacterId);
                    if (conn != null)
                    {
                        conn.SC_ARENA_COMBATLOG(_roomId, string.Empty, logNodeList);
                    }
                }
            }

            foreach (var characterId in removedList)
            {
                _arenaDungeonMap.Remove(characterId);
                CorgiCombatLog.Log(CombatLogCategory.Dungeon, "Remove ArenaDungeon for {0}", characterId);
            }
        }
        
        void JoinAdventure_Serialized(CorgiServerConnection serverConnection, string roomId)
        {
            //-JoinAdventure_Serialized 의 인자를 둘로 나눌 필요가 없음. 쓸데 없는 처리. 어차피 connection 이 roomid 정보 가지고 있음.  
            
            // check duplicate user
            foreach (var conn in _connectionList)
            {
                if (conn == null)
                {
                    continue;
                }

                if (serverConnection.CharacterId == conn.CharacterId)
                {
                    conn.Disconnect();
                    _connectionList.Remove(conn);
                    break;
                }
            }
            
            if (_connectionList.Count >= 4)
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"can't join room. pary user too many", serverConnection.CharacterId, roomId, true);
                
                serverConnection.SC_JOIN_ADVENTURE(
                    CorgiErrorCode.TooManyJoinUser, roomId, serverConnection.CharacterId, null, null, null, null);
                return;
            }
            
            if (_connectionList.Contains(serverConnection))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"duplicated connection", serverConnection.CharacterId, roomId, true);
                
                serverConnection.SC_JOIN_ADVENTURE(CorgiErrorCode.DuplicatedConnection
                    , roomId, serverConnection.CharacterId, null, null, null, null);
                return;
            }
            
            serverConnection.UserState = UserState.Joined;
            _connectionList.Add(serverConnection);
            _destroyTimer.StopTimer();

            switch (_roomState)
            {
                case RoomState.Created:
                    JoinToCreatedRoom(serverConnection, roomId);
                    break;
                case RoomState.Loading:
                    JoinToLoadingRoom(serverConnection, roomId);    
                    break;
                case RoomState.Running:
                case RoomState.NoConnectionHunting:
                    JoinToRunningRoom(serverConnection, roomId, _roomState);

                    if (RoomState.NoConnectionHunting == _roomState)
                    {
                        _roomState = RoomState.Running;//-if don't change state then will be closed session when win while user is connected.
                    }
                    break;
                /*
                case RoomState.NoConnectionHunting:
                    JoinToNoConnectionHuntingRoom(serverConnection, roomId);
                    break;*/
                default:
                    CorgiLog.Log(CorgiLogType.Error, "Error, character[{0}] wanna join unknown state[{1}] room[{2}]", 
                        serverConnection.CharacterId, _roomState.ToString(), serverConnection.RoomId);
                    break;
            }   
        }

        private void JoinToCreatedRoom(CorgiServerConnection serverConnection, string roomId)
        {
            CorgiCombatLog.Log(CombatLogCategory.User,"Room state is created join adventure", serverConnection.CharacterId, roomId);
                
            var requestParam = new RequestParam(RedisRequestType.RoomCoordinateInfo, _roomId);
            if (RequestData(serverConnection.CharacterId, requestParam, "OnLoadRoomCoordinateInfo") == false)
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"RedisRequestType.RoomCoordinateInfo failed", serverConnection.CharacterId, roomId, true);
                throw new CorgiException("can't request to load data");
            }
                
            CorgiCombatLog.Log(CombatLogCategory.User,"RedisRequestType.RoomCoordinateInfo completed", serverConnection.CharacterId, roomId);
        }

        private void JoinToLoadingRoom(CorgiServerConnection serverConnection, string roomId)
        {
            // do nothing. waiting.
            CorgiCombatLog.Log(CombatLogCategory.User,"Room state is loading join adventure", serverConnection.CharacterId, roomId);
        }
        
        private void JoinToRunningRoom(CorgiServerConnection serverConnection, string roomId, RoomState roomState)
        {
            CorgiCombatLog.Log(CombatLogCategory.User,$"Room state is [{roomState}] join adventure", serverConnection.CharacterId, roomId);
                
            var dungeonInfo = new SharedDungeon();
            dungeonInfo.Init(_adventureDungeon);
                
            CorgiCombatLog.Log(CombatLogCategory.User,"send SC_JOIN_ADVENTURE success, created room already", serverConnection.CharacterId, serverConnection.RoomId);
                
            var memberInfo = _party.GetMemberInfo(serverConnection.CharacterId);
            if (memberInfo == null)
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"[party] Critical. can't find character at room party member", serverConnection.CharacterId, roomId, true);
                //-just can't make what was connected log info.
                //throw new CorgiException("invalid member info CharacterId {0}", serverConnection.CharacterId);
                return;
            }

            OnConnected(serverConnection, memberInfo);
            
            var clonedPartyChatting = _partyChatting.GetPartyChatting(memberInfo.joinTimestamp);
            
            serverConnection.SC_JOIN_ADVENTURE(
                CorgiErrorCode.Success, serverConnection.CharacterId, roomId
                , _partyStatus, _partyLog, clonedPartyChatting, dungeonInfo);
            
        }

        private void JoinToNoConnectionHuntingRoom(CorgiServerConnection serverConnection, string roomId)
        {   
        }
        
        void OnUpdatePartyLog()
        {
            var curPartyLog = _partyLog.GetCurLog();
            
            foreach (var conn in _connectionList)
            {
                if (conn.UserState != UserState.Active)
                {
                    continue;
                }

                conn.SC_UPDATE_PARTY_LOG(_roomId, curPartyLog);
            }
            //CorgiLog.LogLine("update party log : {0}", curPartyLog);
            
        }
        
        
        void OnLoadRoomCoordinateInfo_Serialized(RedisRequest redisRequest)
        {
            OnLoadRequestData(redisRequest);
            
            var paramList = new List<RequestParam>();

            // load char infos
            foreach (var charId in _charIds)
            {
                if (string.IsNullOrEmpty(charId))
                {
                    continue;
                }
                paramList.Add(new RequestParam(RedisRequestType.CharaterInfo, charId));
            }
            
            paramList.Add(new RequestParam(RedisRequestType.RoomInfo, _roomId));
            paramList.Add(new RequestParam(RedisRequestType.RoomStatus, _roomId));
            paramList.Add(new RequestParam(RedisRequestType.RoomDeckInfo, _roomId));
            paramList.Add(new RequestParam(RedisRequestType.PartyLogAll, _roomId));
            paramList.Add(new RequestParam(RedisRequestType.GetRiftInfo, _roomId));
            
            //_redisRequest = newRequest;
            RequestData(redisRequest.CharacterId, paramList, "OnLoadRoomInfos");
            
            _roomState = RoomState.Loading;
        }

        void OnLoadRoomInfos_Serialized(RedisRequest redisRequest)
        {
            //-call when login only  
            

            OnLoadRequestData(redisRequest);

            if (_adventureDungeon == null)
            {
                _adventureDungeon = new DungeonAdventure(this);
            }
            
		    if (_adventureDungeon.Load(_party.DungeonUid) == false)
		    {
			    CorgiLog.LogError("cant create dungeon : {0}\n", GameDataManager.Instance.GetStrByUid(_party.DungeonUid));
                CorgiCombatLog.Log(CombatLogCategory.User,"can't create dungeon", "none", "none", true);//@simjinsub redis requset에서 요청한 사람과 요청한 시간의 정보를 넣어야 함.
                         
                DoDestroy((int)RoomDestroyReason.CantCreateRoom);
                return;
            }
            _adventureDungeon.UpdateAutoHuntingBuff(_buffEndTimestamp);
            

            if (_roomState != RoomState.Running)
            {
                var finalList = CreateUnitList(_adventureDungeon);
                
                // enter dungeon for start
                DungeonLogNode logNode = _adventureDungeon.EnterDungeon(finalList, _party.StageUid);
                
                var dungeonInfo = new SharedDungeon();
                dungeonInfo.Init(_adventureDungeon);

                // join broadcasting
                foreach (var conn in _connectionList)
                {
                    if (conn.UserState == UserState.Active)
                    {
                        continue;
                    }
                    
                    // connect log
                    var memberInfo = _party.GetMemberInfo(conn.CharacterId);
                    if (memberInfo == null)
                    {
                        DoDestroy((int)RoomDestroyReason.CantGetPartyMember);
                        throw new CorgiException("invalid member info CharacterId {0}", conn.CharacterId);
                    }
                    
                    OnConnected(conn, memberInfo);
                    
                    var clonedPartyChatting = _partyChatting.GetPartyChatting(memberInfo.joinTimestamp);
                    
                    conn.SC_JOIN_ADVENTURE(CorgiErrorCode.Success
                        , conn.CharacterId, _roomId, _partyStatus, _partyLog, clonedPartyChatting, dungeonInfo);
                    
                    CorgiCombatLog.Log(CombatLogCategory.User,"send SC_JOIN_ADVENTURE success, create room.", conn.CharacterId, conn.RoomId);
                }
                
                _roomState = RoomState.Running;
            }
            
            _isLoadRoomInfos = true;
        }

        void OnStageCompleted_Serialized(ulong stageUid)
        {
            _adventureDungeon.OnStageCompleted(stageUid);
            
            // check stage uid for party log
            var chapterChange = _adventureDungeon.IsChapterChanged(stageUid);

            if (chapterChange > 0)
            {
                var partyLog = new PartyLogParty(PartyLogType.PartyChapterRise, stageUid);
                _partyLog.AddLog(partyLog);
                OnUpdatePartyLog();

            }else if (chapterChange < 0)
            {
                var partyLog = new PartyLogParty(PartyLogType.PartyChapterDrop, stageUid);
                _partyLog.AddLog(partyLog);
                OnUpdatePartyLog();
            }

            if (_adventureDungeon.IsStageChanged(stageUid) < 0)
            {
                var partyLog = new PartyLogBattle(PartyLogType.BattleStageLose, string.Empty, stageUid);
                _partyLog.AddLog(partyLog);
                OnUpdatePartyLog();
            }
            
            StatDataManager.Instance.Increment(StatisticType.StageCompleteCount, 1);
            _stageCompletedCount++;
        }
        
        void OnChallengeStart_Serialized(string characterId, ulong stageUid)
        {
            // not changed update
            // check only deck 
            if (_adventureDungeon != null)
            {
                _adventureDungeon.OnChallengeStarted(characterId, stageUid);
            }
        }
        
        void OnChallengeCompleted_Serialized(string characterId, ulong stageUid, bool challengeResult)
        {
            if (challengeResult)
            {
                var memberInfo = _party.GetMemberInfo(characterId);
                if (memberInfo != null)
                {
                    var partyLog = new PartyLogBattle(PartyLogType.BattleChallenge, memberInfo.character.nickname, stageUid);
                    _partyLog.AddLog(partyLog);
                    OnUpdatePartyLog();
                }
            }
            
            _adventureDungeon.OnChallengeCompleted(stageUid);
            
            // check stage uid for party log
            var chapterChange = _adventureDungeon.IsChapterChanged(stageUid);

            if (chapterChange > 0)
            {
                var partyLog = new PartyLogParty(PartyLogType.PartyChapterRise, stageUid);
                _partyLog.AddLog(partyLog);
                OnUpdatePartyLog();

            }else if (chapterChange < 0)
            {
                var partyLog = new PartyLogParty(PartyLogType.PartyChapterDrop, stageUid);
                _partyLog.AddLog(partyLog);
                OnUpdatePartyLog();
            }
            
            StatDataManager.Instance.Increment(StatisticType.ChallengeCompleteCount, 1);
            
        }

        void OnAutoHuntingStart_Serialized(string characterId, ulong stageUid, bool serialBoss, ulong buffEndTimestamp)
        {
            _buffEndTimestamp = buffEndTimestamp;
            _adventureDungeon.OnAutoHuntingStart(characterId, stageUid, serialBoss, buffEndTimestamp);
            
            var memberInfo = _party.GetMemberInfo(characterId);
            if (memberInfo == null)
            {
                return;
            }
            // broadcasting autohunting
            foreach (var curConn in _connectionList)
            {
                if (curConn == null)
                {
                    continue;
                }
                
                curConn.SC_AUTO_HUNTING_START(_roomId, characterId, _adventureDungeon.GetChallengeCount(), _buffEndTimestamp);
            }
            
            // update party log
            var partyLog = new PartyLogBattle(PartyLogType.BattleAutoHunting, memberInfo.character.nickname, 0);
            _partyLog.AddLog(partyLog);
            OnUpdatePartyLog();
            
        }

        
        void JoinInstance_Serialized(JObject json)
        {
            if (CorgiJson.IsValidString("characterId") == false)
            {
                throw new CorgiException("instance dungeon start error");
            }

            var characterId = CorgiJson.ParseString(json, "characterId");
            
            if (_instanceDungeonMap.ContainsKey(characterId))
            {
                var oldDungeon= _instanceDungeonMap[characterId];
                if (oldDungeon != null && oldDungeon.State != DungeonState.Destroy)
                {
                    _instanceDungeonMap.Remove(characterId);
                }
                else
                {
                    throw new CorgiException("already started instance dungeon {0}", characterId);
                    
                }
                
            }
            
            //create dungeon
            var dungeon = new DungeonInstance(this);

            if (dungeon.Load(json) == false)
            {
                throw new CorgiException("invalid dungeon uid for start ID {0}, {1}", characterId, json.ToString());
            }

            _instanceDungeonMap.Add(characterId, dungeon);
            
            // create unit list
            var finalList = CreateUnitList(dungeon);
            
            dungeon.UpdateAutoHuntingBuff(_buffEndTimestamp);
            
            // enter dungeon for start
            dungeon.EnterDungeon(finalList, dungeon.CurStageUid);
            
            var dungeonInfo = new SharedDungeon();
            dungeonInfo.Init(dungeon);
            
            var conn = GetConnection(characterId);
            
            if (conn != null)
            {
                conn.SC_JOIN_INSTANCE(CorgiErrorCode.Success, _roomId, dungeon.CharacterId, dungeonInfo);
            }
        }
        
        void OnInstanceDungeonCompleted_Serialized(string characterId, string dungeonId, ulong dungeonUid, ulong stageUid)
        {
            if (_instanceDungeonMap.ContainsKey(characterId) == false)
            {
                //throw new CorgiException("invalid dungeon instance dungeon {0}/{1}/{2}", characterId, dungeonUid, stageUid);
                return;
            }
            
            var dungeon = _instanceDungeonMap[characterId];
            if (dungeonId != dungeon.DBId)
            {
                CorgiLog.Log(CorgiLogType.Warning, "[Dungeon] already disappeared dungeon {0}", dungeonId);
                return;
            }
            
            dungeon.OnInstanceDungeonCompleted(stageUid);
            
            //_instanceDungeonMap.Remove(characterId);
        }

        void OnInstanceDungeonStop_Serialized(string characterId, string dungeonId, ulong dungeonUid, ulong stageUid)
        {
            if (_instanceDungeonMap.ContainsKey(characterId) == false)
            {
                //throw new CorgiException("invalid dungeon instance dungeon {0}/{1}/{2}", characterId, dungeonUid, stageUid);
                // already end
                return;
            }
            
            var dungeon = _instanceDungeonMap[characterId];

            if (dungeonId.Equals(dungeon.DBId) == false)
            {
                CorgiLog.Log(CorgiLogType.Warning, "[Dungeon] already disappeared dungeon {0}", dungeonId);
                return;
            }
            
            dungeon.OnInstanceDungeonStop();

            _instanceDungeonMap.Remove(characterId);
        }
        
        void JoinWorldBoss_Serialized(string characterId, string dungeonKey)
        {
            var curConn = GetConnection(characterId);
            if (curConn == null)
            {
                return;
            }
            if (dungeonKey.Split('-').Length != 4)
            {
                curConn.SC_JOIN_WORLD_BOSS(CorgiErrorCode.WorldBossInvalidDungeonKey, _roomId, characterId, dungeonKey, null);
                
                return;
            }
            if (_worldBossDungeonMap.ContainsKey(characterId))
            {
                var oldDungeon= _worldBossDungeonMap[characterId];
                if (oldDungeon != null && oldDungeon.State != DungeonState.Destroy)
                {
                    var dungeonInfo = new SharedDungeon();
                    dungeonInfo.Init(oldDungeon);
                    curConn.SC_JOIN_WORLD_BOSS(CorgiErrorCode.DuplicatedDungeon, _roomId, characterId, dungeonKey, dungeonInfo);
                    return;
                }
                _worldBossDungeonMap.Remove(characterId);
            }
            
            //create dungeon
            var dungeon = new DungeonWorldBoss(this);

            var uidStr = "instance_dungeon.world.worldboss";
            var uid = GameDataManager.GetUidByString(uidStr);

            if (dungeon.Load(uid) == false)
            {
                curConn.SC_JOIN_WORLD_BOSS(CorgiErrorCode.DungeonLoadFailed, _roomId, characterId, dungeonKey, null);
                return;
            }

            dungeon.DungeonKey = dungeonKey;
            dungeon.CharacterId = characterId;

            _worldBossDungeonMap.Add(characterId, dungeon);
            
            // todo request data
            var paramList = new List<RequestParam>();
            paramList.Add(new RequestParam(RedisRequestType.DungeonAuth, dungeonKey));
            paramList.Add(new RequestParam(RedisRequestType.WorldBossCurHP, dungeonKey));
            paramList.Add(new RequestParam(RedisRequestType.WorldBossMaxHP, dungeonKey));

            if (RequestData(characterId, paramList, "OnLoadWorldBossData") == false)
            {
                curConn.SC_JOIN_WORLD_BOSS(CorgiErrorCode.WorldBossHaveRedisProblem, _roomId, characterId, dungeonKey, null);
            }
        }
        
        void JoinRift_Serialized(string characterId, string dungeonKey)
        {
            var curConn = GetConnection(characterId);
            if (curConn == null)
            {
                return;
            }
            var paramList = new List<RequestParam>();
            paramList.Add(new RequestParam(RedisRequestType.DungeonAuth, dungeonKey));

            // first try
            
            if (_rift == null)
            {
                _rift = new Rift();
                
                // new rift 인경우만
                paramList.Add(new RequestParam(RedisRequestType.GetRiftInfo, _roomId));
            }
            
            if (_rift.JoinRift(characterId, dungeonKey, this) == false)
            {
                curConn.SC_JOIN_RIFT(CorgiErrorCode.RiftInitError, _roomId, characterId, dungeonKey, null);
                return;
            }

            if (RequestData(characterId, paramList, "OnLoadRiftData") == false)
            {
                curConn.SC_JOIN_RIFT(CorgiErrorCode.RiftHaveRedisProblem, _roomId, characterId, dungeonKey, null);
                return;
            }
        }

        // rift redis data 처리
        void OnLoadRiftData_Serialized(RedisRequest redisRequest)
        {
            var characterId = redisRequest.CharacterId;
            
            var curConn = GetConnection(characterId);
            if (curConn == null)
            {
                return;
            }
            
            if (_rift == null)
            {
                curConn.SC_JOIN_RIFT(CorgiErrorCode.RiftHaveRedisProblem, _roomId, characterId, string.Empty, null);
                return;
            }

            if (_rift.IsOver)
            {
                curConn.SC_JOIN_RIFT(CorgiErrorCode.RiftIsOver, _roomId, characterId, string.Empty, null);
            }
            
            var taskList = redisRequest.GetRedisTasks();
            var canTry = true;
            string dungeonKey = String.Empty;
            SharedRift riftInfo = null;
            var errorCode = CorgiErrorCode.Success;

            try
            {
                foreach (var curTask in taskList)
                {
                    switch (curTask.RequestType)
                    {
                        case RedisRequestType.DungeonAuth:
                        {
                            var thisTask = curTask as RedisTaskDungeonAuth;
                            if (thisTask == null)
                            {
                                errorCode = CorgiErrorCode.RiftInvalidDungeonKey;
                                break;
                            }

                            dungeonKey = thisTask.DungeonKey;
                            canTry = thisTask.CanTry;
                            break;
                        }
                        case RedisRequestType.GetRiftInfo:
                        {
                            var thisTask = curTask as RedisTaskGetRiftInfo;
                            if (thisTask == null)
                            {
                                errorCode = CorgiErrorCode.RiftHaveRedisProblem;
                                break;
                            }

                            riftInfo = thisTask.SharedRift;

                            break;
                        }
                    }

                    if (canTry == false || errorCode != CorgiErrorCode.Success)
                    {
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                CorgiLog.Log(CorgiLogType.Fatal, "Occur exception[{0}] in OnLoadWorldBossData", e.ToString());

                //-don't call return here, "_isRequestingCount--" have to call. as below 
                curConn.SC_JOIN_RIFT(CorgiErrorCode.RiftHaveRedisProblem , _roomId, characterId, string.Empty, null);
                return;
            }
            finally
            {
                _isRequestingCount--;//-have to call
            }
            
            if (canTry == false || errorCode != CorgiErrorCode.Success || string.IsNullOrEmpty(dungeonKey))
            {
                curConn.SC_JOIN_RIFT(CorgiErrorCode.RiftError, _roomId, characterId, string.Empty, null);
                return;
            }

            var retLog = _rift.OnJoinRift(characterId, riftInfo, this);
            
            if (retLog == null)
            {
                curConn.SC_JOIN_RIFT(CorgiErrorCode.RiftInitError, _roomId, characterId, dungeonKey, null);
                return;
            }
            
            curConn.SC_JOIN_RIFT(CorgiErrorCode.Success, _roomId, characterId, dungeonKey, retLog.SharedInstance as SharedDungeon);
            
            var memberInfo = _party.GetMemberInfo(characterId);
            if (memberInfo != null)
            {
                var partyLog = new PartyLogRift(PartyLogType.RiftJoin, memberInfo.character.nickname, _rift.GetStageUid(), _rift.GetGrade());
                _partyLog.AddLog(partyLog);
                OnUpdatePartyLog();
            }
        }
        
        void OnLoadWorldBossData_Serialized(RedisRequest redisRequest)
        {
            // check condition
            var characterId = redisRequest.CharacterId;
            
            var curConn = GetConnection(characterId);
            if (curConn == null)
            {
                return;
            }
            
            var dungeon = _worldBossDungeonMap[characterId];
            string dungeonKey = String.Empty;

            if (dungeon == null)
            {
                curConn.SC_JOIN_WORLD_BOSS(CorgiErrorCode.WorldBossNoDungeon, _roomId, characterId, string.Empty, null);
                return;
            }
            
            // worldboss data 
            var taskList = redisRequest.GetRedisTasks();
            var canTry = true;
            var curHP = 0L;
            var maxHP = 0L;
            var errorCode = CorgiErrorCode.Success;
            try
            {
                foreach (var curTask in taskList)
                {
                    switch (curTask.RequestType)
                    {
                        case RedisRequestType.DungeonAuth:
                        {
                            var thisTask = curTask as RedisTaskDungeonAuth;
                            if (thisTask == null)
                            {
                                errorCode = CorgiErrorCode.WorldBossHaveRedisProblem;
                                break;
                            }

                            dungeonKey = thisTask.DungeonKey;
                            canTry = thisTask.CanTry;
                            break;
                        }
                        case RedisRequestType.WorldBossCurHP:
                        {
                            var thisTask = curTask as RedisTaskWorldBossCurHP;
                            if (thisTask == null)
                            {
                                errorCode = CorgiErrorCode.WorldBossHaveRedisProblem;
                                break;
                            }

                            curHP = thisTask.CurHP;
                            canTry = thisTask.CanTry;

                            break;
                        }
                        case RedisRequestType.WorldBossMaxHP:
                        {
                            var thisTask = curTask as RedisTaskWorldBossMaxHP;
                            if (thisTask == null)
                            {
                                errorCode = CorgiErrorCode.WorldBossHaveRedisProblem;
                                break;
                            }
                            
                            maxHP = thisTask.MaxHP;
                            canTry = thisTask.CanTry;

                            break;
                        }
                    }

                    if (canTry == false || errorCode != CorgiErrorCode.Success)
                    {
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                CorgiLog.Log(CorgiLogType.Fatal, "Occur exception[{0}] in OnLoadWorldBossData", e.ToString());
                
                //-don't call return here, "_isRequestingCount--" have to call. as below 
            }
            
            _isRequestingCount--;//-have to call

            if (canTry == false || errorCode != CorgiErrorCode.Success || string.IsNullOrEmpty(dungeonKey))
            {
                curConn.SC_JOIN_WORLD_BOSS(CorgiErrorCode.WorldBossHaveRedisProblem, _roomId, characterId, string.Empty, null);
                return;
            }
            
            // create unit list
            var finalList = CreateUnitList(dungeon);
            
            dungeon.UpdateAutoHuntingBuff(_buffEndTimestamp);
            dungeon.InitBossHP(maxHP, curHP);
            
            // enter dungeon for start
            var retLog = dungeon.EnterDungeon(finalList, 0);

            if (retLog == null)
            {
                curConn.SC_JOIN_WORLD_BOSS(CorgiErrorCode.WorldBossError, _roomId, characterId, dungeonKey, null);
                return;
            }
            
            curConn.SC_JOIN_WORLD_BOSS(CorgiErrorCode.Success, _roomId, characterId, dungeonKey, retLog.SharedInstance as SharedDungeon);
        }
        
        void JoinArena_Serialized(string characterId, string dungeonKey, string targetId)
        {
            var split = dungeonKey.Split('-');
            if (split.Length != 4)
            {
                CorgiCombatLog.LogError(CombatLogCategory.Dungeon, "invalid dungeonKey at JoinArena {0}", dungeonKey);
                return;
            }
            
            characterId = split[1];
            targetId = split[2];
            
            var targetIds = split[3].Split(',');
            if (targetIds.Length > 4)
            {
                CorgiCombatLog.LogError(CombatLogCategory.Dungeon, "invalid dungeonKey at JoinArena {0}", dungeonKey);
                return;
            }

            var curConn = GetConnection(characterId);
            if (curConn == null)
            {
                return;
            }
            
            if (_arenaDungeonMap.ContainsKey(characterId))
            {
                var oldDungeon = _arenaDungeonMap[characterId];
                if (oldDungeon != null && oldDungeon.State != DungeonState.Destroy)
                {
                    return;
                }
                _arenaDungeonMap.Remove(characterId);
            }
            
            var dungeon = new DungeonArena(this);
            
            if (dungeon.Load(0) == false)
            {
                CorgiCombatLog.LogError(CombatLogCategory.Dungeon, "ArenaDungeon Load Failed!");
                return;
            }

            dungeon.CharacterId = characterId;
            dungeon.DungeonKey = dungeonKey;
            dungeon.TargetId = targetId;

            _arenaDungeonMap.Add(characterId, dungeon);
            
            var paramList = new List<RequestParam>();
            paramList.Add(new RequestParam(RedisRequestType.DungeonAuth, dungeonKey));
            paramList.Add(new RequestParam(RedisRequestType.CharaterInfo, characterId));
            foreach (var targetCharacterId in targetIds)
            {
                paramList.Add(new RequestParam(RedisRequestType.EnemyInfo, targetCharacterId));
            }

            if (RequestData(characterId, paramList, "OnLoadArena") == false)
            {
                CorgiCombatLog.LogError(CombatLogCategory.Dungeon, "Failed to RequestData on JoinArena {0}", characterId);
            }
        }

        void OnLoadArena_Serialized(RedisRequest redisRequest)
        {
            var characterId = redisRequest.CharacterId;

            var curConn = GetConnection(characterId);
            if (curConn == null || _arenaDungeonMap.ContainsKey(characterId) == false)
            {
                return;
            }

            var dungeon = _arenaDungeonMap[characterId];
            if (dungeon == null)
            {
                CorgiCombatLog.LogError(CombatLogCategory.Dungeon, "dungeon is null OnLoadArena {0}", characterId);
                return;
            }
            
            var taskList = redisRequest.GetRedisTasks();
            var canTry = true;
            var dungeonKey = String.Empty;
            var errorCode = CorgiErrorCode.Success;
            try
            {
                foreach (var curTask in taskList)
                {
                    switch (curTask.RequestType)
                    {
                        case RedisRequestType.DungeonAuth:
                        {
                            var thisTask = curTask as RedisTaskDungeonAuth;
                            if (thisTask == null)
                            {
                                errorCode = CorgiErrorCode.WorldBossHaveRedisProblem;
                                break;
                            }

                            dungeonKey = thisTask.DungeonKey;
                            canTry = thisTask.CanTry;
                            break;
                        }
                        case RedisRequestType.CharaterInfo:
                        {
                            var thisTask = curTask as RedisTaskCharacterInfo;
                            if (thisTask == null)
                            {
                                errorCode = CorgiErrorCode.WorldBossHaveRedisProblem;
                                break;
                            }

                            var memberInfo = thisTask.MemberInfo;

                            _party.AddOrUpdate(memberInfo);

                            break;
                        }
                        case RedisRequestType.EnemyInfo:
                        {
                            var thisTask = curTask as RedisTaskEnemyInfo;
                            if (thisTask == null)
                            {
                                continue;
                            }

                            var memberInfo = thisTask.MemberInfo;

                            var dict = dungeon.SharedEnemyList;

                            dict[thisTask.CharId] = memberInfo;
                            
                            break;
                        }
                    }

                    if (canTry == false || errorCode != CorgiErrorCode.Success)
                    {
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                CorgiLog.Log(CorgiLogType.Fatal, "Occur exception[{0}] in OnLoadArena", e.ToString());
            }
            
            _isRequestingCount--;

            if (canTry == false || errorCode != CorgiErrorCode.Success)
            {
                CorgiCombatLog.LogError(CombatLogCategory.Dungeon, "Error OnLoadArena {0}", characterId);
                return;
            }

            var friendList = CreateArenaUnitList(dungeon);
            var enemyList = CreateArenaEnemyList(dungeon);

            var dungeonLogNode = dungeon.EnterArena(friendList, enemyList);

            if (dungeonLogNode == null)
            {
                CorgiCombatLog.LogError(CombatLogCategory.Dungeon, "Cannot enter arena {0}", characterId);
                return;
            }

            curConn.SC_JOIN_ARENA(CorgiErrorCode.Success, _roomId, characterId, dungeonKey, dungeonLogNode.SharedInstance as SharedArena);
        }
        
        void OnWorldBossCompleted_Serialized(string characterId, string dungeonKey)
        {
            if (_worldBossDungeonMap.ContainsKey(characterId) == false)
            {
                //throw new CorgiException("invalid dungeon instance dungeon {0}/{1}/{2}", characterId, dungeonUid, stageUid);
                return;
            }
            
            var dungeon = _worldBossDungeonMap[characterId];
            
            if (dungeonKey != dungeon.DungeonKey)
            {
                CorgiLog.Log(CorgiLogType.Warning, "[Dungeon] dungeonKey[{0}] does not match OnWorldBossCompleted", dungeonKey);
                return;
            }
            
            dungeon.OnWorldBossDungeonCompleted(0);
            RedisManager.Instance.DeleteDungeonKey(dungeon.DungeonKey);
            
            //_instanceDungeonMap.Remove(characterId);
        }
        
        void OnWorldBossStop_Serialized(string characterId, string dungeonKey)
        {
            if (_worldBossDungeonMap.ContainsKey(characterId) == false)
            {
                //throw new CorgiException("invalid dungeon instance dungeon {0}/{1}/{2}", characterId, dungeonUid, stageUid);
                return;
            }
            
            var dungeon = _worldBossDungeonMap[characterId];
            
            if (dungeonKey != dungeon.DungeonKey)
            {
                CorgiLog.Log(CorgiLogType.Warning, "[Dungeon] dungeonKey[{0}] does not match OnWorldBossStop", dungeonKey);
                return;
            }
            
            dungeon.OnWorldBossDungeonStop();
            RedisManager.Instance.DeleteDungeonKey(dungeon.DungeonKey);
            
            _worldBossDungeonMap.Remove(characterId);
        }
        
        void OnRiftOpen_Serialized(SharedRift sharedRift, string characterId)
        {
            if (sharedRift == null)
            {
                return;
            }

            if (_rift != null)
            {
                CorgiCombatLog.LogError(CombatLogCategory.Rift, "Rift Should be null");
            }

            _rift = new Rift();

            if (_rift.OpenRift(sharedRift) == false)
            {
                // failed to open rift
                _rift = null;
                return;
            }

            OnUpdateRiftInfo();
            
            var memberInfo = _party.GetMemberInfo(characterId);
            if (memberInfo != null)
            {
                var partyLog = new PartyLogRift(PartyLogType.RiftOpen, memberInfo.character.nickname, _rift.GetStageUid(), _rift.GetGrade());
                _partyLog.AddLog(partyLog);
                OnUpdatePartyLog();
            }
        }
        
        void OnRiftCompleted_Serialized(string characterId, string dungeonKey)
        {
            if (string.IsNullOrEmpty(characterId) /*|| string.IsNullOrEmpty(dungeonKey)*/)
            {
                return;
            }

            var curDungeonKey = _rift.GetDungeonKey(characterId);
            //if (_rift.GetDungeonKey(characterId) != dungeonKey)
            if (string.IsNullOrEmpty(curDungeonKey) )
            {
                //throw new CorgiException("invalid dungeon instance dungeon {0}/{1}/{2}", characterId, dungeonUid, stageUid);
                return;
            }
            
            _rift.OnRiftDungeonCompleted(characterId);

            OnUpdateRiftInfo();
            
            RedisManager.Instance.DeleteDungeonKey(curDungeonKey);
        }
        
        void OnRiftStop_Serialized(string characterId, string dungeonKey)
        {
            var curDungeonKey = _rift.GetDungeonKey(characterId);
            
            if (string.IsNullOrEmpty(curDungeonKey))
            {
                return;
            }
            
            _rift.OnRiftDungeonStop(characterId);

            OnUpdateRiftInfo();
            
            RedisManager.Instance.DeleteDungeonKey(curDungeonKey);
        }
        
        void OnArenaCompleted_Serialized(string characterId, string dungeonKey)
        {
            if (_arenaDungeonMap.ContainsKey(characterId) == false)
            {
                return;
            }
            
            var dungeon = _arenaDungeonMap[characterId];
            
            if (dungeonKey != dungeon.DungeonKey)
            {
                CorgiLog.Log(CorgiLogType.Warning, "[Dungeon] dungeonKey[{0}] does not match OnArenaCompleted", dungeonKey);
                return;
            }
            
            dungeon.OnArenaDungeonCompleted(0);
            RedisManager.Instance.DeleteDungeonKey(dungeon.DungeonKey);
        }
        
        void OnArenaStop_Serialized(string characterId, string dungeonKey)
        {
            if (_arenaDungeonMap.ContainsKey(characterId) == false)
            {
                return;
            }
            
            var dungeon = _arenaDungeonMap[characterId];
            
            if (dungeonKey != dungeon.DungeonKey)
            {
                CorgiLog.Log(CorgiLogType.Warning, "[Dungeon] dungeonKey[{0}] does not match OnArenaStop", dungeonKey);
                return;
            }
            
            dungeon.OnArenaDungeonStop();
            RedisManager.Instance.DeleteDungeonKey(dungeon.DungeonKey);
            
            _arenaDungeonMap.Remove(characterId);
        }
        
        /// <summary>
        /// from web server redis command
        /// to party join
        /// </summary>
        /// <param name="characterId"></param>
        /// <exception cref="CorgiException"></exception>
        void OnPartyJoin_Serialized(string characterId)
        {
            var paramList = new List<RequestParam>();
            paramList.Add(new RequestParam(RedisRequestType.CharaterInfo, characterId));
            paramList.Add(new RequestParam(RedisRequestType.RoomDeckInfo, _roomId));
            
            CorgiCombatLog.Log(CombatLogCategory.Party, "[party][1/2] Received party join command from redis. character : [{0}] and request character info to redis", characterId);
            
            if (RequestData(characterId, paramList, "OnLoadPartyJoin") == false)
            {
                CorgiCombatLog.LogError(CombatLogCategory.Party, "[party] [{0}] join failed. when request OnLoadPartyJoin", characterId);
                throw new CorgiException("can't request to load data for partyJoin");
            }
        }

        void OnLoadPartyJoin_Serialized(RedisRequest redisRequest)
        {
            var taskList = redisRequest.GetRedisTasks();
            bool occurException = false;

            var isUpdatedParty = false;
            
            try
            {
                OnLoadRequestData(redisRequest);
                foreach (var curTask in taskList)
                {
                    if (curTask == null 
                        || curTask.RequestType != RedisRequestType.CharaterInfo )
                    {
                        continue;
                    }

                    var thisTask = curTask as RedisTaskCharacterInfo;
                    if (thisTask == null)
                    {
                        CorgiLog.Log(CorgiLogType.Error, "[party] join failed. RedisTaskCharacterInfo is null");
                        //throw new CorgiException("invalid redis task({0})", curTask.RequestType);
                        continue;
                    }

                    var memberInfo = thisTask.MemberInfo;
                    //_party.AddOrUpdate(memberInfo);
                    _party.SetJoinTime(memberInfo.character.dbId, CorgiTime.UtcNowULong);

                    var partyLog = new PartyLogUser(PartyLogType.PartyJoin, memberInfo.character.nickname);
                    _partyLog.AddLog(partyLog);
                    OnUpdatePartyLog();

                    CorgiCombatLog.Log(CombatLogCategory.Party,
                        "[party][2/2] Received party join command from redis. character : [{0}] and response character info from redis",
                        memberInfo.character.dbId);

                    //-join하는 캐릭터를 dungeon에 추가 한다. 
                    //AddJoinUnit(memberInfo.character.dbId);
                    
                    isUpdatedParty = true;
                }

            }
            catch (Exception e)
            {
                occurException = true;
                CorgiLog.Log(CorgiLogType.Fatal, "Occur exception[{0}] when call OnLoadPartyJoin", e.ToString());
                
                //-don't call return here, "_isRequestingCount--" have to call. as below   
            }

            //_isRequestingCount--;//-have to call!!! // called by OnLoadRequestData

            if (true == occurException)
            {
                return;
            }
            
            if (isUpdatedParty == false)
            {
                return;
            }
            
            foreach (var conn in _connectionList)
            {
                if (conn == null)
                {
                    continue;
                }
                
                conn.SC_UPDATE_PARTY_MEMBER(_roomId);
            }
        }
        
        /// <summary>
        /// from web server redis command
        /// to party leave
        /// </summary>
        /// <param name="characterId"></param>
        void OnPartyLeave_Serialized(string characterId)
        {
            var memberInfo = _party.GetMemberInfo(characterId);
            var isUpdatedParty = false;
            
            if (memberInfo != null)
            {
                CorgiCombatLog.Log(CombatLogCategory.Party, "PartyMember[{0}] Leave. this room[{1}]. member will be removed", characterId, RoomId);
                
                _party.RemoveMember(characterId);
                if (0 == _party.MemberCount())
                {
                    _roomState = RoomState.WillBeDestroy;
                    var lastMs = CombatServerConfigConst.EMPTY_ROOM_ALIVE_TIME_MS;
                    
                    CorgiCombatLog.Log(CombatLogCategory.Party, "PartyMember[{0}] Leave. this room[{1}] will be destroy. after [{2}] seconds", characterId, RoomId, lastMs);
                    StartDestoryTimer(lastMs);
                }
                
                var partyMemberStatus = _partyStatus.OnClosed(characterId);
                OnUpdatePartyMemberStatus(partyMemberStatus);
                
                var partyLog = new PartyLogUser(PartyLogType.PartyLeave, memberInfo.character.nickname);
                _partyLog.AddLog(partyLog);
                OnUpdatePartyLog();


                //-leave하는 캐릭터를 dungeon에서 삭제한다. 
                //AddLeaveUnit(characterId);
                
                isUpdatedParty = true;
            }
            
            if (isUpdatedParty == false)
            {
                return;
            }
            
            foreach (var conn in _connectionList)
            {
                if (conn == null)
                {
                    continue;
                }
                
                conn.SC_UPDATE_PARTY_MEMBER(_roomId);
            }
        }
        
        /// <summary>
        /// from web server redis command
        /// to party leave
        /// </summary>
        /// <param name="characterId"></param>
        void OnPartyExile_Serialized(string characterId)
        {
            var memberInfo = _party.GetMemberInfo(characterId);
            
            var isUpdatedParty = false;
            
            if (memberInfo != null)
            {
                CorgiCombatLog.Log(CombatLogCategory.Party, "PartyMember[{0}] Leave. this room[{1}]. member will be removed", characterId, RoomId);
                
                _party.RemoveMember(characterId);
                if (0 == _party.MemberCount())
                {
                    _roomState = RoomState.WillBeDestroy;
                    var lastMs = CombatServerConfigConst.EMPTY_ROOM_ALIVE_TIME_MS;
                    
                    CorgiCombatLog.Log(CombatLogCategory.Party, "PartyMember[{0}] Leave. this room[{1}] will be destroy. after [{2}] seconds", characterId, RoomId, lastMs);
                    StartDestoryTimer(lastMs);
                }
                
                var memberStatus = _partyStatus.OnClosed(characterId);
                OnUpdatePartyMemberStatus(memberStatus);
                
                
                var partyLog = new PartyLogUser(PartyLogType.PartyExile, memberInfo.character.nickname);
                _partyLog.AddLog(partyLog);
                OnUpdatePartyLog();

                //-leave하는 캐릭터를 dungeon에서 삭제한다. 
                //AddLeaveUnit(characterId);
                
                isUpdatedParty = true;
            }
            
            if (isUpdatedParty == false)
            {
                return;
            }
            
            foreach (var conn in _connectionList)
            {
                if (conn == null)
                {
                    continue;
                }
                
                conn.SC_UPDATE_PARTY_MEMBER(_roomId);
            }
        }
        
        /// <summary>
        /// from client
        /// to update party status
        /// Update UserAction
        /// </summary>
        /// <param name="partyMemberStatus"></param>
        void OnUpdatePartyMemberStatus_Serialized(PartyMemberStatus partyMemberStatus)
        {
            if (partyMemberStatus == null)
            {
                return;
            }

            var characterId = partyMemberStatus.CharacterId;
            //var memberInfo = _party.GetMemberInfo(characterId);
            var memberStatus = _partyStatus.GetPartyMemberStatus(characterId);
            if (memberStatus == null)
            {
                return;
            }
            CorgiCombatLog.Log(CombatLogCategory.Party, "PartyMember[{0}] Status Updated to {1}.", characterId, partyMemberStatus.UserAction);
            
            foreach (var conn in _connectionList)
            {
                // 자기 자신 외의 사람한테만 보낸다.
                if (conn == null || conn.CharacterId == characterId)
                {
                    continue;
                }
                
                conn.SC_UPDATE_PARTY_MEMBER_STATUS(_roomId, partyMemberStatus);
            }
        }
        
        
        /// <summary>
        /// from web server redis command
        /// to on event acquire skill item 
        /// </summary>
        void OnEventAcquireSkillItem_Serialized(string characterId, long skillItemUid)
        {
            var memberInfo = _party.GetMemberInfo(characterId);

            if (memberInfo == null)
            {
                CorgiCombatLog.LogError(CombatLogCategory.Party, "PartyMember[{0}] is Invalid. this room[{1}] for Acquire Skill Item.", characterId, RoomId);
                return;
            }
            
            CorgiCombatLog.Log(CombatLogCategory.Party, "PartyMember[{0}] Acquire SkillItem{1}. this room[{2}].", characterId, skillItemUid, RoomId);

            if (skillItemUid == 0)
            {
                CorgiCombatLog.LogError(CombatLogCategory.Party, "PartyMember[{0}] is Invalid. this room[{1}] for Acquire Skill Item.", characterId, RoomId);
                return;
            }
                
            var partyLog = new PartyLogAcquire(PartyLogType.AcquireSkill, memberInfo.character.nickname, (ulong)skillItemUid);
            _partyLog.AddLog(partyLog);
            OnUpdatePartyLog();
        }
        
        /// <summary>
        /// from web server redis command
        /// to on event acquire equip item 
        /// </summary>
        void OnEventAcquireEquipItem_Serialized(string characterId, long equipUid)
        {
            var memberInfo = _party.GetMemberInfo(characterId);

            if (memberInfo == null)
            {
                CorgiCombatLog.LogError(CombatLogCategory.Party, "PartyMember[{0}] is Invalid. this room[{1}] for Acquire Equip Item.", characterId, RoomId);
                return;
            }
            
            CorgiCombatLog.Log(CombatLogCategory.Party, "PartyMember[{0}] Acquire Equip{1}. this room[{2}].", characterId, equipUid, RoomId);

            if (equipUid == 0)
            {
                CorgiCombatLog.LogError(CombatLogCategory.Party, "PartyMember[{0}] is Invalid. this room[{1}] for Acquire Equip Item.", characterId, RoomId);
                return;
            }
                
            var partyLog = new PartyLogAcquire(PartyLogType.AcquireEquip, memberInfo.character.nickname, (ulong)equipUid);
            _partyLog.AddLog(partyLog);
            OnUpdatePartyLog();
        }
        

        void OnSendChatting_Serialized(CorgiServerConnection conn, ChattingType chattingType, string data)
        {
            string characterId = conn.CharacterId;
            
            var memberInfo = _party.GetMemberInfo(characterId);
            if (memberInfo == null)
            {
                return;
            }

            var timestamp = CorgiTime.UtcNowULong;
            var chattingMessage = new ChattingMessage();
            
            chattingMessage.TimeStamp = timestamp;
            chattingMessage.ChattingType = chattingType;
            chattingMessage.CharacterId = memberInfo.character.dbId;
            chattingMessage.Uid = memberInfo.character.uid;
            chattingMessage.Nickname = memberInfo.character.nickname;
            chattingMessage.CharacterGrade = memberInfo.character.grade;
            chattingMessage.Message = data;
            
            if (chattingType == ChattingType.General || chattingType == ChattingType.League)
            {
                ChattingManager.Instance.SerializeMethod("OnChannelChatting", conn, chattingMessage);
            }
            else if (chattingType == ChattingType.Party)
            {
                _partyChatting.AddChatting(chattingMessage);
                
                foreach (var curConn in _connectionList)
                {
                    curConn.SC_UPDATE_CHATTING(chattingMessage);
                }
            }
        }

        void OnCloseWorldBoss(string characterId)
        {
            if (_worldBossDungeonMap.ContainsKey(characterId))
            {
                var dungeon = _worldBossDungeonMap[characterId];
                dungeon.OnWorldBossDungeonStop();
                RedisManager.Instance.DeleteDungeonKey(dungeon.DungeonKey);
                _worldBossDungeonMap.Remove(characterId);
            }
        }

        void OnCloseRift(string characterId)
        {
            if (_rift == null)
            {
                return;
            }
            
            var curDungeonKey = _rift.GetDungeonKey(characterId);
            
            if (string.IsNullOrEmpty(curDungeonKey))
            {
                return;
            }
            
            _rift.OnRiftDungeonStop(characterId);

            RedisManager.Instance.DeleteDungeonKey(curDungeonKey);
        }
        
        void OnCloseArena(string characterId)
        {
            if (_arenaDungeonMap.ContainsKey(characterId))
            {
                var dungeon = _arenaDungeonMap[characterId];
                dungeon.OnArenaDungeonStop();
                RedisManager.Instance.DeleteDungeonKey(dungeon.DungeonKey);
                _arenaDungeonMap.Remove(characterId);
            }
        }

        void OnUpdateRiftInfo()
        {
            if (_rift == null)
            {
                return;
            }
            
            var rift = _rift.GetSharedRiftInfo();
            foreach (var conn in _connectionList)
            {
                if (conn != null && rift != null)
                {
                    conn.SC_UPDATE_RIFT_INFO(rift);
                }
            }
        }
        
        void OnClose_Serialized(CorgiServerConnection serverConnection)
        {
            if (false == _connectionList.Contains(serverConnection))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"Warning!, OnClose at Room but cant' find ServerConnection", serverConnection.CharacterId, serverConnection.RoomId, true);
                return;
            }

            var roomId = serverConnection.RoomId;
            var characterId = serverConnection.CharacterId;

            OnCloseWorldBoss(characterId);
            OnCloseRift(characterId);
            OnCloseArena(characterId);

            _connectionList.Remove(serverConnection);
            OnUpdateRiftInfo();
            var memberStatus = _partyStatus.OnClosed(characterId);
            OnUpdatePartyMemberStatus(memberStatus);
            
            var remainConnectionCount = _connectionList.Count;

            CorgiCombatLog.Log(CombatLogCategory.User,$"OnClose at Room, remain connection count[{remainConnectionCount}] in this room", characterId, roomId);            
            
            if (0 == remainConnectionCount)
            {
                //if (true == CombatServerConfig.Instance.Server.AllowNoConnectionHunting)
                if(_adventureDungeon != null && _adventureDungeon.IsFailed)
                {
                    //-비접속 사냥을 시작하지.
                    _roomState = RoomState.NoConnectionHunting;
                    CorgiCombatLog.Log(CombatLogCategory.Party, "This Room[{0}] will be turn [{1}]mode. characterId[{2}]", 
                        roomId, RoomState.NoConnectionHunting.ToString(), characterId);
                    return;
                }

                try
                {
                    OnDestroyCallback();
                    StartDestoryTimer(CombatServerConfigConst.EMPTY_ROOM_ALIVE_TIME_MS);
                }
                catch (Exception e)
                {
                    CorgiCombatLog.LogFatal(CombatLogCategory.User,"occur exception when room close({0})\n{1}", RoomId, e);
                    DoDestroy((int)RoomDestroyReason.Exception);//-예외시 방삭제 하지 말고, client 의 disconn 에 맡긴다.                    
                }
            }
        }

        void OnDestroyCallback()
        {
            if (_rift != null)
            {
                RedisManager.Instance.SaveRiftInfo(_roomId, _rift.GetSharedRiftInfo());
            }
            RedisManager.Instance.SavePartyLogAll(_roomId, _partyLog, _partyChatting);
        }

        void OnRoomKill_Serialized()
        {
            foreach (var conn in _connectionList)
            {
                if (conn == null)
                {
                    continue;
                }
                
                conn.Disconnect();
            }
 
            DoDestroy((int)RoomDestroyReason.RoomKillCommand);
        }

        Character CreateCharacter(Dungeon dungeon, string charDbId)
        {
            SharedMemberInfo thisMember = _party.GetMemberInfo(charDbId);

            if (thisMember == null)
            {
                return null;
            }
            
            var unit = new Character(dungeon);
            var charInfo = thisMember.character;
            
            if (unit.Load(charInfo) == false)
            {
                CorgiLog.LogError("invalid character info");
                return null;
            }

            return unit;
        }

        public Skill CreateSkill(Character owner, ulong skillUid)
        {
            // var thisMember = _party.GetMemberInfo(owner.DBId);
            //
            // if (thisMember == null)
            // {
            //     return null;
            // }

            var skill = SkillFactory.Create(skillUid, owner);
            if (skill == null)
            {
                CorgiLog.LogError("invalid skill info for create {0}\n", skillUid);
                return null;
            }

            return skill;
        }
        
        public Skill CreateSkillFromItem(Character owner, ulong skillItemBaseUid)
        {
            var thisMember = _party.GetMemberInfo(owner.DBId);

            if (thisMember == null)
            {
                return null;
            }
            
            foreach (var skillItemInfo in thisMember.skills)
            {
                if (skillItemInfo == null || skillItemInfo.baseUid != skillItemBaseUid)
                {
                    continue;
                }

                var skillItemSheet = owner.Dungeon.GameData.GetData<SkillItemSpec>(skillItemInfo.uid);

                if (skillItemSheet == null)
                {
                    return null;
                }
                
                var skill = SkillFactory.Create(skillItemSheet.SkillUid, owner);
                if (skill == null)
                {
                    CorgiLog.LogError("invalid skill info for create {0}\n", skillItemInfo.uid);
                    return null;
                }

                return skill;
            }

            return null;
        }

        SkillActive CreateSkill(Unit owner, ulong skillUid, List<SharedSkillInfo> skillInfos, SkillSlot skillSlot = null)
        {
            if (skillUid == 0)
            {
                return null;
            }
            
            foreach (var skillItemInfo in skillInfos)
            {
                if (skillItemInfo == null || skillItemInfo.baseUid != skillUid)
                {
                    continue;
                }
                
                if (skillItemInfo.characterId != owner.DBId)
                {
                    continue;
                }

                // var skillItemSheet = owner.Dungeon.GameData.GetData<SkillItemSpec>(skillItemInfo.uid);
                //
                // if (skillItemSheet == null)
                // {
                //     return null;
                // }
                
                var skill = SkillFactory.Create(skillItemInfo, owner, skillSlot) as SkillActive;
                if (skill == null)
                {
                    CorgiLog.LogError("invalid skill info for create {0}\n", skillItemInfo.uid);
                    return null;
                }

                return skill;
            }

            return null;
        }

        SkillActive CreateSkill(Unit owner, ulong relicUid, List<SharedRelicInfo> relicInfos)
        {
            if (relicUid == 0)
            {
                return null;
            }
            
            foreach (var relicInfo in relicInfos)
            {
                if (relicInfo == null || relicInfo.baseUid != relicUid)
                {
                    continue;
                }

                if (relicInfo.characterId != owner.DBId)
                {
                    continue;
                }

                var sheet = owner.Dungeon.GameData.GetData<RelicInfoSpec>(relicInfo.uid);

                if (sheet == null)
                {
                    return null;
                }
                
                var skill = SkillFactory.Create(sheet.SkillUid, owner) as SkillActive;
                if (skill == null)
                {
                    CorgiLog.LogError("invalid skill info for create {0}\n", sheet.SkillUid);
                    return null;
                }

                return skill;
            }

            return null;
            
        }

        private SkillSlot CreateSkillSlot(Unit owner, string dbId, List<SharedSkillSlot> skillSlots)
        {
            if (skillSlots == null)
            {
                return null;
            }
            
            var skillSlot = skillSlots.Find(element => element?.dbId == dbId);
            return SkillSlot.Create(skillSlot, owner);
        }
        public Deck GetPersonalDeck(Unit owner)
        {
            var thisMemberInfo = _party.GetMemberInfo(owner.DBId);

            if (thisMemberInfo == null)
            {
                return null;
            }

            var settingInfo = thisMemberInfo.characterSetting;
            var thisDeck = new Deck(owner.DBId);
            
            thisDeck.SkillSlots.Add(CreateSkillSlot(owner, settingInfo.skill0Slot, thisMemberInfo.skillSlots));
            thisDeck.SkillSlots.Add(CreateSkillSlot(owner, settingInfo.skill1Slot, thisMemberInfo.skillSlots));
            thisDeck.SkillSlots.Add(CreateSkillSlot(owner, settingInfo.skill2Slot, thisMemberInfo.skillSlots));
            thisDeck.SkillSlots.Add(CreateSkillSlot(owner, settingInfo.skill3Slot, thisMemberInfo.skillSlots));
            
            thisDeck.ActiveSkills.Add(CreateSkill(owner, settingInfo.skill0Uid, thisMemberInfo.skills, thisDeck.SkillSlots[0]));
            thisDeck.ActiveSkills.Add(CreateSkill(owner, settingInfo.skill1Uid, thisMemberInfo.skills, thisDeck.SkillSlots[1]));
            thisDeck.ActiveSkills.Add(CreateSkill(owner, settingInfo.skill2Uid, thisMemberInfo.skills, thisDeck.SkillSlots[2]));
            thisDeck.ActiveSkills.Add(CreateSkill(owner, settingInfo.skill3Uid, thisMemberInfo.skills, thisDeck.SkillSlots[3]));

            thisDeck.Relics.Add(settingInfo.relic0Uid);
            thisDeck.Relics.Add(settingInfo.relic1Uid);
            thisDeck.Relics.Add(settingInfo.relic2Uid);
            thisDeck.Relics.Add(settingInfo.relic3Uid);
            
            thisDeck.RelicSkills.Add(CreateSkill(owner, settingInfo.relic0Uid, thisMemberInfo.relics));
            thisDeck.RelicSkills.Add(CreateSkill(owner, settingInfo.relic1Uid, thisMemberInfo.relics));
            thisDeck.RelicSkills.Add(CreateSkill(owner, settingInfo.relic2Uid, thisMemberInfo.relics));
            thisDeck.RelicSkills.Add(CreateSkill(owner, settingInfo.relic3Uid, thisMemberInfo.relics));
            
            return thisDeck;
        }

        public Deck GetCoPartyDeck(Unit owner)
        {
            var deckInfo = _party.CoPartyDeck;
            var settingInfo = deckInfo.characterCoPartySetting;
            var characterId = owner.DBId;
            var thisMemberInfo = _party.GetMemberInfo(characterId);

            var thisDeck = new Deck(owner.DBId);

            if (characterId == settingInfo.character0Id)
            {
                thisDeck.SkillSlots.Add(CreateSkillSlot(owner, settingInfo.character0Slot0Id, thisMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(owner, settingInfo.character0Slot1Id, thisMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(owner, settingInfo.character0Slot2Id, thisMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(owner, settingInfo.character0Slot3Id, thisMemberInfo.skillSlots));
                
                thisDeck.ActiveSkills.Add(CreateSkill(owner, settingInfo.character0Skill0Uid, deckInfo.skills, thisDeck.SkillSlots[0]));
                thisDeck.ActiveSkills.Add(CreateSkill(owner, settingInfo.character0Skill1Uid, deckInfo.skills, thisDeck.SkillSlots[1]));
                thisDeck.ActiveSkills.Add(CreateSkill(owner, settingInfo.character0Skill2Uid, deckInfo.skills, thisDeck.SkillSlots[2]));
                thisDeck.ActiveSkills.Add(CreateSkill(owner, settingInfo.character0Skill3Uid, deckInfo.skills, thisDeck.SkillSlots[3]));

                thisDeck.Relics.Add(settingInfo.character0Relic0Uid);
                thisDeck.Relics.Add(settingInfo.character0Relic1Uid);
                thisDeck.Relics.Add(settingInfo.character0Relic2Uid);
                thisDeck.Relics.Add(settingInfo.character0Relic3Uid);
                
                thisDeck.RelicSkills.Add(CreateSkill(owner, settingInfo.character0Relic0Uid, deckInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(owner, settingInfo.character0Relic1Uid, deckInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(owner, settingInfo.character0Relic2Uid, deckInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(owner, settingInfo.character0Relic3Uid, deckInfo.relics));
            }else if (characterId == settingInfo.character1Id)
            {
                thisDeck.SkillSlots.Add(CreateSkillSlot(owner, settingInfo.character1Slot0Id, thisMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(owner, settingInfo.character1Slot1Id, thisMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(owner, settingInfo.character1Slot2Id, thisMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(owner, settingInfo.character1Slot3Id, thisMemberInfo.skillSlots));
                
                thisDeck.ActiveSkills.Add(CreateSkill(owner, settingInfo.character1Skill0Uid, deckInfo.skills, thisDeck.SkillSlots[0]));
                thisDeck.ActiveSkills.Add(CreateSkill(owner, settingInfo.character1Skill1Uid, deckInfo.skills, thisDeck.SkillSlots[1]));
                thisDeck.ActiveSkills.Add(CreateSkill(owner, settingInfo.character1Skill2Uid, deckInfo.skills, thisDeck.SkillSlots[2]));
                thisDeck.ActiveSkills.Add(CreateSkill(owner, settingInfo.character1Skill3Uid, deckInfo.skills, thisDeck.SkillSlots[3]));

                thisDeck.Relics.Add(settingInfo.character1Relic0Uid);
                thisDeck.Relics.Add(settingInfo.character1Relic1Uid);
                thisDeck.Relics.Add(settingInfo.character1Relic2Uid);
                thisDeck.Relics.Add(settingInfo.character1Relic3Uid);
                
                thisDeck.RelicSkills.Add(CreateSkill(owner, settingInfo.character1Relic0Uid, deckInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(owner, settingInfo.character1Relic1Uid, deckInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(owner, settingInfo.character1Relic2Uid, deckInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(owner, settingInfo.character1Relic3Uid, deckInfo.relics));
            }else if (characterId == settingInfo.character2Id)
            {
                thisDeck.SkillSlots.Add(CreateSkillSlot(owner, settingInfo.character2Slot0Id, thisMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(owner, settingInfo.character2Slot1Id, thisMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(owner, settingInfo.character2Slot2Id, thisMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(owner, settingInfo.character2Slot3Id, thisMemberInfo.skillSlots));

                thisDeck.ActiveSkills.Add(CreateSkill(owner, settingInfo.character2Skill0Uid, deckInfo.skills, thisDeck.SkillSlots[0]));
                thisDeck.ActiveSkills.Add(CreateSkill(owner, settingInfo.character2Skill1Uid, deckInfo.skills, thisDeck.SkillSlots[1]));
                thisDeck.ActiveSkills.Add(CreateSkill(owner, settingInfo.character2Skill2Uid, deckInfo.skills, thisDeck.SkillSlots[2]));
                thisDeck.ActiveSkills.Add(CreateSkill(owner, settingInfo.character2Skill3Uid, deckInfo.skills, thisDeck.SkillSlots[3]));

                thisDeck.Relics.Add(settingInfo.character2Relic0Uid);
                thisDeck.Relics.Add(settingInfo.character2Relic1Uid);
                thisDeck.Relics.Add(settingInfo.character2Relic2Uid);
                thisDeck.Relics.Add(settingInfo.character2Relic3Uid);
                
                thisDeck.RelicSkills.Add(CreateSkill(owner, settingInfo.character2Relic0Uid, deckInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(owner, settingInfo.character2Relic1Uid, deckInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(owner, settingInfo.character2Relic2Uid, deckInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(owner, settingInfo.character2Relic3Uid, deckInfo.relics));
                
            }else if (characterId == settingInfo.character3Id)
            {
                thisDeck.SkillSlots.Add(CreateSkillSlot(owner, settingInfo.character3Slot0Id, thisMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(owner, settingInfo.character3Slot1Id, thisMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(owner, settingInfo.character3Slot2Id, thisMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(owner, settingInfo.character3Slot3Id, thisMemberInfo.skillSlots));

                thisDeck.ActiveSkills.Add(CreateSkill(owner, settingInfo.character3Skill0Uid, deckInfo.skills, thisDeck.SkillSlots[0]));
                thisDeck.ActiveSkills.Add(CreateSkill(owner, settingInfo.character3Skill1Uid, deckInfo.skills, thisDeck.SkillSlots[1]));
                thisDeck.ActiveSkills.Add(CreateSkill(owner, settingInfo.character3Skill2Uid, deckInfo.skills, thisDeck.SkillSlots[2]));
                thisDeck.ActiveSkills.Add(CreateSkill(owner, settingInfo.character3Skill3Uid, deckInfo.skills, thisDeck.SkillSlots[3]));

                thisDeck.Relics.Add(settingInfo.character3Relic0Uid);
                thisDeck.Relics.Add(settingInfo.character3Relic1Uid);
                thisDeck.Relics.Add(settingInfo.character3Relic2Uid);
                thisDeck.Relics.Add(settingInfo.character3Relic3Uid);
                
                thisDeck.RelicSkills.Add(CreateSkill(owner, settingInfo.character3Relic0Uid, deckInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(owner, settingInfo.character3Relic1Uid, deckInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(owner, settingInfo.character3Relic2Uid, deckInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(owner, settingInfo.character3Relic3Uid, deckInfo.relics));
            }
            
            return thisDeck;
        }

        public Deck GetSoloPartyDeck(string ownerId, Unit unit)
        {
            var thisMemberInfo = _party.GetMemberInfo(ownerId);

            if (thisMemberInfo == null)
            {
                return null;
            }
            
            var characterId = unit.DBId;
            var characterMemberInfo = _party.GetMemberInfo(characterId);

            if (characterMemberInfo == null)
            {
                return null;
            }
            
            var settingInfo = thisMemberInfo.characterSoloPartySetting;
            var thisDeck = new Deck(characterId);
            
            if (characterId == settingInfo.character0Id)
            {
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character0Slot0Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character0Slot1Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character0Slot2Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character0Slot3Id, characterMemberInfo.skillSlots));

                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character0Skill0Uid, thisMemberInfo.skills, thisDeck.SkillSlots[0]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character0Skill1Uid, thisMemberInfo.skills, thisDeck.SkillSlots[1]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character0Skill2Uid, thisMemberInfo.skills, thisDeck.SkillSlots[2]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character0Skill3Uid, thisMemberInfo.skills, thisDeck.SkillSlots[3]));
                
                
                thisDeck.Relics.Add(settingInfo.character0Relic0Uid);
                thisDeck.Relics.Add(settingInfo.character0Relic1Uid);
                thisDeck.Relics.Add(settingInfo.character0Relic2Uid);
                thisDeck.Relics.Add(settingInfo.character0Relic3Uid);
                
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character0Relic0Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character0Relic1Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character0Relic2Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character0Relic3Uid, thisMemberInfo.relics));
                
                
            }else if (characterId == settingInfo.character1Id)
            {
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character1Slot0Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character1Slot1Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character1Slot2Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character1Slot3Id, characterMemberInfo.skillSlots));
                
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character1Skill0Uid, thisMemberInfo.skills, thisDeck.SkillSlots[0]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character1Skill1Uid, thisMemberInfo.skills, thisDeck.SkillSlots[1]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character1Skill2Uid, thisMemberInfo.skills, thisDeck.SkillSlots[2]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character1Skill3Uid, thisMemberInfo.skills, thisDeck.SkillSlots[3]));

                thisDeck.Relics.Add(settingInfo.character1Relic0Uid);
                thisDeck.Relics.Add(settingInfo.character1Relic1Uid);
                thisDeck.Relics.Add(settingInfo.character1Relic2Uid);
                thisDeck.Relics.Add(settingInfo.character1Relic3Uid);
                
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character1Relic0Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character1Relic1Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character1Relic2Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character1Relic3Uid, thisMemberInfo.relics));
            }else if (characterId == settingInfo.character2Id)
            {
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character2Slot0Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character2Slot1Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character2Slot2Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character2Slot3Id, characterMemberInfo.skillSlots));
                
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character2Skill0Uid, thisMemberInfo.skills, thisDeck.SkillSlots[0]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character2Skill1Uid, thisMemberInfo.skills, thisDeck.SkillSlots[1]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character2Skill2Uid, thisMemberInfo.skills, thisDeck.SkillSlots[2]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character2Skill3Uid, thisMemberInfo.skills, thisDeck.SkillSlots[3]));

                thisDeck.Relics.Add(settingInfo.character2Relic0Uid);
                thisDeck.Relics.Add(settingInfo.character2Relic1Uid);
                thisDeck.Relics.Add(settingInfo.character2Relic2Uid);
                thisDeck.Relics.Add(settingInfo.character2Relic3Uid);
                
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character2Relic0Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character2Relic1Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character2Relic2Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character2Relic3Uid, thisMemberInfo.relics));
                
            }else if (characterId == settingInfo.character3Id)
            {
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character3Slot0Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character3Slot1Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character3Slot2Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character3Slot3Id, characterMemberInfo.skillSlots));
                
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character3Skill0Uid, thisMemberInfo.skills, thisDeck.SkillSlots[0]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character3Skill1Uid, thisMemberInfo.skills, thisDeck.SkillSlots[1]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character3Skill2Uid, thisMemberInfo.skills, thisDeck.SkillSlots[2]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character3Skill3Uid, thisMemberInfo.skills, thisDeck.SkillSlots[3]));

                thisDeck.Relics.Add(settingInfo.character3Relic0Uid);
                thisDeck.Relics.Add(settingInfo.character3Relic1Uid);
                thisDeck.Relics.Add(settingInfo.character3Relic2Uid);
                thisDeck.Relics.Add(settingInfo.character3Relic3Uid);
                
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character3Relic0Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character3Relic1Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character3Relic2Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character3Relic3Uid, thisMemberInfo.relics));
            }
            
            return thisDeck;
        }
        
        public Deck GetSoloOffenseDeck(string ownerId, Unit unit)
        {
            var thisMemberInfo = _party.GetMemberInfo(ownerId);

            if (thisMemberInfo == null)
            {
                return null;
            }
            
            var characterId = unit.DBId;
            var characterMemberInfo = _party.GetMemberInfo(characterId);

            if (characterMemberInfo == null)
            {
                return null;
            }
            
            var settingInfo = thisMemberInfo.characterSoloOffenceSetting;
            if (settingInfo == null)
            {
                settingInfo = thisMemberInfo.characterSoloPartySetting;
            }
            
            var thisDeck = new Deck(characterId);
            
            if (characterId == settingInfo.character0Id)
            {
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character0Slot0Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character0Slot1Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character0Slot2Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character0Slot3Id, characterMemberInfo.skillSlots));

                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character0Skill0Uid, thisMemberInfo.skills, thisDeck.SkillSlots[0]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character0Skill1Uid, thisMemberInfo.skills, thisDeck.SkillSlots[1]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character0Skill2Uid, thisMemberInfo.skills, thisDeck.SkillSlots[2]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character0Skill3Uid, thisMemberInfo.skills, thisDeck.SkillSlots[3]));
                
                
                thisDeck.Relics.Add(settingInfo.character0Relic0Uid);
                thisDeck.Relics.Add(settingInfo.character0Relic1Uid);
                thisDeck.Relics.Add(settingInfo.character0Relic2Uid);
                thisDeck.Relics.Add(settingInfo.character0Relic3Uid);
                
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character0Relic0Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character0Relic1Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character0Relic2Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character0Relic3Uid, thisMemberInfo.relics));
                
                
            }else if (characterId == settingInfo.character1Id)
            {
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character1Slot0Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character1Slot1Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character1Slot2Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character1Slot3Id, characterMemberInfo.skillSlots));
                
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character1Skill0Uid, thisMemberInfo.skills, thisDeck.SkillSlots[0]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character1Skill1Uid, thisMemberInfo.skills, thisDeck.SkillSlots[1]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character1Skill2Uid, thisMemberInfo.skills, thisDeck.SkillSlots[2]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character1Skill3Uid, thisMemberInfo.skills, thisDeck.SkillSlots[3]));

                thisDeck.Relics.Add(settingInfo.character1Relic0Uid);
                thisDeck.Relics.Add(settingInfo.character1Relic1Uid);
                thisDeck.Relics.Add(settingInfo.character1Relic2Uid);
                thisDeck.Relics.Add(settingInfo.character1Relic3Uid);
                
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character1Relic0Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character1Relic1Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character1Relic2Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character1Relic3Uid, thisMemberInfo.relics));
            }else if (characterId == settingInfo.character2Id)
            {
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character2Slot0Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character2Slot1Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character2Slot2Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character2Slot3Id, characterMemberInfo.skillSlots));
                
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character2Skill0Uid, thisMemberInfo.skills, thisDeck.SkillSlots[0]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character2Skill1Uid, thisMemberInfo.skills, thisDeck.SkillSlots[1]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character2Skill2Uid, thisMemberInfo.skills, thisDeck.SkillSlots[2]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character2Skill3Uid, thisMemberInfo.skills, thisDeck.SkillSlots[3]));

                thisDeck.Relics.Add(settingInfo.character2Relic0Uid);
                thisDeck.Relics.Add(settingInfo.character2Relic1Uid);
                thisDeck.Relics.Add(settingInfo.character2Relic2Uid);
                thisDeck.Relics.Add(settingInfo.character2Relic3Uid);
                
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character2Relic0Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character2Relic1Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character2Relic2Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character2Relic3Uid, thisMemberInfo.relics));
                
            }else if (characterId == settingInfo.character3Id)
            {
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character3Slot0Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character3Slot1Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character3Slot2Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character3Slot3Id, characterMemberInfo.skillSlots));
                
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character3Skill0Uid, thisMemberInfo.skills, thisDeck.SkillSlots[0]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character3Skill1Uid, thisMemberInfo.skills, thisDeck.SkillSlots[1]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character3Skill2Uid, thisMemberInfo.skills, thisDeck.SkillSlots[2]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character3Skill3Uid, thisMemberInfo.skills, thisDeck.SkillSlots[3]));

                thisDeck.Relics.Add(settingInfo.character3Relic0Uid);
                thisDeck.Relics.Add(settingInfo.character3Relic1Uid);
                thisDeck.Relics.Add(settingInfo.character3Relic2Uid);
                thisDeck.Relics.Add(settingInfo.character3Relic3Uid);
                
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character3Relic0Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character3Relic1Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character3Relic2Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character3Relic3Uid, thisMemberInfo.relics));
            }
            
            return thisDeck;
        }
        
        public Deck GetSoloDefenceDeck(string ownerId, string targetId, Unit unit)
        {
            var dungeon = _arenaDungeonMap[ownerId];
            var thisMemberInfo = dungeon.SharedEnemyList[targetId];

            if (thisMemberInfo == null)
            {
                return null;
            }
            
            var characterId = unit.DBId;

            var characterMemberInfo = dungeon.SharedEnemyList[characterId];

            if (characterMemberInfo == null)
            {
                return null;
            }
            
            var settingInfo = thisMemberInfo.characterSoloDefenceSetting;
            if (settingInfo == null)
            {
                settingInfo = thisMemberInfo.characterSoloPartySetting;
            }
            
            var thisDeck = new Deck(characterId);
            
            if (characterId == settingInfo.character0Id)
            {
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character0Slot0Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character0Slot1Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character0Slot2Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character0Slot3Id, characterMemberInfo.skillSlots));

                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character0Skill0Uid, thisMemberInfo.skills, thisDeck.SkillSlots[0]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character0Skill1Uid, thisMemberInfo.skills, thisDeck.SkillSlots[1]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character0Skill2Uid, thisMemberInfo.skills, thisDeck.SkillSlots[2]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character0Skill3Uid, thisMemberInfo.skills, thisDeck.SkillSlots[3]));
                
                
                thisDeck.Relics.Add(settingInfo.character0Relic0Uid);
                thisDeck.Relics.Add(settingInfo.character0Relic1Uid);
                thisDeck.Relics.Add(settingInfo.character0Relic2Uid);
                thisDeck.Relics.Add(settingInfo.character0Relic3Uid);
                
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character0Relic0Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character0Relic1Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character0Relic2Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character0Relic3Uid, thisMemberInfo.relics));
                
                
            }else if (characterId == settingInfo.character1Id)
            {
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character1Slot0Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character1Slot1Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character1Slot2Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character1Slot3Id, characterMemberInfo.skillSlots));
                
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character1Skill0Uid, thisMemberInfo.skills, thisDeck.SkillSlots[0]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character1Skill1Uid, thisMemberInfo.skills, thisDeck.SkillSlots[1]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character1Skill2Uid, thisMemberInfo.skills, thisDeck.SkillSlots[2]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character1Skill3Uid, thisMemberInfo.skills, thisDeck.SkillSlots[3]));

                thisDeck.Relics.Add(settingInfo.character1Relic0Uid);
                thisDeck.Relics.Add(settingInfo.character1Relic1Uid);
                thisDeck.Relics.Add(settingInfo.character1Relic2Uid);
                thisDeck.Relics.Add(settingInfo.character1Relic3Uid);
                
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character1Relic0Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character1Relic1Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character1Relic2Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character1Relic3Uid, thisMemberInfo.relics));
            }else if (characterId == settingInfo.character2Id)
            {
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character2Slot0Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character2Slot1Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character2Slot2Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character2Slot3Id, characterMemberInfo.skillSlots));
                
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character2Skill0Uid, thisMemberInfo.skills, thisDeck.SkillSlots[0]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character2Skill1Uid, thisMemberInfo.skills, thisDeck.SkillSlots[1]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character2Skill2Uid, thisMemberInfo.skills, thisDeck.SkillSlots[2]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character2Skill3Uid, thisMemberInfo.skills, thisDeck.SkillSlots[3]));

                thisDeck.Relics.Add(settingInfo.character2Relic0Uid);
                thisDeck.Relics.Add(settingInfo.character2Relic1Uid);
                thisDeck.Relics.Add(settingInfo.character2Relic2Uid);
                thisDeck.Relics.Add(settingInfo.character2Relic3Uid);
                
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character2Relic0Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character2Relic1Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character2Relic2Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character2Relic3Uid, thisMemberInfo.relics));
                
            }else if (characterId == settingInfo.character3Id)
            {
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character3Slot0Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character3Slot1Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character3Slot2Id, characterMemberInfo.skillSlots));
                thisDeck.SkillSlots.Add(CreateSkillSlot(unit, settingInfo.character3Slot3Id, characterMemberInfo.skillSlots));
                
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character3Skill0Uid, thisMemberInfo.skills, thisDeck.SkillSlots[0]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character3Skill1Uid, thisMemberInfo.skills, thisDeck.SkillSlots[1]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character3Skill2Uid, thisMemberInfo.skills, thisDeck.SkillSlots[2]));
                thisDeck.ActiveSkills.Add(CreateSkill(unit, settingInfo.character3Skill3Uid, thisMemberInfo.skills, thisDeck.SkillSlots[3]));

                thisDeck.Relics.Add(settingInfo.character3Relic0Uid);
                thisDeck.Relics.Add(settingInfo.character3Relic1Uid);
                thisDeck.Relics.Add(settingInfo.character3Relic2Uid);
                thisDeck.Relics.Add(settingInfo.character3Relic3Uid);
                
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character3Relic0Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character3Relic1Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character3Relic2Uid, thisMemberInfo.relics));
                thisDeck.RelicSkills.Add(CreateSkill(unit, settingInfo.character3Relic3Uid, thisMemberInfo.relics));
            }
            
            return thisDeck;
        }

        public Equip CreateEquip(Character owner, string equipDbId)
        {
            var thisMember = _party.GetMemberInfo(owner.DBId);

            if (thisMember == null)
            {
                return null;
            }

            SharedEquipInfo thisEquip = null;
            foreach (var dbEquip in thisMember.equips)
            {
                if (dbEquip != null && dbEquip.dbId == equipDbId)
                {
                    thisEquip = dbEquip;
                    break;
                }
            }

            if (thisEquip == null)
            {
                CorgiLog.LogError("invalid equip info for create {0}", equipDbId);
                return null;
            }
            
            var equip = new Equip(owner);
            
            if (equip.Load(thisEquip) == false)
            {
                CorgiLog.LogError("invalid equip info for create {0}", equipDbId);
                return null;
            }

            return equip;
        }

        public SharedAlmanacStat GetAlmanacStat(Character owner)
        {
            var thisMember = _party.GetMemberInfo(owner.DBId);

            if (thisMember == null)
            {
                return null;
            }

            return thisMember.almanacStat;
        }

        public List<BindingStone> GetBindingStones(Character owner)
        {
            var memberInfo = _party.GetMemberInfo(owner.DBId);

            if (memberInfo == null)
            {
                return null;
            }

            var retList = new List<BindingStone>();

            if (memberInfo.bindingStones == null)
            {
                return null;
            }
            
            foreach (var sBindingStone in memberInfo.bindingStones)
            {
                var bindingStone = BindingStone.Create(sBindingStone, owner);

                if (bindingStone != null)
                {
                    retList.Add(bindingStone);
                }
            }

            return retList;
        }

        public SharedCharInfo GetCharInfo(string characterId)
        {
            var memberInfo = _party.GetMemberInfo(characterId);

            return memberInfo?.character;
        }


        public void StageFinish(string characterId, ulong stageUid, bool stageResult)
        {
            List<string> characterIds = new List<string>();
            foreach (var conn in _connectionList)
            {
                if (conn == null)
                {
                    continue;
                }

                characterIds.Add(conn.CharacterId);
            }
            RedisManager.Instance.SendStageFinish(_roomId, stageUid, stageResult, characterIds);
            
            var remainConnectionCount = _connectionList.Count;

            if (0 == remainConnectionCount && _adventureDungeon.IsFailed == false)
            {
                RedisManager.Instance.SavePartyLogAll(_roomId, _partyLog, _partyChatting);
                StartDestoryTimer(CombatServerConfigConst.EMPTY_ROOM_ALIVE_TIME_MS);
            }
            
        }

        public void ChallengeFinish(string characterId, ulong stageUid, bool stageResult)
        {
            RedisManager.Instance.SendChallengeFinish(_roomId, characterId, stageUid, stageResult);
        }

        public void InstanceDungeonFinish(string characterId, DungeonCriteriaType dungeonType, string dungeonId, ulong dungeonUid, ulong stageUid, bool stageResult)
        {
            RedisManager.Instance.SendInstanceDungeonFinish(_roomId, characterId, dungeonId, dungeonUid, stageUid, stageResult);
            
            if (stageResult && dungeonType == DungeonCriteriaType.DctChapter)
            {
                var memberInfo = _party.GetMemberInfo(characterId);
                if (memberInfo != null)
                {
                    var partyLog = new PartyLogBattle(PartyLogType.BattleInstance, memberInfo.character.nickname, stageUid);
                    _partyLog.AddLog(partyLog);
                    OnUpdatePartyLog();
                }
            }
        }

        public void WorldBossFinish(string dungeonKey, string characterId, long totalDamage, bool stageResult)
        {
            RedisManager.Instance.SendWorldBossFinish(_roomId, characterId, dungeonKey, totalDamage, stageResult);
        }

        public void WorldBossDead(string dungeonKey)
        {
            var strArray = dungeonKey.Split('-');
            if (strArray.Length != 4)
            {
                throw new CorgiException("Invalid DungeonKey on WorldBossDead");
            }
            var dayNum = strArray[2];
            RedisManager.Instance.SendWorldBossDead(_roomId, dayNum);
            RedisManager.Instance.SaveWorldBossDeadTime(dayNum, CorgiTime.UtcNowULong);
        }
        
        public void RiftFinish(string dungeonKey, string dungeonId, string characterId, long totalDamage, bool stageResult)
        {
            RedisManager.Instance.SendRiftFinish(_roomId, dungeonId, characterId, dungeonKey, totalDamage, stageResult);
        }

        public void RiftDead(string dungeonId, string characterId)
        {
            RedisManager.Instance.SendRiftDead(_roomId, dungeonId, characterId);
        }
        
        public void ArenaFinish(string dungeonKey, string characterId, string targetId, string winnerId)
        {
            RedisManager.Instance.SendArenaFinish(_roomId, characterId, targetId, dungeonKey, winnerId);
        }
        

        public bool RequestData(string characterId, RequestParam param, string callbackName)
        {
            var paramList = new List<RequestParam>();
            paramList.Add(param);
            return RequestData(characterId, paramList, callbackName);
        }

        public bool RequestData(string characterId, List<RequestParam> paramList, string callbackName)
        {
            //var request = new RedisRequest(this, "OnLoadRequestData");
            var request = new RedisRequest(this, characterId,callbackName);
            foreach (var param in paramList)
            {
                if (param == null || param.RequestType == RedisRequestType.None ||
                    string.IsNullOrEmpty(param.RequestKey))
                {
                    continue;
                }

                var task = RedisTaskFactory.Create(param.RequestType, param.RequestKey);
                if (task == null)
                {
                    continue;
                }
                request.AddRedisTask(task);
            }

            if (request.Invoke())
            {
                //-요청카운트를 증가 시켰지만, 위의 invoke 내부에서는 비동기 처리이기 때문에, 사실 여기서 카운트를 올려봤자 의미가 없다.
                // 요청카운트가 유효하려면 redis 요청하기 직전에서 카운트를 올려야 한다.
                // 그 전에 에러가 나서 요청한게 아니라면, 카운트만 증가하고, 멈춤현상(카운트를 올리기만 하고 응답을 받아 차감을 못하니.. 무한 Waitng상태에 빠질수 있다. 구조적인 문제.)
                // 요청카운트가 차감되는 곳도 예외처리나 반환 하는 곳없이 반드시 호출하게 해야한다. 안그러면 역시 무한 waiting상태에 빠진다.
                _isRequestingCount++;
                return true;
            }

            return false;
        }

        private void OnLoadRequestData(RedisRequest redisRequest)
        {
            var taskList = redisRequest.GetRedisTasks();
            
            //bool isPartyClear = false;

            try
            {
                foreach (var curTask in taskList)
                {
                    switch (curTask.RequestType)
                    {
                        case RedisRequestType.RoomCoordinateInfo:
                        {
                            //string requestType = curTask.RequestType.ToString();

                            var thisTask = curTask as RedisTaskRoomCoordinateInfo;
                            if (thisTask == null)
                            {
                                var room = redisRequest.ThisObject() as Room;
                                if (null != room)
                                {
                                    string format = string.Format("(Request) {0} is null", curTask.RequestType.ToString());
                                    room.LoadErrorLog(format);
                                }

                                throw new CorgiException("invalid redis task : {0}", curTask.RequestType);
                            }

                            _charIds.Clear();
                            _charIds.AddRange(thisTask.CharacterIds);

                            foreach (var characterid in thisTask.CharacterIds)
                            {
                                CorgiCombatLog.Log(CombatLogCategory.User,$"OnLoadRequest [{curTask.RequestType}] completed.", characterid, thisTask.RoomId);
                            }

                            break;
                        }
                        case RedisRequestType.CharaterInfo:
                        {
                            var thisTask = curTask as RedisTaskCharacterInfo;
                            if (thisTask == null)
                            {
                                throw new CorgiException("invalid redis task({0})", curTask.RequestType);
                            }

                            var memberInfo = thisTask.MemberInfo;

                            _party.AddOrUpdate(memberInfo);

                            break;
                        }
                        case RedisRequestType.RoomInfo:
                        {
                            var thisTask = curTask as RedisTaskRoomInfo;
                            if (thisTask == null)
                            {
                                throw new CorgiException("invalid redis task({0})", curTask.RequestType);
                            }

                            _party.SetDungeonState(thisTask.DungeonUid, thisTask.StageUid);

                            if (_adventureDungeon != null && _adventureDungeon.Uid != _party.DungeonUid)
                            {
                                // reload
                                if (_adventureDungeon.Load(_party.DungeonUid) == false)
                                {
                                    CorgiLog.LogError("cant create dungeon : {0}\n",
                                        GameDataManager.Instance.GetStrByUid(_party.DungeonUid));
                                    return;
                                }

                                _adventureDungeon.UpdateStage(_party.StageUid);
                            }

                            break;
                        }
                        
                        case RedisRequestType.RoomStatus:
                        {
                            var thisTask = curTask as RedisTaskRoomStatus;
                            if (thisTask == null)
                            {
                                throw new CorgiException("invalid redis task({0})", curTask.RequestType);
                            }

                            _buffEndTimestamp = thisTask.BuffEndTimestamp;

                            break;
                        }
                        case RedisRequestType.RoomDeckInfo:
                        {
                            var thisTask = curTask as RedisTaskRoomDeckInfo;
                            if (thisTask == null)
                            {

                                throw new CorgiException("invalid redis task({0})", curTask.RequestType);
                            }

                            _party.SetCoPartyDeck(thisTask.DeckInfo);

                            break;
                        }

                        case RedisRequestType.PartyLogAll:
                        {
                            var thisTask = curTask as RedisTaskPartyLogAll;
                            if (thisTask == null)
                            {
                                throw new CorgiException("invalid redis task({0})", curTask.RequestType);
                            }

                            if (thisTask.PartyLog != null)
                            {
                                _partyLog = thisTask.PartyLog;
                            }

                            if (thisTask.PartyChatting != null)
                            {
                                _partyChatting = thisTask.PartyChatting;
                            }

                            break;
                        }
                        
                        case RedisRequestType.GetRiftInfo:
                        {
                            var thisTask = curTask as RedisTaskGetRiftInfo;
                            if (thisTask == null)
                            {
                                throw new CorgiException("invalid redis task({0})", curTask.RequestType);
                            }

                            if (thisTask.SharedRift != null)
                            {
                                _rift = new Rift();
                                _rift.InitRift(thisTask.SharedRift);
                            }

                            break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                CorgiLog.Log(CorgiLogType.Fatal, "Occur exception[{0}] in OnLoadRequestData", e.ToString());
                
                //-don't call return here, "_isRequestingCount--" have to call. as below 
            }

            _isRequestingCount--;//-have to call
        }

        public void WorldBossDamage(string dungeonKey, string characterId, long damage)
        {
            var redisTask =
                new RedisTaskWorldBossDamage(dungeonKey , damage, RedisRequestType.WorldBossDamage);

            var request = new RedisRequest(this, characterId, "OnWorldBossDamage");
            
            request.AddRedisTask(redisTask);

            request.Invoke();

        }

        void OnWorldBossDamage_Serialized(RedisRequest redisRequest)
        {
            string characterId = redisRequest.CharacterId;

            if (!_worldBossDungeonMap.ContainsKey(characterId))
            {
                return;
            }
            
            var dungeon = _worldBossDungeonMap[characterId];
            if (dungeon == null)
            {
                return;
            }
            
            var taskList = redisRequest.GetRedisTasks();
            foreach (var curTask in taskList)
            {
                var thisTask = curTask as RedisTaskWorldBossDamage;
                if (thisTask == null)
                {
                    throw new CorgiException("invalid redis task({0})", curTask.RequestType);
                }

                var newHP = thisTask.CurHP;
                var myDamage = thisTask.Damage;

                dungeon.OnWorldBossDamage(newHP, myDamage);
            }
        }

        public List<Unit> CreateUnitList(Dungeon dungeon)
        {
            // create gameLogic instances
            var characters = new List<Unit>();
            
            var leaderSkillUids = new List<ulong>();
            
            foreach (var userInfo in _party.MemberInfos)
            {
                // characters
                var unit = CreateCharacter(dungeon, userInfo.character.dbId);
                if (unit == null)
                {
                    CorgiLog.LogError("invalid character info");
                    continue;
                }
                
                characters.Add(unit);
                
                if (leaderSkillUids.Count == 0 && unit.ActiveSkills.Count == 4)
                {
                    leaderSkillUids.Add(unit.ActiveSkills[0].Uid);
                    leaderSkillUids.Add(unit.ActiveSkills[1].Uid);
                    leaderSkillUids.Add(unit.ActiveSkills[2].Uid);
                    leaderSkillUids.Add(unit.ActiveSkills[3].Uid);
                }
            }

            var finalList = dungeon.FillNpc(characters);

            return finalList;
        }

        public void UpdateUnitList(Dungeon dungeon)
        {
            var characters = dungeon.CharList;
            
            foreach (var userInfo in _party.MemberInfos)
            {
                // characters
                Unit thisUnit = null;

                if (userInfo.character == null || userInfo.character.dbId == null)
                {
                    continue;
                }

                var charInfo = userInfo.character;
                
                foreach (var character in characters)
                {
                    if (character != null && character.DBId == userInfo.character.dbId)
                    {
                        thisUnit = character;
                        break;
                    }
                }
                
                if (thisUnit != null)
                {
                    if (thisUnit.Load(charInfo) == false)
                    {
                        CorgiLog.LogError("invalid character info for update unit");
                    }
                }
            }
            
        }
        
        public List<Unit> CreateArenaUnitList(Dungeon dungeon)
        {
            // create gameLogic instances
            var characters = new List<Unit>();
            
            foreach (var userInfo in _party.MemberInfos)
            {
                // characters
                var unit = new CharacterArena(dungeon);
                
                if (unit.Load(userInfo.character) == false)
                {
                    CorgiLog.LogError("invalid character info");
                    continue;
                }
                
                characters.Add(unit);
            }

            return characters;
        }
        
        public List<Unit> CreateArenaEnemyList(Dungeon dungeon)
        {
            var arenaDungeon = dungeon as DungeonArena;
            
            if (arenaDungeon == null)
            {
                return null;
            }
            
            var enemyList = new List<Unit>();
            var characterId = arenaDungeon.CharacterId;

            foreach (var userInfo in arenaDungeon.SharedEnemyList.Values)
            {
                if (userInfo == null)
                {
                    continue;
                }

                var unit = new CharacterArena(dungeon);
                
                if (unit.Load(userInfo.character) == false)
                {
                    CorgiLog.LogError("invalid character info");
                    continue;
                }
                
                unit.SetCombatSide(CombatSideType.Enemy);
                
                var deck = GetSoloDefenceDeck(characterId, arenaDungeon.TargetId, unit);
                if (unit.LoadCombatSetting(deck) == false)
                {
                    CorgiCombatLog.LogError(CombatLogCategory.Dungeon, "Failed to LoadCombatSetting {0}", unit.DBId);
                    continue;
                }
                
                enemyList.Add(unit);
            }

            return enemyList;
        }
        private void OnLoadRequestData_Serialized(RedisRequest redisRequest)
        {
            OnLoadRequestData(redisRequest);
        }

        public bool IsRequestCompleted()
        {
            if (0 > _isRequestingCount)
            {
                CorgiLog.Log(CorgiLogType.Fatal, "Critical!, Something is wrong. redis request/response unmatched!!!");
            }
            
            return (0 == _isRequestingCount);
        }

        // public bool IsUpdatedParty()
        // {
        //     return _isUpdatedParty;
        // }

        public void LoadErrorLog(string format)
        {
            foreach (var conn in _connectionList)
            {
                CorgiCombatLog.LogError(CombatLogCategory.User,format, conn.CharacterId, conn.RoomId, true);
            }
        }

        
        // check no connection hunting
        // if win, destroy
        // else if lose, do nothing
        public void CheckNoConnectionHunting(DungeonState dungeonState, bool isChallenging)
        {
            if (RoomState.NoConnectionHunting != _roomState)
            {
                return;
            }

            CorgiCombatLog.Log(CombatLogCategory.Party, "<Notice><Result> [{0}] with [{1}]", dungeonState.ToString(), isChallenging ? ("Challenge mode") : ("Normal mode"));
            
            //-let be played if challenging.   
            if (true == isChallenging)
            {
                return;//-never come..
            }
            
            //-finish this room if win.
            if (DungeonState.Win == dungeonState)  
            {
                var remainMs = CombatServerConfigConst.EMPTY_ROOM_FINISH_ALIVE_TIME_MS;
                CorgiCombatLog.Log(CombatLogCategory.Party, "Room mode[{0}] will be stop and close this room after [{1}] ms. result[{2}]", _roomState.ToString(), remainMs, dungeonState.ToString());
            
                StartDestoryTimer(remainMs);
            }                    
        }

        private void StartDestoryTimer(ulong remainMs)
        {
            _isLoadRoomInfos = true;//-_isLoadRoomInfos 가 true 가 되지 않은 상태라면 _destroyTimer.IsOver() 를 체크하지 못하는 문제가 있음.
                                    //    구체적으로 말하자면, _isLoadRoomInfos 가 true로 하기 직전에 접속이 끊기는 경우. 
            _destroyTimer.StartTimer(remainMs);
            
            //CorgiLog.Log(CorgiLogType.Warning, "Start destroy timer... remain ms[{0}]", remainMs);
        }

        private void OnConnected(CorgiServerConnection conn, SharedMemberInfo memberInfo)
        {
            // update party status
            var memberStatus = _partyStatus.OnConnected(conn.CharacterId);
            
            OnUpdatePartyMemberStatus(memberStatus);

            var partyLog = new PartyLogUser(PartyLogType.Connect, memberInfo.character.nickname);
            _partyLog.AddLog(partyLog);
            OnUpdatePartyLog();

            if (_rift != null)
            {
                var rift = _rift.GetSharedRiftInfo();
                if (rift != null)
                {
                    conn.SC_UPDATE_RIFT_INFO(rift);
                }
            }

            conn.UserState = UserState.Active;
            
            ChattingManager.Instance.SerializeMethod("AddConnection", conn, memberInfo.leagueSerial); 
        }

        private void OnUpdatePartyMemberStatus(PartyMemberStatus memberStatus)
        {
            if (memberStatus == null)
            {
                return;
            }
            foreach (var thisConn in _connectionList)
            {
                if (thisConn == null || thisConn.CharacterId == memberStatus.CharacterId)
                {
                    continue;
                }

                thisConn.SC_UPDATE_PARTY_MEMBER_STATUS(_roomId, memberStatus);
            }
            
        }
        
    }
}    