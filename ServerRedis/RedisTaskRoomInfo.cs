using IdleCs.GameLogic;
using IdleCs.Logger;
using IdleCs.Managers;
using IdleCs.Utils;

using Newtonsoft.Json.Linq;

namespace IdleCs.CombatServer.ServerRedis
{
    public class RedisTaskRoomInfo : RedisTask
    {
        public string RoomId { get; private set; }
        public ulong DungeonUid { get; private set; }
        public ulong StageUid { get; private set; }
        

        public RedisTaskRoomInfo(string roomId, RedisRequestType requestType) : base(requestType)
        {
            RoomId = roomId;
        }

        public override void Invoke()
        {
            var task = RedisManager.Instance.RequestRoomInfo(RoomId);
            
            //CorgiLog.LogLine("Load for room Info : {0}", RoomId);
            CorgiCombatLog.Log(CombatLogCategory.System, "Get RedisTaskRoomInfo from redis Roomid[{0}]", RoomId);
            
            InvokeInner(task);
        }

        public override void OnTaskKeyValueCompleted(string retValue)
        {
            IsComplete++ ;
            var roomInfo = JObject.Parse(retValue);

            if (roomInfo == null)
            {
                throw new CorgiException($"invalid RedisTaskRoomInfo, RoomId[{RoomId}]");
            }
            
            JToken dungeonUidToken;
            if (roomInfo.TryGetValue("dungeonUid", out dungeonUidToken) == false)
            {
                throw new CorgiException($"invalid RedisTaskRoomInfo dungeonUid, RoomId[{RoomId}]");
            }

            JToken stageUidToken;
            if (roomInfo.TryGetValue("stageUid", out stageUidToken) == false)
            {
                throw new CorgiException($"invalid RedisTaskRoomInfo stageUid, RoomId[{RoomId}]");
            }

            DungeonUid = (ulong) dungeonUidToken;
            StageUid = (ulong) stageUidToken;


            //CorgiLog.LogLine("success to load Room Info {0}", retValue);
            //CorgiLog.Log(CorgiLogType.Info, "success to load Room Info");
        }
    }
}