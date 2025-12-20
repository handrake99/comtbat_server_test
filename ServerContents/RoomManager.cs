using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;

using IdleCs.Network.NetLib;

using IdleCs.GameLogic;
using IdleCs.ServerCore;
using IdleCs.Utils;

using IdleCs.CombatServer;
using IdleCs.GameLogic.SharedInstance;
using IdleCs.Managers;
using IdleCs.Logger;
using IdleCs.ServerSystem;
using IdleCs.ServerUtils;

namespace IdleCs.ServerContents
{
    public partial class RoomManager : CorgiServerObjectSingleton<RoomManager>
    {
        private int _removedRoomCount = 0; // for gc
        private ulong _updatedTimestamp = 0;
        
        protected override bool Init()
        {
            base.Init();
            
            _roomMap = new ConcurrentDictionary<string, Room>();

            return true;
        }

        Room GetRoom(string roomId)
        {
            if (string.IsNullOrEmpty(roomId))
            {
                return null;
            }

            Room getRoom = null;
            if (false == _roomMap.TryGetValue(roomId, out getRoom))
            {
                return null;
            }

            return getRoom;
        }

        bool HasRoom(string roomId)
        {
            if (string.IsNullOrEmpty(roomId))
            {
                return false;
            }
            
            return _roomMap.ContainsKey(roomId);
        }

        //void JoinAdventure_Serialized(CorgiServerConnection conn, string roomId))
        void JoinAdventure_Serialized(CorgiServerConnection conn, string roomId, bool joinExistRoom)
        {
            if (string.IsNullOrEmpty(roomId))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"room id is null", conn.CharacterId, "", true);
                throw new CorgiException("invalid room id for create");
            }
    
            if (false == CheckValidJoin(conn.CharacterId, roomId, joinExistRoom))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"Error, exist room unmatched when join room.", conn.CharacterId, roomId, true);
                
                conn.SC_JOIN_ADVENTURE(
                    CorgiErrorCode.JoinRoomUnmatched, roomId,conn.CharacterId, null, null, null, null);
                conn.Disconnect();
                return;
            }                
            
            JoinRoom(conn, roomId);
        }
        
        void JoinInstance_Serialized(CorgiServerConnection conn, string roomId, string characterId, JObject dungeonInfo)
        {
            if (string.IsNullOrEmpty(roomId) || string.IsNullOrEmpty(characterId))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"room id is null", conn.CharacterId, "", true);

                conn.SC_JOIN_INSTANCE(CorgiErrorCode.FailedToJoinInvalidRoom, roomId, characterId, null);
                return;
            }
            
            Room room = null;
            if (_roomMap.TryGetValue(roomId, out room) == false)
            {
                conn.SC_JOIN_INSTANCE(CorgiErrorCode.FailedToJoinInvalidRoom, roomId, characterId, null);
                return;
            }
            
            room.SerializeMethod("JoinInstance", dungeonInfo);
            
        }
        
        void JoinWorldBoss_Serialized(CorgiServerConnection conn, string roomId, string characterId, string dungeonKey)
        {
            if (string.IsNullOrEmpty(roomId) || string.IsNullOrEmpty(characterId))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"room id is null", conn.CharacterId, "", true);
                throw new CorgiException("invalid room id for create");
            }
            
            Room room = null;
            if (_roomMap.TryGetValue(roomId, out room) == false)
            {
                CorgiCombatLog.Log(CombatLogCategory.System, "Failed to Join WorldBoss Start({0}) Because of NoRoom", roomId);
                return;
            }
            
            room.SerializeMethod("JoinWorldBoss", characterId, dungeonKey);
            
        }
        
        void JoinRift_Serialized(CorgiServerConnection conn, string roomId, string characterId, string dungeonKey)
        {
            if (string.IsNullOrEmpty(roomId) || string.IsNullOrEmpty(characterId))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"room id is null", conn.CharacterId, "", true);
                throw new CorgiException("invalid room id for create");
            }
            
            Room room = null;
            if (_roomMap.TryGetValue(roomId, out room) == false)
            {
                CorgiCombatLog.Log(CombatLogCategory.System, "Failed to Join WorldBoss Start({0}) Because of NoRoom", roomId);
                return;
            }
            
            room.SerializeMethod("JoinRift", characterId, dungeonKey);
            
        }
        
        void JoinArena_Serialized(CorgiServerConnection conn, string roomId, string characterId, string dungeonKey, string targetId)
        {
            if (string.IsNullOrEmpty(roomId) || string.IsNullOrEmpty(characterId))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"room id is null", conn.CharacterId, "", true);
                throw new CorgiException("invalid room id for create");
            }
            
            Room room = null;
            if (_roomMap.TryGetValue(roomId, out room) == false)
            {
                CorgiCombatLog.Log(CombatLogCategory.System, "Failed to Join Arena Start({0}) Because of NoRoom", roomId);
                return;
            }
            
            room.SerializeMethod("JoinArena", characterId, dungeonKey, targetId);
            
        }

        void OnStageCompleted_Serialized(string roomId, ulong stageUid)
        {
            if (string.IsNullOrEmpty(roomId))
            {
                throw new CorgiException("invalid room id for OnStageCompleted");
            }

            Room room = null;
            if (_roomMap.TryGetValue(roomId, out room) == false)
            {
                // set error
                CorgiCombatLog.Log(CombatLogCategory.System,"failed to StageCompleted ({0})", roomId);
                return;
            }
            
            //CorgiLog.LogLine("\nStageCompleted Request {0}", roomId);
            room.SerializeMethod("OnStageCompleted", stageUid);
            
            //CorgiLog.LogLine("\nStageCompleted Created({0})", roomId);
            
        }
        
        
        void OnChallengeStart_Serialized(string roomId, string characterId, ulong stageUid)
        {

            if (string.IsNullOrEmpty(roomId) || string.IsNullOrEmpty(characterId) || stageUid <= 0)
            {
                throw new CorgiException("invalid room id for create");
            }

            Room room = null;
            if (_roomMap.TryGetValue(roomId, out room) == false)
            {
                CorgiCombatLog.Log(CombatLogCategory.System,"failed to challenge start. roomid[{0}], characterid[{1}]", roomId, characterId);
                return;
            }
            
            room.SerializeMethod("OnChallengeStart", characterId, stageUid);
        }
        void OnChallengeCompleted_Serialized(string roomId, string characterId, ulong stageUid, bool challengeResult)
        {
            if (string.IsNullOrEmpty(roomId))
            {
                throw new CorgiException("invalid room id for OnChallengeCompleted");
            }

            Room room = null;
            if (_roomMap.TryGetValue(roomId, out room) == false)
            {
                // set error
                CorgiCombatLog.Log(CombatLogCategory.System,"failed to OnChallengeCompleted ({0})", roomId);
                return;
            }
            
            //CorgiLog.LogLine("\nOnChallengeCompleted Request {0}", roomId);
            room.SerializeMethod("OnChallengeCompleted", characterId, stageUid, challengeResult);
            
            //CorgiLog.LogLine("\nOnChallengeCompleted Created({0})", roomId);
            
        }
        
        void OnAutoHuntingStart_Serialized(string roomId, string characterId, ulong stageUid, bool serialBoss, ulong buffEndTimestamp)
        {
            if (string.IsNullOrEmpty(roomId))
            {
                throw new CorgiException("invalid room id for OnAutoHuntingStart");
            }

            Room room = null;
            if (_roomMap.TryGetValue(roomId, out room) == false)
            {
                // set error
                CorgiCombatLog.Log(CombatLogCategory.System,"failed to OnAutoHuntingStart ({0})", roomId);
                return;
            }
            
            room.SerializeMethod("OnAutoHuntingStart", characterId, stageUid, serialBoss, buffEndTimestamp);
        }
        
        void OnInstanceDungeonStart_Serialized(string roomId, JObject json)
        {

            if (string.IsNullOrEmpty(roomId) )
            {
                throw new CorgiException("invalid parameter for instance dungeon start");
            }

            Room room = null;
            if (_roomMap.TryGetValue(roomId, out room) == false)
            {
                CorgiCombatLog.Log(CombatLogCategory.System, "Failed to Challenge Start({0}) Because of NoRoom", roomId);
                return;
            }
            
            room.SerializeMethod("OnInstanceDungeonStart", json);
            
            //CorgiCombatLog.Log(CombatLogCategory.System,"OnInstanceDungeon Started({0}/{1})",  roomId, json);
        }
        void OnInstanceDungeonCompleted_Serialized(string roomId, string characterId, string dungeonId, ulong dungeonUid, ulong stageUid)
        {
            if (string.IsNullOrEmpty(roomId) || string.IsNullOrEmpty(characterId) || string.IsNullOrEmpty(dungeonId)|| dungeonUid <= 0 || stageUid <= 0)
            {
                throw new CorgiException("invalid room id for OnInstanceDungeonCompleted");
            }

            Room room = null;
            if (_roomMap.TryGetValue(roomId, out room) == false)
            {
                // set error
                CorgiCombatLog.Log(CombatLogCategory.System,"failed to OnInstanceDungeonCompleted ({0})", roomId);
                return;
            }
            
            room.SerializeMethod("OnInstanceDungeonCompleted", characterId, dungeonId, dungeonUid, stageUid);
            
            CorgiCombatLog.Log(CombatLogCategory.System,"OnChallengeCompleted Created({0}/{1}/{2}/{3}/{4})", roomId, characterId, dungeonId, dungeonUid, stageUid);
            
        }
        void OnInstanceDungeonStop_Serialized(string roomId, string characterId, string dungeonId, ulong dungeonUid, ulong stageUid)
        {

            if (string.IsNullOrEmpty(roomId) 
                || string.IsNullOrEmpty(characterId) 
                || string.IsNullOrEmpty(dungeonId) 
                || dungeonUid <= 0 || stageUid <= 0)
            {
                throw new CorgiException("invalid parameter for instance dungeon stop");
            }

            Room room = null;
            if (_roomMap.TryGetValue(roomId, out room) == false)
            {
                CorgiCombatLog.Log(CombatLogCategory.System,"failed to instance dungeon stop({0})", roomId);
                return;
            }
            
            room.SerializeMethod("OnInstanceDungeonStop", characterId, dungeonId, dungeonUid, stageUid);
            
            CorgiCombatLog.Log(CombatLogCategory.System,"OnInstanceDungeon Stop ({0}/{1}/{2}/{3}/{4})",  roomId, characterId, dungeonId, dungeonUid, stageUid);
        }
        
        void OnWorldBossCompleted_Serialized(string roomId, string characterId, string dungeonKey)
        {
            if (string.IsNullOrEmpty(roomId) || string.IsNullOrEmpty(characterId) || string.IsNullOrEmpty(dungeonKey))
            {
                throw new CorgiException("invalid room id for OnWorldBossCompleted");
            }

            Room room = null;
            if (_roomMap.TryGetValue(roomId, out room) == false)
            {
                // set error
                CorgiCombatLog.Log(CombatLogCategory.System,"failed to OnWorldBossDungeonCompleted ({0})", roomId);
                return;
            }
            
            room.SerializeMethod("OnWorldBossCompleted", characterId, dungeonKey);
            
            CorgiCombatLog.Log(CombatLogCategory.System,"OnWorldBossCompleted Created({0}/{1}/{2})", roomId, characterId, dungeonKey);
        }
        
        void OnWorldBossStop_Serialized(string roomId, string characterId, string dungeonKey)
        {
            if (string.IsNullOrEmpty(roomId) || string.IsNullOrEmpty(characterId) || string.IsNullOrEmpty(dungeonKey))
            {
                throw new CorgiException("invalid room id for OnWorldBossStop");
            }

            Room room = null;
            if (_roomMap.TryGetValue(roomId, out room) == false)
            {
                // set error
                CorgiCombatLog.Log(CombatLogCategory.System,"failed to OnWorldBossDungeonStop ({0})", roomId);
                return;
            }
            
            room.SerializeMethod("OnWorldBossStop", characterId, dungeonKey);
            
            CorgiCombatLog.Log(CombatLogCategory.System,"OnWorldBossStop Created({0}/{1}/{2})", roomId, characterId, dungeonKey);
        }
        
        void OnRiftOpen_Serialized(string roomId, SharedRift sharedRift, string characterId)
        {
            if (string.IsNullOrEmpty(roomId) )
            {
                throw new CorgiException("invalid room id for OnRiftOpen");
            }

            Room room = null;
            if (_roomMap.TryGetValue(roomId, out room) == false)
            {
                // set error
                CorgiCombatLog.Log(CombatLogCategory.System,"failed to OnRiftOpen ({0})", roomId);
                return;
            }
            
            room.SerializeMethod("OnRiftOpen", sharedRift, characterId);
            
            CorgiCombatLog.Log(CombatLogCategory.System,"OnRiftOpen Created({0})", roomId);
        }
        
        void OnRiftCompleted_Serialized(string roomId, string characterId, string dungeonKey)
        {
            if (string.IsNullOrEmpty(roomId) || string.IsNullOrEmpty(characterId) /*|| string.IsNullOrEmpty(dungeonKey)*/)
            {
                throw new CorgiException("invalid room id for OnRiftCompleted");
            }

            Room room = null;
            if (_roomMap.TryGetValue(roomId, out room) == false)
            {
                // set error
                CorgiCombatLog.Log(CombatLogCategory.System,"failed to OnRiftCompleted ({0})", roomId);
                return;
            }
            
            room.SerializeMethod("OnRiftCompleted", characterId, dungeonKey);
            
            CorgiCombatLog.Log(CombatLogCategory.System,"OnRiftCompleted Created({0}/{1}/{2})", roomId, characterId, dungeonKey);
        }
        
        void OnRiftStop_Serialized(string roomId, string characterId, string dungeonKey)
        {
            if (string.IsNullOrEmpty(roomId) || string.IsNullOrEmpty(characterId) /*|| string.IsNullOrEmpty(dungeonKey)*/)
            {
                throw new CorgiException("invalid room id for OnRiftStop");
            }

            Room room = null;
            if (_roomMap.TryGetValue(roomId, out room) == false)
            {
                // set error
                CorgiCombatLog.Log(CombatLogCategory.System,"failed to OnRiftStop ({0})", roomId);
                return;
            }
            
            room.SerializeMethod("OnRiftStop", characterId, dungeonKey);
            
            CorgiCombatLog.Log(CombatLogCategory.System,"OnRiftStop Created({0}/{1}/{2})", roomId, characterId, dungeonKey);
        }

        void OnArenaCompleted_Serialized(string roomId, string characterId, string dungeonKey)
        {
            if (string.IsNullOrEmpty(roomId) || string.IsNullOrEmpty(characterId) /*|| string.IsNullOrEmpty(dungeonKey)*/)
            {
                throw new CorgiException("invalid room id for OnArenaCompleted");
            }

            Room room = null;
            if (_roomMap.TryGetValue(roomId, out room) == false)
            {
                // set error
                CorgiCombatLog.Log(CombatLogCategory.System,"failed to OnArenaCompleted ({0})", roomId);
                return;
            }
            
            room.SerializeMethod("OnArenaCompleted", characterId, dungeonKey);
            
            CorgiCombatLog.Log(CombatLogCategory.System,"OnArenaCompleted Created({0}/{1}/{2})", roomId, characterId, dungeonKey);
        }
        
        void OnArenaStop_Serialized(string roomId, string characterId, string dungeonKey)
        {
            if (string.IsNullOrEmpty(roomId) || string.IsNullOrEmpty(characterId) /*|| string.IsNullOrEmpty(dungeonKey)*/)
            {
                throw new CorgiException("invalid room id for OnArenaStop");
            }

            Room room = null;
            if (_roomMap.TryGetValue(roomId, out room) == false)
            {
                // set error
                CorgiCombatLog.Log(CombatLogCategory.System,"failed to OnArenaStop ({0})", roomId);
                return;
            }
            
            room.SerializeMethod("OnArenaStop", characterId, dungeonKey);
            
            CorgiCombatLog.Log(CombatLogCategory.System,"OnArenaStop Created({0}/{1}/{2})", roomId, characterId, dungeonKey);
        }
        
        void OnPartyJoin_Serialized(string roomId, string characterId)
        {
            if (string.IsNullOrEmpty(roomId) 
                || string.IsNullOrEmpty(characterId) )
            {
                CorgiCombatLog.Log(CombatLogCategory.System,"invalid parameter for instance party join");
                return;
            }
            
            Room room = null;
            if (_roomMap.TryGetValue(roomId, out room) == false)
            {
                // party Join 시 해당 파티가 플레이중이 아니면 room이 없을수 있다.
                return;
            }
            
            room.SerializeMethod("OnPartyJoin", characterId);

            CorgiCombatLog.Log(CombatLogCategory.System,"OnParty Join ({0})", roomId, characterId);
        }
        
        void OnPartyLeave_Serialized(string roomId, string characterId)
        {
            if (string.IsNullOrEmpty(roomId) 
                || string.IsNullOrEmpty(characterId) )
            {
                CorgiCombatLog.Log(CombatLogCategory.System,"invalid parameter for instance party leave");
                return;
            }
            
            Room room = null;
            if (_roomMap.TryGetValue(roomId, out room) == false)
            {
                CorgiLog.Log(CorgiLogType.Fatal, "[party] can't find room[{0}] when party leaving. characterid[{1}]", roomId, characterId);
                return;
                //throw new CorgiException("failed to instance party leave({0})", roomId);
            }
            
            room.SerializeMethod("OnPartyLeave", characterId);
            
            CorgiLog.LogLine("OnParty leave ({0})",  roomId);
        }
        
        void OnPartyExile_Serialized(string roomId, string characterId)
        {
            if (string.IsNullOrEmpty(roomId) 
                || string.IsNullOrEmpty(characterId) )
            {
                CorgiCombatLog.Log(CombatLogCategory.System,"invalid parameter for instance party exile");
                return;
            }
            
            Room room = null;
            if (_roomMap.TryGetValue(roomId, out room) == false)
            {
                // party Join 시 해당 파티가 플레이중이 아니면 room이 없을수 있다.
                return;
            }
            
            room.SerializeMethod("OnPartyExile", characterId);

            CorgiCombatLog.Log(CombatLogCategory.System,"OnParty Exile ({0})", roomId, characterId);
        }

        void OnUpdatePartyMemberStatus_Serialized(CorgiServerConnection conn, PartyMemberStatus memberStatus)
        {
            if (conn == null || memberStatus == null)
            {
                return;
            }

            var roomId = conn.RoomId;
            var characterId = conn.CharacterId;
            
            if (string.IsNullOrEmpty(roomId) 
                || string.IsNullOrEmpty(characterId) )
            {
                throw new CorgiException("invalid parameter for instance UpdatePartyMemeber");
            }
            
            Room room = null;
            if (_roomMap.TryGetValue(roomId, out room) == false)
            {
                CorgiLog.Log(CorgiLogType.Fatal, "[party] can't find room[{0}] when update party status. characterid[{1}]", roomId, characterId);
                return;
                //throw new CorgiException("failed to instance party leave({0})", roomId);
            }
            
            room.SerializeMethod("OnUpdatePartyMemberStatus", memberStatus);
            
            CorgiCombatLog.Log(CombatLogCategory.System,"OnParty leave ({0})",  roomId);
        }
        
        void OnEventAcquireSkillItem_Serialized(string roomId, string characterId, long skillItemUid)
        {
            if (string.IsNullOrEmpty(roomId) 
                || string.IsNullOrEmpty(characterId) 
                || skillItemUid == 0)
            {
                throw new CorgiException("invalid parameter for event acquire skill");
            }
            
            Room room = null;
            if (_roomMap.TryGetValue(roomId, out room) == false)
            {
                //CorgiLog.Log(CorgiLogType.Fatal, "[party] can't find room[{0}] when Event Acquire SkillItem. characterid[{1}]", roomId, characterId);
                RedisManager.Instance.SendHackLog(HackType.NoConnected_CombatServer_AcquireSkill, roomId, characterId);
                return;
            }
            
            room.SerializeMethod("OnEventAcquireSkillItem", characterId, skillItemUid);
            
            CorgiLog.LogLine("OnEvent Acquire Skill ({0})",  roomId);
        }

        void OnEventAcquireEquipItem_Serialized(string roomId, string characterId, long equipUid)
        {
            if (string.IsNullOrEmpty(roomId) 
                || string.IsNullOrEmpty(characterId) 
                || equipUid == 0)
            {
                throw new CorgiException("invalid parameter for event acquire equip");
            }
            
            Room room = null;
            if (_roomMap.TryGetValue(roomId, out room) == false)
            {
                //CorgiLog.Log(CorgiLogType.Fatal, "[party] can't find room[{0}] when Event Acquire EquipItem. characterid[{1}]", roomId, characterId);
                RedisManager.Instance.SendHackLog(HackType.NoConnected_CombatServer_AcquireEquip, roomId, characterId);
                return;
            }
            
            room.SerializeMethod("OnEventAcquireEquipItem", characterId, equipUid);
            
            CorgiCombatLog.Log(CombatLogCategory.System,"OnEvent Acquire Equip ({0})",  roomId);
        }
        
        void OnRoomStatus_Serialized(string roomId)
        {
            if (string.IsNullOrEmpty(roomId) )
            {
                throw new CorgiException("invalid parameter for event room status");
            }
            
            Room room = null;
            if (_roomMap.TryGetValue(roomId, out room) == false)
            {
                CorgiLog.LogLine("[Room] no Room Status : {0}", roomId);
                return;
            }

            CorgiLog.Log(CorgiLogType.Info, "--------------------");
            CorgiLog.Log(CorgiLogType.Info, "Room Status");
            CorgiLog.Log(CorgiLogType.Info, "RoomId : {0}", roomId);
            CorgiLog.Log(CorgiLogType.Info, "State : {0}", room.RoomState.ToString());
            CorgiLog.Log(CorgiLogType.Info, "Count : {0}", room.ConnectionCount);
            CorgiLog.Log(CorgiLogType.Info, "StageComplete : {0}", room.StageCompletedCount);
            CorgiLog.Log(CorgiLogType.Info, "--------------------");
            
            CorgiCombatLog.Log(CombatLogCategory.System,"OnEvent Acquire Equip ({0})",  roomId);
        }
        
        void OnRoomKill_Serialized(string roomId)
        {
            if (string.IsNullOrEmpty(roomId) )
            {
                throw new CorgiException("invalid parameter for event room status");
            }
            
            Room room = null;
            if (_roomMap.TryGetValue(roomId, out room) == false)
            {
                CorgiLog.LogLine("[Room] no Room Status : {0}", roomId);
                return;
            }

            CorgiLog.Log(CorgiLogType.Info, "--------------------");
            CorgiLog.Log(CorgiLogType.Info, "Room Kill");
            CorgiLog.Log(CorgiLogType.Info, "RoomId : {0}", roomId);
            CorgiLog.Log(CorgiLogType.Info, "State : {0}", room.RoomState.ToString());
            CorgiLog.Log(CorgiLogType.Info, "Count : {0}", room.ConnectionCount);
            CorgiLog.Log(CorgiLogType.Info, "--------------------");
            
            room.SerializeMethod("OnRoomKill");
            
            CorgiCombatLog.Log(CombatLogCategory.System,"OnEvent Acquire Equip ({0})",  roomId);
        }
        
        
        void OnClose_Serialized(CorgiServerConnection conn)
        {
            var roomId = conn.RoomId;
            
            if (string.IsNullOrEmpty(roomId))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"Warning!, OnClose at RoomManager, room id is null", conn.CharacterId, "");
                return;
            }

            Room room = null;
            if (false == _roomMap.TryGetValue(roomId, out room))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"Warning!, OnClose at RoomManager, can't find room", conn.CharacterId, roomId);
                return;
            }
            
            room.SerializeMethod("OnClose", conn);
        }

        void Tick_Serialized()
        {
            var removedList = new List<string>();
            
            foreach (var room in _roomMap.Values)
            {
                if (room == null)
                {
                    continue;
                }
                
                if (room.HaveToDestroy())
                {
                    removedList.Add(room.RoomId);
                    continue;
                }

                if (room.RoomState == RoomState.Running)
                {
                    if (room.LastTickTimestamp + CorgiLogicConst.MaxRoomTickTime < CorgiTime.UtcNowULong)
                    {
                        // 해당 방이 오랫동안 tick 이 안불리고 있음
                        removedList.Add(room.RoomId);
                        LogHelper.LogRoom(LogType.RoomNoTick, room.RoomId, String.Empty, String.Empty, "No Tick");
                        continue;
                    }
                }

                ThreadPool.QueueUserWorkItem(TickRoomCallback, room);
            }

            foreach (var roomId in removedList)
            {
                Room room;
                if (true == _roomMap.TryRemove(roomId, out room))
                {
                    room?.Destroy();
                    
                    StatDataManager.Instance.Decrement(StatisticType.RoomCount, 1);
                    StatDataManager.Instance.Increment(StatisticType.UserOut, 1);
                }
                else
                {
                    LogHelper.LogRoom(LogType.RoomFailedForceRemove, room.RoomId, String.Empty, String.Empty, "Failed Force to remove room");
                }
                
            }

            // var curTimestamp = CorgiTime.NowULong;
            //
            // if (_updatedTimestamp + CombatServerConfigConst.ROOM_STATE_UPDATE_INTERVAL < curTimestamp)
            // {
            //     // do update room list
            //     var json = new JArray();
            //     foreach (var room in _roomMap.Values)
            //     {
            //         if (room == null)
            //         {
            //             continue;
            //         }
            //         
            //         if (room.HaveToDestroy())
            //         {
            //             removedList.Add(room.RoomId);
            //             continue;
            //         }
            //
            //         json.Add(room.RoomId);
            //
            //     }
            //     //RedisManager.Instance.SendRoomList(json);
            //     _updatedTimestamp = curTimestamp;
            // }
            
            AliveSignalManager.Instance.Process();
            TestHolderManager.Instance.Process();
            
            //GC.Collect(0);

            if (StressTestManager.Instance.IsStressTest)
            {
                // report
                // CorgiLog.Log(CorgiLogType.Info, $"Room Count : {_roomMap.Count}");
                // CorgiLog.Log(CorgiLogType.Info, $"Conn Count : {ChattingManager.Instance.ConnectionCount}");
            }
        }

        void TickRoomCallback(Object state)
        {
            Room room = (Room) state;
            
            room.SerializeMethod("Tick");
        }

        void Ping_Serialized(CorgiServerConnection conn, int id, string message)
        {
            CorgiLog.LogLine("\ninvoke method with ({0}), ({1})\n", id, message);
            
            conn.SC_PING(id, message);
        }

        void PongFromRedis_Serialized()
        {
            if (false == AliveSignalManager.Instance.DelayCheck())
            {
                ForceDestroy();
                return;
            }
            
            //AliveSignalManager.Instance.PongFromRedis(_roomMap);
            AliveSignalManager.Instance.PongFromRedis();
        }

        private void ForceDestroy()
        {
            //-destroy and clear, each room
            foreach (var keyValuePair in _roomMap)
            {
                keyValuePair.Value?.DoDestroy((int)Room.RoomDestroyReason.ServerGetProblem);
            }
        }

        private bool CheckValidJoin(string characterId, string roomId, bool joinExistRoom)
        {
            var room = GetRoom(roomId);
            if ((null != room) && (RoomState.WillBeDestroy == room.RoomState)) 
            {
                CorgiCombatLog.Log(CombatLogCategory.User,$"Error, CheckValidJoin failed. this room will be destroy", characterId, roomId, true);
                return false;   
            }
            
            bool existRoom = HasRoom(roomId);
            string userKnew = joinExistRoom ? "User have knew room exist." : "User have knew there are no room.";
            string serverKnew = existRoom ? "Server has room" : "Server has no room";


            if ((true == joinExistRoom) && (false == existRoom))//-here
            {
                //-A유저가 Party를 떠났음. 웹에는 A 에게 새로운 RoomId를 발급하고. 웹에는 RoomId가 있다고 판단하기 때문에 A는 joinExistRoom 을 true 로 설정하고 JoinAdventure
                // 그런데, 전투서버의 RoomManager 에는 해당 RoomId를 가진 Room이 없다.(당연히) 
                // 그러므로, 생성해줘야 한다.
                CorgiCombatLog.Log(CombatLogCategory.User,$"CheckValidJoin OK. but room will be created {userKnew} {serverKnew}", characterId, roomId);
                return true;
            }
            
            if (joinExistRoom != existRoom)
            {    
                CorgiCombatLog.Log(CombatLogCategory.User,$"Error, CheckValidJoin failed. {userKnew} but {serverKnew}", characterId, roomId, true);
                return false;
            }
            
            CorgiCombatLog.Log(CombatLogCategory.User,$"CheckValidJoin OK. {userKnew} {serverKnew}", characterId, roomId);
            return true;
        }
        
        private void JoinRoom(CorgiServerConnection conn, string roomId)
        {
            Room room = GetRoom(roomId);
            bool isExistRoom = (null != room);
            if (null == room)
            {
                room = new Room(roomId);

                if (_roomMap.TryAdd(roomId, room) == false)
                {
                    CorgiCombatLog.Log(CombatLogCategory.User,"can't find room id on map", conn.CharacterId, roomId);
                    throw new CorgiException("failed to create room ({0})", roomId);
                }
                
                StatDataManager.Instance.Increment(StatisticType.RoomCount, 1);
                StatDataManager.Instance.Increment(StatisticType.UserJoin, 1);
            }
                
            
            CorgiCombatLog.Log(CombatLogCategory.User,$"join adeventure to room. room exist[{isExistRoom}]", conn.CharacterId, roomId);
            room.SerializeMethod("JoinAdventure", conn, roomId); 
        }
        
        private ConcurrentDictionary<string, Room> _roomMap;
    }
}