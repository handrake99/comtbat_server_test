using IdleCs.GameLogic;
using IdleCs.Logger;
using IdleCs.Managers;
using IdleCs.Utils;

using Newtonsoft.Json.Linq;

namespace IdleCs.CombatServer.ServerRedis
{
    public class RedisTaskRoomStatus: RedisTask
    {
        public string RoomId { get; private set; }
        public ulong BuffEndTimestamp { get; private set; }
        

        public RedisTaskRoomStatus(string roomId, RedisRequestType requestType) : base(requestType)
        {
            RoomId = roomId;
        }

        public override void Invoke()
        {
            var task = RedisManager.Instance.RequestRoomStatus(RoomId);
            
            //CorgiLog.LogLine("Load for room Info : {0}", RoomId);
            CorgiCombatLog.Log(CombatLogCategory.System, "Get RedisTaskRoomStatusfrom redis Roomid[{0}]", RoomId);
            
            InvokeInner(task);
        }

        public override void OnTaskKeyValueCompleted(string retValue)
        {
            IsComplete++ ;
            if (string.IsNullOrEmpty(retValue))
            {
                return;
            }
            
            var roomInfo = JObject.Parse(retValue);

            if (roomInfo == null)
            {
                throw new CorgiException($"invalid RedisTaskRoomStatusfrom, RoomId[{RoomId}]");
            }
            
            JToken buffEndTimestampToken;
            if (roomInfo.TryGetValue("buffEndTimestamp", out buffEndTimestampToken) )
            {
                BuffEndTimestamp = (ulong)buffEndTimestampToken;
            }
            
        }
    }
}
