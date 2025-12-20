using IdleCs.GameLogic;
using IdleCs.GameLogic.SharedInstance;
using IdleCs.ServerContents;
using IdleCs.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IdleCs.CombatServer.ServerCommand
{
    public class RiftOpenCommand : RedisCommand
    {
        public RiftOpenCommand()
        {
            CommandType = CommandType.RiftOpen;
        }

        public override void Invoke(JObject json)
        {
            if (CheckCommandJson(json) == false)
            {
                return;
            }
            
            if (CorgiJson.IsValidString(json, "roomId") == false)
            {
                CorgiLog.LogError("invalid command parameter for RedisOpenCommand\n");
                return;
            }

            var roomId = CorgiJson.ParseString(json, "roomId");
            var riftStr = CorgiJson.ParseString(json, "riftInfo");
            var sharedRift = JsonConvert.DeserializeObject<SharedRift>(riftStr);
            var characterId = CorgiJson.ParseString(json, "characterId");

            CorgiCombatLog.Log(CombatLogCategory.System, "Get RiftOpen Request {0}\n", roomId);
            RoomManager.Instance.SerializeMethod("OnRiftOpen", roomId, sharedRift, characterId);
        }
    }
}
