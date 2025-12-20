using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;

using IdleCs.GameLogic;
using IdleCs.Library;
using IdleCs.CombatServer;
using IdleCs.Logger;
using IdleCs.ServerContents;
using IdleCs.ServerCore;
using IdleCs.ServerUtils;
using IdleCs.Utils;


//using UnityEngine;

namespace IdleCs.Managers
{
    public class ChattingManager : CorgiServerObjectSingleton<ChattingManager>
    {
        private int _maxChannelUserCount = 0;
        private int _maxChattingMessageCount = 1000;
        private ChattingChannelList _generalChannelList = null;
        private Dictionary<int, ChattingChannelList> _leagueChannelMap = null;
        private List<CorgiServerConnection> _connectionList = null; 
        
        public int ConnectionCount
        {
            get {
                if (_connectionList == null)
                {
                    return 0;
                } 
                return _connectionList.Count;
            }
        }
        
        protected override bool Init()
        {
            base.Init();
            
            var maxChannelCount = GameDataManager.Instance.GetConfig("config.chat.channel.count.max");
            if (null == maxChannelCount)
            {
                return false;
            }
            
            var maxChannelUserCount = GameDataManager.Instance.GetConfig("config.chat.channel.red");
            if (null == maxChannelUserCount)
            {
                return false;
            }

            var maxMessageCount = CombatServerConfigConst.CHATTING_MESSAGE_MAX_COUNT;
            
            
            CorgiLog.Log(CorgiLogType.Info, "* Max channel count : {0}", maxChannelCount.Value.IntValue);
            CorgiLog.Log(CorgiLogType.Info, "* Max user count per channel : {0}", maxChannelUserCount.Value.IntValue);
            
            _connectionList = new List<CorgiServerConnection>();
            _generalChannelList = new ChattingChannelList();
            _leagueChannelMap = new Dictionary<int, ChattingChannelList>();
            
            _maxChannelUserCount = maxChannelUserCount.Value.IntValue;
            _maxChattingMessageCount = maxMessageCount;
            
            // 일반 채널만 초기화 & 리그 채널은 비워둔다
            var initResult = _generalChannelList.Initialize(ChattingType.General, maxChannelCount.Value.IntValue, _maxChannelUserCount, _maxChattingMessageCount);
            if (initResult == false)
            {
                CorgiLog.LogLine("[error] chatting manager initialize failed. channel count : {0}", maxChannelCount.Value.IntValue);
                return false;
            }

            return true;
        }
        
        void OnChangeChattingChannel_Serialized(CorgiServerConnection conn, int channel)
        {
            ChangeChannel(conn, channel);
        }
        
        void OnChannelChatting_Serialized(CorgiServerConnection conn, ChattingMessage message)
        {
            RedisManager.Instance.SendChattingMessage(conn.ChannelKey, message);
            
            //NtfChannelChatting(conn.ChannelKey, message);
        }
        
        void OnChattingChannelList_Serialized(CorgiServerConnection conn)
        {
            NtfCChannelList(conn);
        }

        void NtfChannelChatting_Serialized(string channelKey, ChattingMessage message)
        {
            NtfChannelChatting(channelKey, message);
        }
        
        // void Reset()
        // {
        //     _ = _generalChannelList.GeneralChannels.All(channel =>
        //     {
        //         channel.Initialize(channel.Index(), _maxChattingMessageCount);
        //         return true;
        //     });
        //
        //     _connectionList.Clear();
        // }

        void AddConnection_Serialized(CorgiServerConnection conn, int leagueSerial)
        {
            ChattingChannel chattingChannel = null;
            if (leagueSerial == 0)
            {
                chattingChannel = _generalChannelList.FindInitialChannel();
            }
            else
            {
                if (_leagueChannelMap.ContainsKey(leagueSerial))
                {
                    chattingChannel = _leagueChannelMap[leagueSerial].FindInitialChannel();
                }
                else
                {
                    var channelList = new ChattingChannelList();
                    channelList.Initialize(ChattingType.League, leagueSerial, -1, _maxChattingMessageCount);
                    _leagueChannelMap[leagueSerial] = channelList;

                    chattingChannel = channelList.FindInitialChannel();
                    
                    // regist channel
                    RedisManager.Instance.AddChattingSubscriber(chattingChannel.ChannelKey);
                    
                }
            }
            if (chattingChannel != null)
            {
                conn.ChattingType = chattingChannel.ChattingType;
                conn.ChannelKey = chattingChannel.ChannelKey;
                conn.ChannelIndex = chattingChannel.Index;
                
                _connectionList.Add(conn);
                
                chattingChannel.AddUser();
                
            }
            
            NtfCChannelList(conn);
        }

        void RemoveConnection_Serialized(CorgiServerConnection conn)
        {
            if (conn.ChattingType == ChattingType.General)
            {
                var chattingChannel = _generalChannelList.GetChannel(conn.ChannelIndex);
                if (chattingChannel != null)
                {
                    chattingChannel.RemoveUser();
                }
            }
            else if(conn.ChattingType == ChattingType.League)
            {
                if (_leagueChannelMap.ContainsKey(conn.ChannelIndex))
                {
                    var channelList = _leagueChannelMap[conn.ChannelIndex];
                    var chattingChannel = channelList.GetChannel(conn.ChannelIndex);
                    if (chattingChannel != null)
                    {
                        chattingChannel.RemoveUser();
                    }
                }
            }

            _connectionList.Remove(conn);
            
            var remainCount = GetConnectionCount();
            CorgiCombatLog.Log(CombatLogCategory.User, $"OnClose at RoomManager, remove chatting connection. remain count[{remainCount}]", conn.CharacterId, conn.RoomId);
        }
        
        void NtfCChannelList(CorgiServerConnection conn)
        {
            if (conn.ChattingType == ChattingType.General)
            {
                conn.SC_CHATTING_CHANNEL_LIST(_generalChannelList, conn.ChannelIndex);
            }
            else
            {
                var channelList = _leagueChannelMap[conn.ChannelIndex];
                if (channelList != null)
                {
                    conn.SC_CHATTING_CHANNEL_LIST(channelList, conn.ChannelIndex);
                    
                }
            }
        }

        void NtfChannelChatting(string channelKey, ChattingMessage message)
        {
            foreach (var curConn in _connectionList)
            {
                if (curConn != null && message.ChattingType == curConn.ChattingType && curConn.ChannelKey == channelKey)
                {
                    curConn.SC_UPDATE_CHATTING(message);
                }
            }
        }
        
        // for general channel
        ChattingChannel GetChannel(int channel)
        {
            var chattingChannel =_generalChannelList.GetChannel(channel);
            return chattingChannel;
        }

        // change general channel
        void ChangeChannel(CorgiServerConnection conn, int newChannel)
        {
            int oldChannel = conn.ChannelIndex;
            if (oldChannel == newChannel)
            {
                return;
            }

            var oldChattingChannel = GetChannel(oldChannel);
            var newChattingChannel = GetChannel(newChannel);

            if ((null == oldChattingChannel) || (null == newChattingChannel))
            {
                return;
            }
            
            CorgiLog.Log(CorgiLogType.Info, "Change channel, old[{0}] user count[{1}], new[{2}] user count[{3}] / max user count per channel [{4}]",
                oldChattingChannel.Index, oldChattingChannel.CurrentUser(), newChattingChannel.Index, newChattingChannel.CurrentUser(),
                _maxChannelUserCount);

            // 정원 초과도 넣어줌
            // if (_maxChannelUserCount <= newChattingChannel.CurrentUser())
            // {
            //     return;
            // }
            
            oldChattingChannel.RemoveUser();
            newChattingChannel.AddUser();

            CorgiLog.Log(CorgiLogType.Info, "last, old channel user count[{0}], new channel user count[{1}]", oldChattingChannel.CurrentUser(), newChattingChannel.CurrentUser());
            
            conn.ChannelKey = newChattingChannel.ChannelKey;
            conn.ChannelIndex = newChattingChannel.Index;
            
            CorgiLog.Log(CorgiLogType.Info, "send changed chatting channel [{0}]", newChannel);
            
            conn.SC_CHANGE_CHATTING_CHANNEL(conn.ChannelIndex);
        }

        int GetConnectionCount()
        {
            return _connectionList.Count;
        }

    }
}