using System;
using IdleCs.GameLogic;
using IdleCs.Managers;
using IdleCs.Utils;

namespace IdleCs.CombatServer.ServerRedis
{
    public class RedisTaskWorldBossDamage : RedisTask
    {
        public string DayNum { get; private set; }
        public bool CanTry { get; private set; }
        public long CurHP { get; private set; }
        public long Damage { get; private set; }
        

        public RedisTaskWorldBossDamage(string dungeonKey, long damage, RedisRequestType requestType) : base(requestType)
        {

            var strArray = dungeonKey.Split('-');
            if (strArray.Length != 4)
            {
                throw new CorgiException("Invalid DungeonKey for BossCurHP");
            }
            DayNum = strArray[2];
            Damage = damage;
        }

        public override void Invoke()
        {
            var task = RedisManager.Instance.RequestWorldBossDamage(DayNum, Damage);
            
            //CorgiLog.LogLine("Load for room Info : {0}", RoomId);
            CorgiCombatLog.Log(CombatLogCategory.System, "Get RedisTaskWorldBossDamage from redis Roomid[{0}]", DayNum);
            
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

            CanTry = true;
            CurHP = curHP;
        }
    }
}
