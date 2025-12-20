using IdleCs.ServerContents;
using IdleCs.Utils;
using Newtonsoft.Json.Linq;

namespace IdleCs.CombatServer.ServerCommand
{
    public class AutoHuntingStartCommand : RedisCommand
    {
        public AutoHuntingStartCommand()
        {
            CommandType = CommandType.AutoHuntingStart;
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
                || CorgiJson.IsValidBool(json, "serialBoss") == false
                || CorgiJson.IsValidLong(json, "buffEndTimestamp") == false)
            {
                CorgiLog.LogError("invalid commnad parameter for AutoHuntingStart\n");
                return;
            }

            var roomId = CorgiJson.ParseString(json, "roomId");
            var characterId = CorgiJson.ParseString(json, "characterId");
            var stageUid = (ulong) CorgiJson.ParseLong(json, "stageUid");
            var serialBoss = CorgiJson.ParseBool(json, "serialBoss");
            var buffEndTimestamp = (ulong)CorgiJson.ParseLong(json, "buffEndTimestamp");
            
            CorgiLog.LogLine("Get AutoHuntingStart Request {0}/{1}/{2}/{3}\n", roomId, stageUid, serialBoss, buffEndTimestamp);
            RoomManager.Instance.SerializeMethod("OnAutoHuntingStart", roomId, characterId, stageUid, serialBoss, buffEndTimestamp);
            
        }
        
    }
}