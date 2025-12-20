using System;
using System.Collections.Generic;
using IdleCs.GameLog;
using IdleCs.GameLogic;
using IdleCs.GameLogic.SharedInstance;
using IdleCs.Managers;
using IdleCs.Network.NetLib;
using IdleCs.ServerCore;
using IdleCs.ServerUtils;
using IdleCs.Utils;
using Newtonsoft.Json.Linq;

namespace IdleCs.ServerContents
{
    public partial class CorgiServerConnection
    {
        public static bool CS_PING_S(IPacketHandler handler, IPacketReader packetReader)
        {
            int reqId;
            string message;
            
            if (!packetReader.Read(out reqId)) return false;
            if (!packetReader.Read(out message)) return false;


            //SC_PING_BUFFER(reqId, message);
            CorgiServerConnection thisConn = (CorgiServerConnection) (handler);
            if (thisConn == null)
            {
                return false;
            }
            
            RoomManager.Instance.SerializeMethod("Ping", thisConn, reqId, message);
            
            return true;
        }
        
        public static bool CS_CHECK_CONNECTION_S(IPacketHandler handler, IPacketReader packetReader)
        {
            long reqId;
            string token = string.Empty;
            
            if (!packetReader.Read(out reqId)) return false;
            if (!packetReader.Read(out token)) return false;

            //SC_PING_BUFFER(reqId, message);
            CorgiServerConnection thisConn = (CorgiServerConnection) (handler);
            if (thisConn == null)
            {
                return false;
            }
            
            CorgiServerObject.SerializeMethod(thisConn, "CheckConnection", reqId, token);
            
            return true;
        }

        public static bool CS_JOIN_ADVENTURE_S(IPacketHandler handler, IPacketReader packetReader)
        {
            CorgiCombatLog.Log(CombatLogCategory.User,"CS_JOIN_ADVENTURE_S", "", "");
            
            string roomId = string.Empty;
            string characterId = string.Empty;
            int channel = 0;
            bool joinExistRoom = false;

            if (!packetReader.Read(out roomId))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"CS_JOIN_ADVENTURE_S, can't read roomid", "", "", true);
                return false;
            }

            if (!packetReader.Read(out characterId))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"CS_JOIN_ADVENTURE_S, can't read characterid", "", "", true);
                return false;
            }

            if (!packetReader.Read(out channel))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"CS_JOIN_ADVENTURE_S, can't read channel", "", "", true);
                return false;
            }
    
            if (!packetReader.Read(out joinExistRoom))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"CS_JOIN_ADVENTURE_S, can't read joinExistRoom", "", "", true);
                return false;
            }
            
            CorgiCombatLog.Log(CombatLogCategory.User,$"CS_JOIN_ADVENTURE_S, joinExistRoom value is {joinExistRoom}", characterId, roomId);
        
            //SC_PING_BUFFER(reqId, message);
            CorgiServerConnection thisConn = (CorgiServerConnection) (handler);
            if (thisConn == null)
            {
                return false;
            }

            // set connection userid
            thisConn.RoomId = roomId;
            thisConn.CharacterId = characterId;
            
            //RoomManager.Instance.SerializeMethod("JoinAdventure", thisConn, roomId);
            RoomManager.Instance.SerializeMethod("JoinAdventure", thisConn, roomId, joinExistRoom);
            
            LogHelper.LogAPI(LogType.Protocol, roomId, characterId, characterId, "JoinAdventure");
            return true;
        }
        
        public static bool CS_JOIN_INSTANCE_S(IPacketHandler handler, IPacketReader packetReader)
        {
            CorgiCombatLog.Log(CombatLogCategory.User,"CS_JOIN_INSTANCE_S", "", "");
            
            string roomId = string.Empty;
            string characterId = string.Empty;
            string dungeonInfoStr = string.Empty;
            
            if (!packetReader.Read(out roomId))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"CS_JOIN_INSTANCE_S, can't read roomid", "", "", true);
                return false;
            }

            if (!packetReader.Read(out characterId))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"CS_JOIN_INSTANCE_S, can't read characterid", "", "", true);
                return false;
            }

            if (!packetReader.Read(out dungeonInfoStr))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"CS_JOIN_INSTANCE_S, can't read dungeonInfo", "", "", true);
                return false;
            }
    
            CorgiCombatLog.Log(CombatLogCategory.User,$"CS_JOIN_INSTANCE_S", characterId, roomId);
            
            //SC_PING_BUFFER(reqId, message);
            CorgiServerConnection thisConn = (CorgiServerConnection) (handler);
            if (thisConn == null)
            {
                return false;
            }

            if (thisConn.RoomId != roomId || thisConn.CharacterId != characterId)
            {
                thisConn.SC_JOIN_INSTANCE(CorgiErrorCode.DuplicatedConnection, roomId, characterId, null);
                return true;
            }

            try
            {
                var dungeonInfo = JObject.Parse(dungeonInfoStr);
                
                RoomManager.Instance.SerializeMethod("JoinInstance", thisConn, roomId, characterId, dungeonInfo);
            }
            catch
            {
                thisConn.SC_JOIN_INSTANCE(CorgiErrorCode.InstanceInvalidDungeonInfo, roomId, characterId, null);
                
                return false;
            }
        
            
            LogHelper.LogAPI(LogType.Protocol, roomId, characterId, characterId, "JoinInstance");
            
            return true;
        }
        
        public static bool CS_JOIN_INSTANCE2_S(IPacketHandler handler, IPacketReader packetReader)
        {
            CorgiCombatLog.Log(CombatLogCategory.User,"CS_JOIN_INSTANCE_S", "", "");
            
            string roomId = string.Empty;
            string characterId = string.Empty;
            string dungeonId = string.Empty;
            ulong dungeonUid;
            ulong stageUid;
            int dungeonType;
            int grade;
            int level;
            ulong affix1;
            ulong affix2;
            ulong affix3;
            ulong affix4;
            
            if (!packetReader.Read(out roomId))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"CS_JOIN_INSTANCE_S, can't read roomid", "", "", true);
                return false;
            }

            if (!packetReader.Read(out characterId))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"CS_JOIN_INSTANCE_S, can't read characterid", "", "", true);
                return false;
            }
            if (!packetReader.Read(out dungeonId))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"CS_JOIN_INSTANCE_S, can't read dungeonId", "", "", true);
                return false;
            }
            
            if (!packetReader.Read(out dungeonUid))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"CS_JOIN_INSTANCE_S, can't read stageUid", "", "", true);
                return false;
            }
            if (!packetReader.Read(out stageUid))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"CS_JOIN_INSTANCE_S, can't read stageUid", "", "", true);
                return false;
            }
            if (!packetReader.Read(out dungeonType))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"CS_JOIN_INSTANCE_S, can't read dungeonType", "", "", true);
                return false;
            }
            if (!packetReader.Read(out grade))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"CS_JOIN_INSTANCE_S, can't read level", "", "", true);
                return false;
            }
            if (!packetReader.Read(out level))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"CS_JOIN_INSTANCE_S, can't read level", "", "", true);
                return false;
            }
            if (!packetReader.Read(out affix1))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"CS_JOIN_INSTANCE_S, can't read affix1", "", "", true);
                return false;
            }
            if (!packetReader.Read(out affix2))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"CS_JOIN_INSTANCE_S, can't read affix2", "", "", true);
                return false;
            }
            if (!packetReader.Read(out affix3))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"CS_JOIN_INSTANCE_S, can't read affix3", "", "", true);
                return false;
            }
            if (!packetReader.Read(out affix4))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"CS_JOIN_INSTANCE_S, can't read affix4", "", "", true);
                return false;
            }

            CorgiCombatLog.Log(CombatLogCategory.User,$"CS_JOIN_INSTANCE_S", characterId, roomId);
            
            //SC_PING_BUFFER(reqId, message);
            CorgiServerConnection thisConn = (CorgiServerConnection) (handler);
            if (thisConn == null)
            {
                return false;
            }

            if (thisConn.RoomId != roomId || thisConn.CharacterId != characterId)
            {
                thisConn.SC_JOIN_INSTANCE(CorgiErrorCode.DuplicatedConnection, roomId, characterId, null);
                return true;
            }
            
            var jObject = new JObject();
            jObject.Add("characterId", new JValue(characterId));
            jObject.Add("dungeonId", new JValue(dungeonId));
            jObject.Add("dungeonUid", new JValue(dungeonUid));
            jObject.Add("stageUid", new JValue(stageUid));
            jObject.Add("dungeonType", new JValue(dungeonType));
            jObject.Add("grade", new JValue(grade));
            jObject.Add("level", new JValue(level));
            jObject.Add("affix", new JArray(affix1, affix2, affix3, affix4));
            
            RoomManager.Instance.SerializeMethod("JoinInstance", thisConn, roomId, characterId, jObject);
            
            LogHelper.LogAPI(LogType.Protocol, roomId, characterId, characterId, "JoinInstance");
            
            return true;
        }
        
        public static bool CS_JOIN_WORLD_BOSS_S(IPacketHandler handler, IPacketReader packetReader)
        {
            CorgiCombatLog.Log(CombatLogCategory.User, "CS_JOIN_WORLD_BOSS_S");
            
            string roomId = string.Empty;
            string characterId = string.Empty;
            string dungeonKey = string.Empty;

            if (!packetReader.Read(out roomId))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"CS_JOIN_ADVENTURE_S, can't read roomid", "", "", true);
                return false;
            }

            if (!packetReader.Read(out characterId))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"CS_JOIN_ADVENTURE_S, can't read characterid", "", "", true);
                return false;
            }
            
            if (!packetReader.Read(out dungeonKey))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"CS_JOIN_ADVENTURE_S, can't read characterid", "", "", true);
                return false;
            }
        
            CorgiServerConnection thisConn = (CorgiServerConnection) (handler);
            if (thisConn == null)
            {
                return false;
            }

            if (roomId != thisConn.RoomId || characterId != thisConn.CharacterId)
            {
                thisConn.SC_JOIN_WORLD_BOSS(CorgiErrorCode.UnmatchedConnectionInformation, roomId, characterId, dungeonKey, null);
                return false;
            }

            RoomManager.Instance.SerializeMethod("JoinWorldBoss", thisConn, roomId, characterId, dungeonKey);
            
            LogHelper.LogAPI(LogType.Protocol, roomId, characterId, characterId, "JoinWorldBoss");
            return true;
        }
        
        public static bool CS_JOIN_RIFT_S(IPacketHandler handler, IPacketReader packetReader)
        {
            CorgiCombatLog.Log(CombatLogCategory.User, "CS_JOIN_WORLD_BOSS_S");
            
            string roomId = string.Empty;
            string characterId = string.Empty;
            string dungeonKey = string.Empty;

            if (!packetReader.Read(out roomId))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"CS_JOIN_ADVENTURE_S, can't read roomid", "", "", true);
                return false;
            }

            if (!packetReader.Read(out characterId))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"CS_JOIN_ADVENTURE_S, can't read characterid", "", "", true);
                return false;
            }
            
            if (!packetReader.Read(out dungeonKey))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"CS_JOIN_ADVENTURE_S, can't read characterid", "", "", true);
                return false;
            }
        
            CorgiServerConnection thisConn = (CorgiServerConnection) (handler);
            if (thisConn == null)
            {
                return false;
            }

            if (roomId != thisConn.RoomId || characterId != thisConn.CharacterId)
            {
                // process error
            }

            RoomManager.Instance.SerializeMethod("JoinRift", thisConn, roomId, characterId, dungeonKey);
            
            LogHelper.LogAPI(LogType.Protocol, roomId, characterId, characterId, "JoinRift");
            return true;
        }
        
        public static bool CS_JOIN_ARENA_S(IPacketHandler handler, IPacketReader packetReader)
        {
            CorgiCombatLog.Log(CombatLogCategory.User, "CS_JOIN_ARENA_S");
            
            string roomId = string.Empty;
            string characterId = string.Empty;
            string dungeonKey = string.Empty;
            string targetId = string.Empty;

            if (!packetReader.Read(out roomId))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"CS_JOIN_ARENA_S, can't read roomid");
                return false;
            }

            if (!packetReader.Read(out characterId))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"CS_JOIN_ARENA_S, can't read characterid");
                return false;
            }
            
            if (!packetReader.Read(out dungeonKey))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"CS_JOIN_ARENA_S, can't read dungeonKey");
                return false;
            }
            
            if (!packetReader.Read(out targetId))
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"CS_JOIN_ARENA_S, can't read targetId");
                return false;
            }
        
            CorgiServerConnection thisConn = (CorgiServerConnection) (handler);
            if (thisConn == null)
            {
                return false;
            }

            if (roomId != thisConn.RoomId || characterId != thisConn.CharacterId)
            {
                // process error
            }

            RoomManager.Instance.SerializeMethod("JoinArena", thisConn, roomId, characterId, dungeonKey, targetId);
            
            LogHelper.LogAPI(LogType.Protocol, roomId, characterId, characterId, "JoinArena");
            return true;
        }
        
        public static bool CS_SEND_CHATTING_S(IPacketHandler handler, IPacketReader packetReader)
        {
            int chattingTypeInt = 0;
            string data;
            
            if (!packetReader.Read(out chattingTypeInt)) return false;
            if (!packetReader.Read(out data)) return false;

            var chattingType = (ChattingType) chattingTypeInt;

            if (string.IsNullOrEmpty(data))
            {
                return false;
            }

            CorgiServerConnection thisConn = (CorgiServerConnection) (handler);
            if (thisConn == null)
            {
                return false;
            }

            RoomManager.Instance.SerializeMethod("OnSendChatting", thisConn, chattingType, data);
            LogHelper.LogAPI(LogType.Protocol, thisConn.RoomId, thisConn.CharacterId, thisConn.CharacterId,"SendChatting");

            return true;
            
        }
        
        public static bool CS_CHANGE_CHATTING_CHANNEL_S(IPacketHandler handler, IPacketReader packetReader)
        {
            int channel = 0;
            
            if (!packetReader.Read(out channel)) return false;

            var maxChannelCount = GameDataManager.Instance.GetConfig("config.chat.channel.count.max");
            if (null == maxChannelCount)
            {
                return false;
            }
            
            CorgiCombatLog.Log(CombatLogCategory.Chatting, "CS_CHANGE_CHATTING_CHANNEL_S, wanna channel : {0}, max channel count : {1}", 
                channel,
                maxChannelCount.Value.IntValue);
            
            if (channel < 0 || channel >= maxChannelCount.Value.IntValue)
            {
                return false;
            }

            CorgiServerConnection thisConn = (CorgiServerConnection) (handler);
            if (thisConn == null)
            {
                return false;
            }
            
            ChattingManager.Instance.SerializeMethod("OnChangeChattingChannel", thisConn, channel);
            LogHelper.LogAPI(LogType.Protocol, thisConn.RoomId, thisConn.CharacterId, thisConn.CharacterId, "ChangeChatting");
            return true;
        }

        public static bool CS_CHATTING_CHANNEL_LIST_S(IPacketHandler handler, IPacketReader packetReader)
        {
            CorgiServerConnection thisConn = (CorgiServerConnection) (handler);
            if (null == thisConn)
            {
                return false;
            }
        
            ChattingManager.Instance.SerializeMethod("OnChattingChannelList", thisConn);
            LogHelper.LogAPI(LogType.Protocol, thisConn.RoomId, thisConn.CharacterId, thisConn.CharacterId,"ChattingChannelList");
            return true;
        }
        
        public static bool CS_UPDATE_PARTY_MEMBER_STATUS_S(IPacketHandler handler, IPacketReader packetReader)
        {
            CorgiServerConnection thisConn = (CorgiServerConnection) (handler);
            if (null == thisConn)
            {
                return false;
            }

            var memberStatus = new PartyMemberStatus();
            memberStatus.DeSerialize(packetReader);
        
            RoomManager.Instance.SerializeMethod("OnUpdatePartyMemberStatus", thisConn, memberStatus);
            LogHelper.LogAPI(LogType.Protocol, thisConn.RoomId, thisConn.CharacterId, thisConn.CharacterId, "OnUpdatePartyMemberStatus");
            return true;
        }
       
        // public static bool CS_JOIN_INSTANCE_S(IPacketHandler handler, IPacketReader packetReader)
        // {
        //     string userId;
        //     string roomId;
        //     ulong dungeonUid;
        //     ulong stageUid;
        //     
        //     if (!packetReader.Read(out userId)) return false;
        //     if (!packetReader.Read(out roomId)) return false;
        //     if (!packetReader.Read(out dungeonUid)) return false;
        //     if (!packetReader.Read(out stageUid)) return false;
        //
        //
        //     //SC_PING_BUFFER(reqId, message);
        //     CorgiServerConnection thisConn = (CorgiServerConnection) (handler);
        //     if (thisConn == null)
        //     {
        //         return false;
        //     }
        //
        //     // set connection userid
        //     thisConn.UserId = userId;
        //     thisConn.RoomId = roomId;
        //
        //     var room = RoomManager.Instance.GetRoom(roomId);
        //
        //     if (room == null)
        //     {
        //         // send error
        //         CorgiLog.LogError("invalid room for join {0}:{1}", userId, roomId);
        //         
        //         return false;
        //     }
        //     
        //     //CorgiLog.LogError("Receive Join Room Request {0}:{1}", userId, roomId);
        //     
        //     room.SerializeMethod("JoinInstance", thisConn, roomId, dungeonUid, stageUid);
        //
        //     return true;
        //     
        // }
        
        static SendPacketBufferUnit SC_PING_BUFFER(int reqId, string message)
        {
            IPacketWriter PacketWriter = mPacketTranslator.CreatePacketWriter();
            MarkSendPacketID(mPacketTranslator, PacketWriter, PacketIDType.CreatePacketID((int)(CorgiPacketId.Ping)));
            PacketWriter.Write(reqId);
            PacketWriter.Write(message);
            return new SendPacketBufferUnit(PacketWriter.PullOut());
        }

        public void SC_PING(int reqId, string message)
        {
            Send(SC_PING_BUFFER(reqId, message));
        }
        
        static SendPacketBufferUnit SC_CHECK_CONNECTION_BUFFER(long reqId, string token)
        {
            IPacketWriter PacketWriter = mPacketTranslator.CreatePacketWriter();
            MarkSendPacketID(mPacketTranslator, PacketWriter, PacketIDType.CreatePacketID((int)(CorgiPacketId.CheckConnection)));
            PacketWriter.Write(reqId);
            PacketWriter.Write(token);
            return new SendPacketBufferUnit(PacketWriter.PullOut());
        }
        
        public void SC_CHECK_CONNECTION(long reqId, string token)
        {
            Send(SC_CHECK_CONNECTION_BUFFER(reqId, token));
        }
        
        static SendPacketBufferUnit SC_JOIN_ADVENTURE_BUFFER(
            CorgiErrorCode errorCode, 
            string roomId, 
            string characterId, 
            PartyStatus partyStatus,
            PartyLog partyLog,
            ChattingChannel partyChatting,
            SharedDungeon dungeonInfo)
        {
            IPacketWriter PacketWriter = mPacketTranslator.CreatePacketWriter();
            MarkSendPacketID(mPacketTranslator, PacketWriter, PacketIDType.CreatePacketID((int)(CorgiPacketId.JoinAdventure)));
            PacketWriter.Write((int)errorCode);
            PacketWriter.Write(roomId);
            PacketWriter.Write(characterId);
            
            if (partyStatus != null)
            {
                partyStatus.Serialize(PacketWriter);
            }
            else
            {
                PacketWriter.Write(0);
            }

            if (partyLog != null)
            {
                partyLog.Serialize(PacketWriter);
            }
            else
            {
                PacketWriter.Write(0);
            }

            if (partyChatting != null)
            {
                partyChatting.Serialize(PacketWriter);
                
            }
            else
            {
                PacketWriter.Write(0);
            }

            if (dungeonInfo != null)
            {
                dungeonInfo.Serialize(PacketWriter);
            }
            else
            {
                PacketWriter.Write(0);
            }

            var buffer = PacketWriter.PullOut();
            //CorgiLog.LogLine("Network: Send JOIN_ADVENTURE {0}, ErrorCode : {1}", buffer.Length, (int)errorCode);
            return new SendPacketBufferUnit(buffer);
        }
        
        static SendPacketBufferUnit SC_JOIN_INSTANCE_BUFFER(
            CorgiErrorCode errorCode, 
            string roomId, 
            string characterId,
            SharedDungeon dungeonInfo)
        {
            IPacketWriter PacketWriter = mPacketTranslator.CreatePacketWriter();
            MarkSendPacketID(mPacketTranslator, PacketWriter, PacketIDType.CreatePacketID((int)(CorgiPacketId.JoinInstance)));
            PacketWriter.Write((int)errorCode);
            PacketWriter.Write(roomId);
            PacketWriter.Write(characterId);
            
            if (dungeonInfo != null)
            {
                dungeonInfo.Serialize(PacketWriter);
            }
            else
            {
                PacketWriter.Write(0);
            }

            var buffer = PacketWriter.PullOut();
            //CorgiLog.LogLine("Network: Send JOIN_ADVENTURE {0}, ErrorCode : {1}", buffer.Length, (int)errorCode);
            return new SendPacketBufferUnit(buffer);
        }
        
        static SendPacketBufferUnit SC_JOIN_WORLD_BOSS_BUFFER(
            CorgiErrorCode errorCode, 
            string roomId, 
            string characterId,
            string dungeonKey,
            SharedDungeon dungeonInfo)
        {
            IPacketWriter PacketWriter = mPacketTranslator.CreatePacketWriter();
            MarkSendPacketID(mPacketTranslator, PacketWriter, PacketIDType.CreatePacketID((int)(CorgiPacketId.JoinWorldBoss)));
            PacketWriter.Write((int)errorCode);
            PacketWriter.Write(roomId);
            PacketWriter.Write(characterId);
            PacketWriter.Write(dungeonKey);
            
            if (dungeonInfo != null)
            {
                dungeonInfo.Serialize(PacketWriter);
            }
            else
            {
                PacketWriter.Write(0);
            }

            var buffer = PacketWriter.PullOut();
            //CorgiLog.LogLine("Network: Send JOIN_ADVENTURE {0}, ErrorCode : {1}", buffer.Length, (int)errorCode);
            return new SendPacketBufferUnit(buffer);
        }
        
        static SendPacketBufferUnit SC_JOIN_RIFT_BUFFER(
            CorgiErrorCode errorCode, 
            string roomId, 
            string characterId,
            string dungeonKey,
            SharedDungeon dungeonInfo)
        {
            IPacketWriter PacketWriter = mPacketTranslator.CreatePacketWriter();
            MarkSendPacketID(mPacketTranslator, PacketWriter, PacketIDType.CreatePacketID((int)(CorgiPacketId.JoinRift)));
            PacketWriter.Write((int)errorCode);
            PacketWriter.Write(roomId);
            PacketWriter.Write(characterId);
            PacketWriter.Write(dungeonKey);
            
            if (dungeonInfo != null)
            {
                dungeonInfo.Serialize(PacketWriter);
            }
            else
            {
                PacketWriter.Write(0);
            }

            var buffer = PacketWriter.PullOut();
            //CorgiLog.LogLine("Network: Send JOIN_ADVENTURE {0}, ErrorCode : {1}", buffer.Length, (int)errorCode);
            return new SendPacketBufferUnit(buffer);
        }
        static SendPacketBufferUnit SC_JOIN_ARENA_BUFFER(
            CorgiErrorCode errorCode, 
            string roomId, 
            string characterId,
            string dungeonKey,
            SharedArena dungeonInfo)
        {
            IPacketWriter PacketWriter = mPacketTranslator.CreatePacketWriter();
            MarkSendPacketID(mPacketTranslator, PacketWriter, PacketIDType.CreatePacketID((int)(CorgiPacketId.JoinArena)));
            PacketWriter.Write((int)errorCode);
            PacketWriter.Write(roomId);
            PacketWriter.Write(characterId);
            PacketWriter.Write(dungeonKey);
            
            if (dungeonInfo != null)
            {
                dungeonInfo.Serialize(PacketWriter);
            }
            else
            {
                PacketWriter.Write(0);
            }

            var buffer = PacketWriter.PullOut();
            //CorgiLog.LogLine("Network: Send JOIN_ADVENTURE {0}, ErrorCode : {1}", buffer.Length, (int)errorCode);
            return new SendPacketBufferUnit(buffer);
        }
        
        static SendPacketBufferUnit SC_UPDATE_PARTY_MEMBER_BUFFER(string roomId)
        {
            IPacketWriter PacketWriter = mPacketTranslator.CreatePacketWriter();
            MarkSendPacketID(mPacketTranslator, PacketWriter, PacketIDType.CreatePacketID((int)(CorgiPacketId.UpdatePartyMember)));
            PacketWriter.Write(roomId);

            var buffer = PacketWriter.PullOut();
            
            return new SendPacketBufferUnit(buffer);
        }
        
        static SendPacketBufferUnit SC_UPDATE_PARTY_LOG_BUFFER(string roomId, PartyLogMessage partyLogMessage)
        {
            IPacketWriter PacketWriter = mPacketTranslator.CreatePacketWriter();
            MarkSendPacketID(mPacketTranslator, PacketWriter, PacketIDType.CreatePacketID((int)(CorgiPacketId.UpdatePartyLog)));
            PacketWriter.Write(roomId);

            partyLogMessage.Serialize(PacketWriter);
            
            var buffer = PacketWriter.PullOut();
            //CorgiLog.LogLine("Network: Send UpdatePartyLog {0}", buffer.Length);
            
            return new SendPacketBufferUnit(buffer);
        }
        
        static SendPacketBufferUnit SC_UPDATE_CHATTING_BUFFER(ChattingMessage message)
        {
            IPacketWriter PacketWriter = mPacketTranslator.CreatePacketWriter();
            MarkSendPacketID(mPacketTranslator, PacketWriter, PacketIDType.CreatePacketID((int)(CorgiPacketId.RecvChattingMessage)));
            //PacketWriter.Write((int)chattingType);

            message.Serialize(PacketWriter);
            
            var buffer = PacketWriter.PullOut();
            //CorgiLog.LogLine("Network: Send UpdatePartyChatting {0}", buffer.Length);
            
            return new SendPacketBufferUnit(buffer);
        }
        
        static SendPacketBufferUnit SC_CHANGE_CHATTING_CHANNEL_BUFFER(int channel)
        {
            IPacketWriter PacketWriter = mPacketTranslator.CreatePacketWriter();
            MarkSendPacketID(mPacketTranslator, PacketWriter, PacketIDType.CreatePacketID((int)(CorgiPacketId.ChangeChattingChannel)));
            PacketWriter.Write(channel);

            var buffer = PacketWriter.PullOut();
            //CorgiLog.LogLine("Network: Send UpdatePartyChatting {0}", buffer.Length);
            
            return new SendPacketBufferUnit(buffer);
        }
        
        static SendPacketBufferUnit SC_UPDATE_PARTY_MEMBER_STATUS_BUFFER(string roomId, PartyMemberStatus memberStatus)
        {
            IPacketWriter PacketWriter = mPacketTranslator.CreatePacketWriter();
            MarkSendPacketID(mPacketTranslator, PacketWriter, PacketIDType.CreatePacketID((int)(CorgiPacketId.UpdatePartyMemberStatus)));

            PacketWriter.Write(roomId);
            
            memberStatus.Serialize(PacketWriter);

            var buffer = PacketWriter.PullOut();
            CorgiCombatLog.Log(CombatLogCategory.System, "Network: Send UpdatePartyMemberStatus {0}", buffer.Length);
            
            return new SendPacketBufferUnit(buffer);
        }
        
        static SendPacketBufferUnit SC_AUTO_HUNTING_START_BUFFER(string roomId, string characterId, uint challengeCount, ulong buffEndTimestamp)
        {
            IPacketWriter PacketWriter = mPacketTranslator.CreatePacketWriter();
            MarkSendPacketID(mPacketTranslator, PacketWriter, PacketIDType.CreatePacketID((int)(CorgiPacketId.AutoHuntingStart)));
            PacketWriter.Write(roomId);
            PacketWriter.Write(characterId);
            PacketWriter.Write(challengeCount);
            PacketWriter.Write(buffEndTimestamp);

            var buffer = PacketWriter.PullOut();
            //CorgiLog.LogLine("Network: Send AutoHuntingStart {0}", buffer.Length);
            
            return new SendPacketBufferUnit(buffer);
        }
        
        static SendPacketBufferUnit SC_ADVENTURE_COMBATLOG_BUFFER(string roomId, List<DungeonLogNode> logNodes)
        {
            IPacketWriter PacketWriter = mPacketTranslator.CreatePacketWriter();
            MarkSendPacketID(mPacketTranslator, PacketWriter, PacketIDType.CreatePacketID((int)(CorgiPacketId.AdventureCombatLog)));
            PacketWriter.Write(roomId);
            PacketWriter.Write(logNodes.Count);
            foreach (var curLogNode in logNodes)
            {
                curLogNode.Serialize(PacketWriter);
            }
            
            var buffer = PacketWriter.PullOut();
            //CorgiLog.LogLine("Network: Send AdventureCombatLog {0}", buffer.Length);
            
            return new SendPacketBufferUnit(buffer);
        }
        
        static SendPacketBufferUnit SC_INSTANCE_COMBATLOG_BUFFER(string roomId, ulong dungeonUid, ulong stageUid, List<DungeonLogNode> logNodes)
        {
            IPacketWriter PacketWriter = mPacketTranslator.CreatePacketWriter();
            MarkSendPacketID(mPacketTranslator, PacketWriter, PacketIDType.CreatePacketID((int)(CorgiPacketId.InstanceCombatLog)));
            PacketWriter.Write(roomId);
            PacketWriter.Write(dungeonUid);
            PacketWriter.Write(stageUid);
            PacketWriter.Write(logNodes.Count);
            foreach (var curLogNode in logNodes)
            {
                curLogNode.Serialize(PacketWriter);
            }
            
            var buffer = PacketWriter.PullOut();
            //CorgiLog.LogLine("Network: Send InstanceCombatLog {0}", buffer.Length);
            
            return new SendPacketBufferUnit(buffer);
        }
        
        static SendPacketBufferUnit SC_WORLD_BOSS_COMBATLOG_BUFFER(string roomId, string dungeonKey, List<DungeonLogNode> logNodes)
        {
            IPacketWriter PacketWriter = mPacketTranslator.CreatePacketWriter();
            MarkSendPacketID(mPacketTranslator, PacketWriter, PacketIDType.CreatePacketID((int)(CorgiPacketId.WorldBossCombatLog)));
            PacketWriter.Write(roomId);
            PacketWriter.Write(dungeonKey);
            PacketWriter.Write(logNodes.Count);
            foreach (var curLogNode in logNodes)
            {
                curLogNode.Serialize(PacketWriter);
            }
            
            var buffer = PacketWriter.PullOut();
            
            return new SendPacketBufferUnit(buffer);
        }
        
        static SendPacketBufferUnit SC_RIFT_COMBATLOG_BUFFER(string roomId, string dungeonKey, List<DungeonLogNode> logNodes)
        {
            IPacketWriter PacketWriter = mPacketTranslator.CreatePacketWriter();
            MarkSendPacketID(mPacketTranslator, PacketWriter, PacketIDType.CreatePacketID((int)(CorgiPacketId.RiftCombatLog)));
            PacketWriter.Write(roomId);
            PacketWriter.Write(dungeonKey);
            PacketWriter.Write(logNodes.Count);
            foreach (var curLogNode in logNodes)
            {
                curLogNode.Serialize(PacketWriter);
            }
            
            var buffer = PacketWriter.PullOut();
            
            return new SendPacketBufferUnit(buffer);
        }
        
        static SendPacketBufferUnit SC_ARENA_COMBATLOG_BUFFER(string roomId, string dungeonKey, List<DungeonLogNode> logNodes)
        {
            IPacketWriter PacketWriter = mPacketTranslator.CreatePacketWriter();
            MarkSendPacketID(mPacketTranslator, PacketWriter, PacketIDType.CreatePacketID((int)(CorgiPacketId.ArenaCombatLog)));
            PacketWriter.Write(roomId);
            PacketWriter.Write(dungeonKey);
            PacketWriter.Write(logNodes.Count);
            foreach (var curLogNode in logNodes)
            {
                curLogNode.Serialize(PacketWriter);
            }
            
            var buffer = PacketWriter.PullOut();
            
            return new SendPacketBufferUnit(buffer);
        }

        static SendPacketBufferUnit SC_CHATTING_CHANNEL_LIST_BUFFER(ChattingChannelList channelList, int ownChannelID)
        {
            IPacketWriter PacketWriter = mPacketTranslator.CreatePacketWriter();
            MarkSendPacketID(mPacketTranslator, PacketWriter, PacketIDType.CreatePacketID((int)(CorgiPacketId.ChattingChannelList)));
            PacketWriter.Write(ownChannelID);
            channelList.Serialize(PacketWriter);

            var buffer = PacketWriter.PullOut();
            return new SendPacketBufferUnit(buffer);
        }

        static SendPacketBufferUnit SC_UPDATE_PARTY_UNIT_BUFFER(SharedCharacter joinUnit, string leaveUnitObjectId)
        {
            IPacketWriter PacketWriter = mPacketTranslator.CreatePacketWriter();
            MarkSendPacketID(mPacketTranslator, PacketWriter, PacketIDType.CreatePacketID((int)(CorgiPacketId.UpdatePartyUnit)));
            joinUnit.Serialize(PacketWriter);
            PacketWriter.Write(leaveUnitObjectId);

            var buffer = PacketWriter.PullOut();
            return new SendPacketBufferUnit(buffer);
        }
        
        static SendPacketBufferUnit SC_UPDATE_DUNGEON_INFO_BUFFER(SharedDungeon sharedDungeon)
        {
            IPacketWriter PacketWriter = mPacketTranslator.CreatePacketWriter();
            MarkSendPacketID(mPacketTranslator, PacketWriter, PacketIDType.CreatePacketID((int)(CorgiPacketId.UpdateDungeonInfo)));
            sharedDungeon.Serialize(PacketWriter);

            var buffer = PacketWriter.PullOut();
            return new SendPacketBufferUnit(buffer);
        }
        
        static SendPacketBufferUnit SC_UPDATE_RIFT_INFO_BUFFER(SharedRift sharedRift)
        {
            IPacketWriter PacketWriter = mPacketTranslator.CreatePacketWriter();
            MarkSendPacketID(mPacketTranslator, PacketWriter, PacketIDType.CreatePacketID((int)(CorgiPacketId.UpdateRiftInfo)));
            sharedRift.Serialize(PacketWriter);

            var buffer = PacketWriter.PullOut();
            return new SendPacketBufferUnit(buffer);
        }
        
        public void SC_JOIN_ADVENTURE(CorgiErrorCode errorCode
            , string roomId, string characterId, PartyStatus partyStatus, PartyLog partyLog, ChattingChannel partyChatting, SharedDungeon dungeonInfo)
        {
            Send(SC_JOIN_ADVENTURE_BUFFER(errorCode, roomId, characterId, partyStatus, partyLog, partyChatting, dungeonInfo));
        }
        
        public void SC_JOIN_INSTANCE(CorgiErrorCode errorCode, string roomId, string characterId, SharedDungeon dungeonInfo)
        {
            Send(SC_JOIN_INSTANCE_BUFFER(errorCode, roomId, characterId, dungeonInfo));
        }
        
        public void SC_JOIN_WORLD_BOSS(CorgiErrorCode errorCode, string roomId, string characterId, string dungeonKey, SharedDungeon dungeonInfo)
        {
            Send(SC_JOIN_WORLD_BOSS_BUFFER(errorCode, roomId, characterId, dungeonKey, dungeonInfo));
        }
        
        public void SC_JOIN_RIFT(CorgiErrorCode errorCode, string roomId, string characterId, string dungeonKey, SharedDungeon dungeonInfo)
        {
            Send(SC_JOIN_RIFT_BUFFER(errorCode, roomId, characterId, dungeonKey, dungeonInfo));
        }
        
        public void SC_JOIN_ARENA(CorgiErrorCode errorCode, string roomId, string characterId, string dungeonKey, SharedArena dungeonInfo)
        {
            Send(SC_JOIN_ARENA_BUFFER(errorCode, roomId, characterId, dungeonKey, dungeonInfo));
        }
        
        public void SC_UPDATE_PARTY_LOG(string roomId, PartyLogMessage partyLogMessage)
        {
            Send(SC_UPDATE_PARTY_LOG_BUFFER(roomId, partyLogMessage));
        }
        public void SC_UPDATE_CHATTING(ChattingMessage message)
        {
            Send(SC_UPDATE_CHATTING_BUFFER(message));
        }
        
        public void SC_CHANGE_CHATTING_CHANNEL(int channel)
        {
            Send(SC_CHANGE_CHATTING_CHANNEL_BUFFER(channel));
        }
        
        public void SC_UPDATE_PARTY_MEMBER(string roomId)
        {
            Send(SC_UPDATE_PARTY_MEMBER_BUFFER(roomId));
        }
        
        public void SC_AUTO_HUNTING_START(string roomId, string characterId, uint challengeCount, ulong buffEndTimestamp)
        {
            Send(SC_AUTO_HUNTING_START_BUFFER(roomId, characterId, challengeCount, buffEndTimestamp));
        }
        
        // public void SC_JOIN_INSTANCE(CorgiErrorCode errorCode, string userId, string roomId, SharedDungeon dungeonInfo)
        // {
        //     Send(SC_JOIN_INSTANCE_BUFFER(errorCode, userId, roomId, dungeonInfo));
        // }
        
        public void SC_ADVENTURE_COMBATLOG(string roomId, List<DungeonLogNode> logNodes)
        {
            Send(SC_ADVENTURE_COMBATLOG_BUFFER(roomId, logNodes));
        }
        
        public void SC_INSTANCE_COMBATLOG(string roomId, ulong dungeonUid, ulong stageUid, List<DungeonLogNode> logNodes)
        {
            Send(SC_INSTANCE_COMBATLOG_BUFFER(roomId, dungeonUid, stageUid, logNodes));
        }
        
        public void SC_WORLD_BOSS_COMBATLOG(string roomId, string dungeonKey, List<DungeonLogNode> logNodes)
        {
            Send(SC_WORLD_BOSS_COMBATLOG_BUFFER(roomId, dungeonKey, logNodes));
        }
        
        public void SC_RIFT_COMBATLOG(string roomId, string dungeonKey, List<DungeonLogNode> logNodes)
        {
            Send(SC_RIFT_COMBATLOG_BUFFER(roomId, dungeonKey, logNodes));
        }

        public void SC_ARENA_COMBATLOG(string roomId, string dungeonKey, List<DungeonLogNode> logNodes)
        {
            Send(SC_ARENA_COMBATLOG_BUFFER(roomId, dungeonKey, logNodes));
        }
        
        public void SC_CHATTING_CHANNEL_LIST(ChattingChannelList channelList, int ownChannelID)
        {
            Send(SC_CHATTING_CHANNEL_LIST_BUFFER(channelList, ownChannelID));
        }
        
        public void SC_UPDATE_PARTY_MEMBER_STATUS(string roomId, PartyMemberStatus memberStatus)
        {
            Send(SC_UPDATE_PARTY_MEMBER_STATUS_BUFFER(roomId, memberStatus));
        }

        public void SC_UPDATE_PARTY_UNIT(SharedCharacter joinUnit, string leaveUnitObjectid)
        {
            Send(SC_UPDATE_PARTY_UNIT_BUFFER(joinUnit, leaveUnitObjectid));
        }
        
        public void SC_UPDATE_DUNGEON_INFO(SharedDungeon dungeonInfo)
        {
            Send(SC_UPDATE_DUNGEON_INFO_BUFFER(dungeonInfo));
        }
        
        public void SC_UPDATE_RIFT_INFO(SharedRift riftInfo)
        {
            Send(SC_UPDATE_RIFT_INFO_BUFFER(riftInfo));
        }
        
        public void SC_TEST_PACKET(int protocol, int data1, string data2)
        {
            IPacketWriter packetWriter = mPacketTranslator.CreatePacketWriter();
            MarkSendPacketID(mPacketTranslator, packetWriter, PacketIDType.CreatePacketID(protocol));
            packetWriter.Write(data1);
            packetWriter.Write(data2);
            var packetBuffer = new SendPacketBufferUnit(packetWriter.PullOut());
            Send(packetBuffer);
        }
        
        protected override HandlerDeleage GetRecvPacketHandler(PacketIDType PacketID)
        {
            var index = (int) PacketID.Enum;
            if (index < 0 || index >= (int) CorgiPacketId.Max)
            {
                return null;
            }
            
            return mPacketHandlers[index];
        }

        public static void InitProtocol()
        {
            // Init
            mPacketHandlers = new HandlerDeleage[(int)CorgiPacketId.Max];
            
            // Resister for Server
            ResisterProtocol(CorgiPacketId.Ping, CS_PING_S);
            ResisterProtocol(CorgiPacketId.CheckConnection, CS_CHECK_CONNECTION_S);
            ResisterProtocol(CorgiPacketId.JoinAdventure, CS_JOIN_ADVENTURE_S);
            ResisterProtocol(CorgiPacketId.JoinInstance, CS_JOIN_INSTANCE_S);
            ResisterProtocol(CorgiPacketId.JoinInstance2, CS_JOIN_INSTANCE2_S);
            ResisterProtocol(CorgiPacketId.JoinWorldBoss, CS_JOIN_WORLD_BOSS_S);
            ResisterProtocol(CorgiPacketId.JoinRift, CS_JOIN_RIFT_S);
            ResisterProtocol(CorgiPacketId.JoinArena, CS_JOIN_ARENA_S);
            ResisterProtocol(CorgiPacketId.UpdatePartyLog);
            ResisterProtocol(CorgiPacketId.UpdatePartyMember);
            ResisterProtocol(CorgiPacketId.AdventureCombatLog);
            ResisterProtocol(CorgiPacketId.InstanceCombatLog);
            ResisterProtocol(CorgiPacketId.SendChattingMessage, CS_SEND_CHATTING_S);
            ResisterProtocol(CorgiPacketId.ChangeChattingChannel, CS_CHANGE_CHATTING_CHANNEL_S);
            ResisterProtocol(CorgiPacketId.ChattingChannelList, CS_CHATTING_CHANNEL_LIST_S);
            ResisterProtocol(CorgiPacketId.UpdatePartyMemberStatus, CS_UPDATE_PARTY_MEMBER_STATUS_S);
            
            // Resister for Client
        }

        // for recieve
        static void ResisterProtocol(CorgiPacketId packetId, HandlerDeleage handler)
        {
            var packetName = packetId.ToString();
            mRecvPacketNameMap.Register((int) packetId , packetName);
            mSendPacketNameMap.Register((int) packetId , packetName);
            mPacketHandlers[(int)packetId] = handler;
        }

        // for send
        static void ResisterProtocol(CorgiPacketId packetId)
        {
            var packetName = packetId.ToString();
            mRecvPacketNameMap.Register((int) packetId , packetName);
            mSendPacketNameMap.Register((int) packetId , packetName);
        }

        private static HandlerDeleage[] mPacketHandlers;

    }
}