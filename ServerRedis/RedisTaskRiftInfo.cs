using System;
using IdleCs.GameLogic;
using IdleCs.GameLogic.SharedInstance;
using IdleCs.Managers;
using IdleCs.Network.NetLib;
using IdleCs.Utils;

using Newtonsoft.Json;

namespace IdleCs.CombatServer.ServerRedis
{
    public class RedisTaskGetRiftInfo : RedisTask
    {
        public string RoomId { get; protected set; }
        public SharedRift SharedRift { get; protected set; }
        public RedisTaskGetRiftInfo(string roomId, RedisRequestType requestType) : base(requestType)
        {
            RoomId = roomId;
        }
        
        public override void Invoke()
        {
            var task = RedisManager.Instance.RequestRiftInfo(RoomId);
            
            //CorgiLog.LogLine("Load for User Info : {0}", CharId);
            CorgiCombatLog.Log(CombatLogCategory.System, "Get RedisTaskRiftInfo from Redis. RoomId[{0}]", RoomId);
            
            InvokeInner(task);
        }
        
        public override void OnTaskKeyValueCompleted(string retValue)
        {
            IsComplete++ ;
            var info = JsonConvert.DeserializeObject<SharedRift>(retValue);
            SharedRift = info;
        }
    }
}
