using IdleCs.Logger;
using IdleCs.ServerContents;
using IdleCs.Utils;

using Newtonsoft.Json.Linq;

namespace IdleCs.CombatServer.ServerCommand
{
    public class StageCompletedCommand : RedisCommand
    {
        public StageCompletedCommand()
        {
            CommandType = CommandType.StageCompleted;
        }
        
        public override void Invoke(JObject json)
        {
            if (CheckCommandJson(json) == false)
            {
                CorgiLog.Log(CorgiLogType.Fatal, "Can't get stage completed command from reids");
                return;
            }

            var roomId = CorgiJson.ParseString(json, "roomId");
            
            if (CorgiJson.IsValidLong(json, "stageUid") == false)
            {
                CorgiLog.Log(CorgiLogType.Fatal, "Can't get stage(uid) completed command from reids. room id[{0}]", roomId);
                return;
            }

            var stageUid = (ulong)CorgiJson.ParseLong(json, "stageUid");
            
            //CorgiLog.Log(CorgiLogType.Info, "room stage was completed. roomid[{0}], stageuid[{1}]", roomId, stageUid);
            
            RoomManager.Instance.SerializeMethod("OnStageCompleted", roomId, stageUid);
            
        }
    }
}
