using IdleCs.ServerContents;
using IdleCs.Utils;
using Newtonsoft.Json.Linq;

namespace IdleCs.CombatServer.ServerCommand
{
    public class ChallengeStartCommand : RedisCommand
    {
        public ChallengeStartCommand()
        {
            CommandType = CommandType.ChallengeStart;
        }

        public override void Invoke(JObject json)
        {
            if (CheckCommandJson(json) == false)
            {
                CorgiLog.LogError("invalid command parameter");
                return;
            }
            
            if (CorgiJson.IsValidString(json, "roomId") == false
                || CorgiJson.IsValidString(json, "characterId") == false
                || CorgiJson.IsValidLong(json, "stageUid") == false)
            {
                CorgiLog.LogError("invalid commnad parameter for ChallengeStart\n");
                return;
            }

            var roomId = CorgiJson.ParseString(json, "roomId");
            var characterId = CorgiJson.ParseString(json, "characterId");
            var stageUid = (ulong) CorgiJson.ParseLong(json, "stageUid");
            
            //CorgiLog.LogLine("Get Challenge Start Request {0}/{1}", roomId, stageUid);
            RoomManager.Instance.SerializeMethod("OnChallengeStart", roomId, characterId, stageUid);
            
        }
    }
}