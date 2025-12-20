using IdleCs.ServerContents;
using IdleCs.GameLogic;
using IdleCs.Utils;
using Newtonsoft.Json.Linq;

namespace IdleCs.CombatServer.ServerCommand
{
    public class ArenaCompletedCommand : RedisCommand
    {
        public ArenaCompletedCommand()
        {
            CommandType = CommandType.PvpCompleted;
        }
        
        public override void Invoke(JObject json)
        {
            if (CheckCommandJson(json) == false)
            {
                return;
            }
            
            if (CorgiJson.IsValidString(json, "roomId") == false
                || CorgiJson.IsValidString(json, "characterId") == false
                || CorgiJson.IsValidString(json, "dungeonKey") == false)
            {
                CorgiLog.LogError("invalid command parameter for ArenaCompletedCommand\n");
                return;
            }

            var roomId = CorgiJson.ParseString(json, "roomId");
            var characterId = CorgiJson.ParseString(json, "characterId");
            var dungeonKey = CorgiJson.ParseString(json, "dungeonKey");
            
            CorgiCombatLog.Log(CombatLogCategory.System, "Get ArenaCompletedCommand Request {0}/{1}\n", roomId, characterId);
            RoomManager.Instance.SerializeMethod("OnArenaCompleted", roomId, characterId, dungeonKey);
        }
    }
}