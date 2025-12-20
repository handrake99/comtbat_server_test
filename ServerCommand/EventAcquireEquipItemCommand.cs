using IdleCs.Logger;
using IdleCs.ServerContents;
using IdleCs.Utils;

using Newtonsoft.Json.Linq;

namespace IdleCs.CombatServer.ServerCommand
{
    public class EventAcquireEquipItemCommand : RedisCommand
    {
        public EventAcquireEquipItemCommand()
        {
            CommandType = CommandType.EventAcquireEquipItem;
        }
        
        public override void Invoke(JObject json)
        {
            var commandType = CommandType.ToString();
            
            if (CheckCommandJson(json) == false)
            {
                CorgiLog.Log(CorgiLogType.Error, "Invalid command[{0}] parameter #1", commandType);
                return;
            }
            
            if ((false == CorgiJson.IsValidString(json, "roomId"))
            ||  (false == CorgiJson.IsValidString(json, "characterId"))
            ||  (false == CorgiJson.IsValidLong(json, "equipUid")))
            {
                CorgiLog.Log(CorgiLogType.Error, "Invalid command[{0}] parameter #2", commandType);
                return;
            }
            
            var roomId = CorgiJson.ParseString(json, "roomId");
            var characterId = CorgiJson.ParseString(json, "characterId");
            var equipUid = CorgiJson.ParseLong(json, "equipUid");

            //CorgiLog.Log(CorgiLogType.Error, "It[{0}] needs to be implemented.", commandType);
            RoomManager.Instance.SerializeMethod("OnEventAcquireEquipItem", roomId, characterId, equipUid);
        }
    }
}