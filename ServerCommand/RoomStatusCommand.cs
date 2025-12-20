
using Newtonsoft.Json.Linq;

using IdleCs.Managers;
using IdleCs.ServerContents;
using IdleCs.Utils;

namespace IdleCs.CombatServer.ServerCommand
{
    public class RoomStatusCommand : RedisCommand
    {
        public RoomStatusCommand()
        {
            CommandType = CommandType.RoomStatus;
        }
        
        public override void Invoke(JObject json)
        {
            if (CheckCommandJson(json) == false)
            {
                CorgiLog.LogError("invalid command parameter");
                return;
            }
            
            if (CorgiJson.IsValidString(json, "roomId") == false)
            {
                CorgiLog.LogError("invalid command parameter for Party Join\n");
                return;
            }
            
            var roomId = CorgiJson.ParseString(json, "roomId");

            if (string.IsNullOrEmpty(roomId))
            {
                return;
            }
            
            RoomManager.Instance.SerializeMethod("OnRoomStatus", roomId);
        }
    }
}