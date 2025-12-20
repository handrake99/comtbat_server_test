using IdleCs.ServerContents;
using IdleCs.Utils;
using Newtonsoft.Json.Linq;
using IdleCs.GameLogic;

namespace IdleCs.CombatServer.ServerCommand
{
    public class InstanceDungeonStopCommand : RedisCommand
    {
        public InstanceDungeonStopCommand()
        {
            CommandType = CommandType.InstanceDungeonStop;
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
                || CorgiJson.IsValidLong(json, "stageUid") == false)
            {
                CorgiLog.LogError("invalid commnad parameter for InstanceDungeonStart\n");
                return;
            }

            var roomId = CorgiJson.ParseString(json, "roomId");
            var characterId = CorgiJson.ParseString(json, "characterId");
            var dungeonId = CorgiJson.ParseString(json, "dungeonId");
            var dungeonUid = (ulong)CorgiJson.ParseLong(json, "dungeonUid");
            var stageUid = (ulong) CorgiJson.ParseLong(json, "stageUid");
            
            CorgiCombatLog.Log(CombatLogCategory.System, "Get InstanceDungeonStop Request {0}/{1}/{2}/{3}{4}\n", roomId, characterId, dungeonId, dungeonUid, stageUid);
            RoomManager.Instance.SerializeMethod("OnInstanceDungeonStop", roomId, characterId, dungeonId, dungeonUid, stageUid);
        }

    }
}