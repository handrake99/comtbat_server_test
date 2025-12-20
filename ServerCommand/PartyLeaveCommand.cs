using IdleCs.ServerContents;
using IdleCs.Utils;
using Newtonsoft.Json.Linq;

namespace IdleCs.CombatServer.ServerCommand
{
    public class PartyLeaveCommand : RedisCommand
    {
        public PartyLeaveCommand()
        {
            CommandType = CommandType.PartyLeave;
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
                CorgiLog.LogError("invalid command parameter for Party Leave\n");
                return;
            }
            
            var roomId = CorgiJson.ParseString(json, "roomId");
            var charId = CorgiJson.ParseString(json, "characterId");
            
            CorgiLog.LogLine("Get Party Leave Request {0}/{1}", roomId, charId);
            RoomManager.Instance.SerializeMethod("OnPartyLeave", roomId, charId);
            
        }
        
    }
}