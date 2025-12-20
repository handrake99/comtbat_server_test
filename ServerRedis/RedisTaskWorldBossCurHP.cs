using System;
using IdleCs.GameLogic;
using IdleCs.Managers;
using IdleCs.Utils;

namespace IdleCs.CombatServer.ServerRedis
{
    public class RedisTaskWorldBossCurHP : RedisTask
    {
        public string DayNum { get; private set; }
        public bool CanTry { get; private set; }
        public long CurHP { get; private set; }
        

        public RedisTaskWorldBossCurHP(string dungeonKey, RedisRequestType requestType) : base(requestType)
        {

            var strArray = dungeonKey.Split('-');
            if (strArray.Length != 4)
            {
                throw new CorgiException("Invalid DungeonKey for BossCurHP");
            }
            DayNum = strArray[2];
        }

        public override void Invoke()
        {
            var task = RedisManager.Instance.RequestWorldBossCurHP(DayNum);
            
            //CorgiLog.LogLine("Load for room Info : {0}", RoomId);
            CorgiCombatLog.Log(CombatLogCategory.System, "Get RedisTaskWorldBossCurHP from redis Roomid[{0}]", DayNum);
            
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

            var curHP = Convert.ToInt64(retValue);

            if (curHP == 0)
            {
                CanTry = false;
                return;
            }

            CanTry = true;
            CurHP = curHP;
        }
    }
}
