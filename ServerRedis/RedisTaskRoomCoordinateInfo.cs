using System.Collections.Generic;
using IdleCs.GameLogic;
using IdleCs.Managers;
using IdleCs.Utils;

using IdleCs.Logger;

namespace IdleCs.CombatServer.ServerRedis
{
    public class RedisTaskRoomCoordinateInfo : RedisTask
    {
        public string RoomId { get; private set; }
        public List<string> CharacterIds { get; private set; }

        public RedisTaskRoomCoordinateInfo(string roomId, RedisRequestType requestType) : base(requestType)
        {
            RoomId = roomId;
        }

        public override void Invoke()
        {
            var task = RedisManager.Instance.RequestRoomCoordinateInfo(RoomId);
            
            //CorgiLog.LogLine("Load for room Coordinate Info : {0}", RoomId);
            CorgiCombatLog.Log(CombatLogCategory.System, "Get RedisTaskRoomCoordinateInfo from Redis. RoomId[{0}]", RoomId);
            InvokeInner(task);
        }
        
        public override void OnTaskSetCompleted(List<string> retValues)
        {
            IsComplete++;
            CharacterIds = new List<string>();
            
            CharacterIds.AddRange(retValues);
            
            
            //CorgiLog.LogLine("success to room Coordinate Info : {0}", retValues);
        }
    }
}
