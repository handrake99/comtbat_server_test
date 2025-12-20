using IdleCs.ServerContents;
using IdleCs.Utils;
using Newtonsoft.Json.Linq;

namespace IdleCs.CombatServer.ServerCommand
{
    public class ChallengeCompletedCommand : RedisCommand
    {
        public ChallengeCompletedCommand()
        {
            CommandType = CommandType.ChallengeStart;
        }

        public override void Invoke(JObject json)
        {
            if (CheckCommandJson(json) == false)
            {
                return;
            }
            
            if (CorgiJson.IsValidString(json, "roomId") == false
                || CorgiJson.IsValidString(json, "characterId") == false
                || CorgiJson.IsValidLong(json, "stageUid") == false
                || CorgiJson.IsValidBool(json, "challengeResult") == false)
            {
                CorgiLog.LogError("invalid command parameter for ChallengeCompleted\n");
                return;
            }

            var roomId = CorgiJson.ParseString(json, "roomId");
            var characterId = CorgiJson.ParseString(json, "characterId");
            var stageUid = (ulong)CorgiJson.ParseLong(json, "stageUid");
            var challengeResult = CorgiJson.ParseBool(json, "challengeResult");
            
            //CorgiLog.LogLine("Get Challenge Start Completed{0}/{1}", roomId, stageUid);
            RoomManager.Instance.SerializeMethod("OnChallengeCompleted", roomId, characterId, stageUid, challengeResult);
        }
        
    }
}