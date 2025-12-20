using System;
using IdleCs.GameLogic;
using IdleCs.Logger;
using IdleCs.Managers;
using IdleCs.Network.NetLib;
using IdleCs.Utils;

using Newtonsoft.Json.Linq;

namespace IdleCs.CombatServer.ServerRedis
{
    public class RedisTaskPartyLogAll : RedisTask
    {
        public string RoomId { get; private set; }
        public PartyLog PartyLog { get; private set; }
        public ChattingChannel PartyChatting { get; private set; }

        public RedisTaskPartyLogAll(string roomId, RedisRequestType requsetType) : base(requsetType)
        {   
            RoomId = roomId;
        }

        public override void Invoke()
        {
            var task = RedisManager.Instance.RequestPartyLogAll(RoomId);
            
            //CorgiLog.LogLine("Load for PartyLog Info : {0}", RoomId);
            CorgiCombatLog.Log(CombatLogCategory.System, "Get RedisTaskPartyLogAll from redis RoomId[{0}]", RoomId);
            InvokeInner(task);
        }

        public override void OnTaskKeyValueCompleted(string retValue)
        {
            PartyLog = null;
            IsComplete++ ;
            if (string.IsNullOrEmpty(retValue))
            {
                return;
            }

            try
            {
                var recvBuffer = new RecvPacketBuffer(retValue);
                var reader = new StringPacketReader(recvBuffer, 0, retValue.Length);
                PartyLog = new PartyLog();
                PartyLog.DeSerialize(reader);

                PartyChatting = new ChattingChannel();
                PartyChatting.DeSerialize(reader);

                //CorgiLog.LogLine("success to load Party Log Info {0}", retValue);
            }
            catch (Exception e)
            {
                PartyLog = null;
                PartyChatting = null;
                //Console.Write("{0}", e.ToString());
                CorgiLog.Log(CorgiLogType.Fatal, "Occur Exception. can't deserialize party log. RoomId[{0}], Exception[{1}]", RoomId, e.ToString());
            }
        }
    }
}