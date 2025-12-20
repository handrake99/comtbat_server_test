using IdleCs.ServerContents;
using IdleCs.Utils;
using Newtonsoft.Json.Linq;

namespace IdleCs.CombatServer.ServerCommand
{
    public class PartyExileCommand : RedisCommand
    {
        public PartyExileCommand()
        {
            CommandType = CommandType.PartyExile;
        }

        public override void Invoke(JObject json)
        {
            if (CheckCommandJson(json) == false)
            {
                CorgiLog.LogError("invalid command parameter");
                return;
            }
            
            if (CorgiJson.IsValidString(json, "roomId") == false
                || CorgiJson.IsValidString(json, "characterId") == false)
            {
                CorgiLog.LogError("invalid command parameter for Party Join\n");
                return;
            }
            
            var roomId = CorgiJson.ParseString(json, "roomId");
            var charId = CorgiJson.ParseString(json, "characterId");
            
            CorgiLog.LogLine("Get Party Join Request {0}/{1}", roomId, charId);
            RoomManager.Instance.SerializeMethod("OnPartyExile", roomId, charId);
        }
    }
}
