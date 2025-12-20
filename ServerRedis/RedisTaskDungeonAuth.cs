using IdleCs.GameLogic;
using IdleCs.Logger;
using IdleCs.Managers;
using IdleCs.Utils;

using Newtonsoft.Json.Linq;

namespace IdleCs.CombatServer.ServerRedis
{
    public class RedisTaskDungeonAuth : RedisTask
    {
        public string DungeonKey { get; private set; }
        public bool CanTry { get; private set; }
        

        public RedisTaskDungeonAuth(string dungeonKey, RedisRequestType requestType) : base(requestType)
        {
            DungeonKey = dungeonKey;
        }

        public override void Invoke()
        {
            var task = RedisManager.Instance.RequestDungeonAuth(DungeonKey);
            
            //CorgiLog.LogLine("Load for room Info : {0}", RoomId);
            CorgiCombatLog.Log(CombatLogCategory.System, "Get RedisTaskWorldBossDungeonKey redis DungeonKey[{0}]", DungeonKey);
            
            InvokeInner(task);
        }

        public override void OnTaskKeyValueCompleted(string retValue)
        {
            IsComplete++ ;
            if (string.IsNullOrEmpty(retValue))
            {
                CanTry = false;
                return;
            }

            CanTry = true;
        }
    }
}
