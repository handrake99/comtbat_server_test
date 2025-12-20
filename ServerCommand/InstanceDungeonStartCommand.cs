using IdleCs.GameLogic;
using IdleCs.ServerContents;
using IdleCs.Utils;
using Newtonsoft.Json.Linq;

namespace IdleCs.CombatServer.ServerCommand
{
    public class InstanceDungeonStartCommand : RedisCommand
    {
        public InstanceDungeonStartCommand()
        {
            CommandType = CommandType.InstanceDungeonStart;
        }

        public override void Invoke(JObject json)
        {
            if (CheckCommandJson(json) == false)
            {
                return;
            }

            if (CorgiJson.IsValidString(json, "roomId") == false
                || CorgiJson.IsValidString(json, "characterId") == false
                || CorgiJson.IsValidString(json, "dungeonId") == false
                || CorgiJson.IsValidLong(json, "dungeonUid") == false
                || CorgiJson.IsValidLong(json, "stageUid") == false
                || CorgiJson.IsValidInt(json, "grade") == false
                || CorgiJson.IsValidInt(json, "level") == false
                || CorgiJson.IsValid(json, "affix") == false)
            {
                CorgiLog.LogError("invalid commnad parameter for InstanceDungeonStart\n");
                return;
            }

            var roomId = CorgiJson.ParseString(json, "roomId");
            
            CorgiCombatLog.Log(CombatLogCategory.System,"Get InstanceDungeonStart Request {0}/{1}\n", roomId, json);
            //remove instance dungeon start command
            // run by client
            //RoomManager.Instance.SerializeMethod("OnInstanceDungeonStart", roomId, json);
        }
        
    }
}