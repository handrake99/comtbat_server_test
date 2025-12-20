using IdleCs.ServerContents;
using IdleCs.Utils;
using Newtonsoft.Json.Linq;
using IdleCs.GameLogic;

namespace IdleCs.CombatServer.ServerCommand
{
    public class InstanceDungeonCompletedCommand : RedisCommand
    {
        public InstanceDungeonCompletedCommand()
        {
            CommandType = CommandType.InstanceDungeonCompleted;
        }

        public override void Invoke(JObject json)
        {
            if (CheckCommandJson(json) == false)
            {
                return;
            }
            
            if (CorgiJson.IsValidString(json, "roomId") == false
                || CorgiJson.IsValidString(json, "characterId") == false
                || CorgiJson.IsValidLong(json, "dungeonUid") == false
                || CorgiJson.IsValidLong(json, "stageUid") == false)
            {
                CorgiLog.LogError("invalid commnad parameter for InstanceDungeonCompleted\n");
                return;
            }

            var roomId = CorgiJson.ParseString(json, "roomId");
            var characterId = CorgiJson.ParseString(json, "characterId");
            var dungeonId = CorgiJson.ParseString(json, "dungeonId");
            var dungeonUid = (ulong)CorgiJson.ParseLong(json, "dungeonUid");
            var stageUid = (ulong) CorgiJson.ParseLong(json, "stageUid");
            
            CorgiCombatLog.Log(CombatLogCategory.System, "Get InstanceDungeonCompleted Request {0}/{1}/{2}/{3}\n", roomId, characterId, dungeonUid, stageUid);
            RoomManager.Instance.SerializeMethod("OnInstanceDungeonCompleted", roomId, characterId, dungeonId, dungeonUid, stageUid);
            
        }
        
    }
}