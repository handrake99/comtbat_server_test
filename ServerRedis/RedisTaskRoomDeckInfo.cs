using IdleCs.GameLogic;
using IdleCs.GameLogic.SharedInstance;
using IdleCs.Logger;
using IdleCs.Managers;
using IdleCs.Utils;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IdleCs.CombatServer.ServerRedis
{
    public class RedisTaskRoomDeckInfo : RedisTask
    {
        public string RoomId { get; private set; }

        public SharedPartyInfo DeckInfo { get; private set; }

        public RedisTaskRoomDeckInfo(string roomId, RedisRequestType requestType) : base(requestType)
        {
            RoomId = roomId;
        }

        public override void Invoke()
        {
            var task = RedisManager.Instance.RequestRoomDeckInfo(RoomId);
            
            CorgiCombatLog.Log(CombatLogCategory.System, "Get RedisTaskRoomDeckInfo from Redis. RoomId[{0}]", RoomId);
            InvokeInner(task);
        }

        public override void OnTaskKeyValueCompleted(string retValue)
        {
            IsComplete++ ;
            var deckInfo = JsonConvert.DeserializeObject<SharedPartyInfo>(retValue);

            if (null == deckInfo)
            {
                throw new CorgiException($"RedisTaskRoomDeckInfo, room deck info is null when invoke redis task. RoomId[{RoomId}]");
            }
            
            if (string.IsNullOrEmpty(deckInfo.characterCoPartySetting.dbId))
            {
                throw new CorgiException($"RedisTaskRoomDeckInfo, deckInfo.characterCoPartySetting.dbId null when invoke redis task. RoomId[{RoomId}]");
            }

            DeckInfo = deckInfo;
            

            //CorgiLog.Log(CorgiLogType.Info, "invoke room deck info completed. roomid[{0}] ret[{1}]", RoomId, retValue);
        }
    }
}