using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using IdleCs.Library;
using IdleCs.ServerCore;
using IdleCs.Utils;
using Newtonsoft.Json.Linq;

namespace IdleCs.Managers
{
    public enum StatisticType
    {
        // SErver Status
        Memory
        
        // core
        , ThreadLoopCount
        
        // room
        , RoomCount
        , ConnectionCount
        
        , UserJoin
        , UserOut
        
        // Contents
        , StageCompleteCount
        , ChallengeCompleteCount
        
    }
    
    public class StatDataManager : CorgiServerObjectSingleton<StatDataManager>
    {
        private Dictionary<StatisticType, ICorgiStat> _statMap;

        //private ConcurrentDictionary<string, long> _statHacks;

        private ulong _lastLogTimestamp = 0 ;
        private ulong _logTime = 5000;

        protected override bool Init()
        {
            _statMap = new Dictionary<StatisticType, ICorgiStat>();
            //_statHacks = new ConcurrentDictionary<string, long>();
            
            _statMap.Add(StatisticType.Memory, new CorgiStatServerStatus(StatisticType.Memory));
            
            //_statMap.Add(StatisticType.ThreadLoopCount, new CorgiStatLocal(StatisticType.ThreadLoopCount));
            
            _statMap.Add(StatisticType.RoomCount, new CorgiStatGlobal(StatisticType.RoomCount));
            _statMap.Add(StatisticType.ConnectionCount, new CorgiStatGlobal(StatisticType.ConnectionCount));
            
            _statMap.Add(StatisticType.UserJoin, new CorgiStatGlobal(StatisticType.UserJoin));
            _statMap.Add(StatisticType.UserOut, new CorgiStatGlobal(StatisticType.UserOut));
            
            _statMap.Add(StatisticType.StageCompleteCount, new CorgiStatGlobal(StatisticType.StageCompleteCount));
            _statMap.Add(StatisticType.ChallengeCompleteCount, new CorgiStatGlobal(StatisticType.ChallengeCompleteCount));

            return true;
        }

        public void Log()
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.Append("\n------------- Statistics -------------\n");
            foreach (var statComp in _statMap.Values)
            {
                if (statComp == null)
                {
                    continue;
                }

                statComp.Log(stringBuilder);
            }

            stringBuilder.Append("-------------     End    -------------");
            
            CorgiLog.Log(CorgiLogType.Info, stringBuilder.ToString());
            
            // redis log
            JObject statJson = new JObject();
            
            foreach (var statComp in _statMap.Values)
            {
                if (statComp == null)
                {
                    continue;
                }

                var statType = statComp.GetStatType();
                var statValue= statComp.GetStatValue();

                statJson.Add(statType.ToString(), new JValue(statValue));
            }
            
            RedisManager.Instance.SendStatistic(statJson);
        }

        void Tick_Serialized()
        {
            var curTimestamp = CorgiTime.UtcNowULong;

            var timeDiff = curTimestamp - _lastLogTimestamp ;
            if (timeDiff > _logTime)
            {
                // log
                Log();
                _lastLogTimestamp = curTimestamp;
            }
        }

        public void Increment(StatisticType statType, uint incValue)
        {
            if (_statMap.ContainsKey(statType) == false)
            {
                return;
            }

            var statInst = _statMap[statType];
            statInst.Increment(incValue);
        }
        public void Decrement(StatisticType statType, uint decValue)
        {
            if (_statMap.ContainsKey(statType) == false)
            {
                return;
            }

            var statInst = _statMap[statType];
            statInst.Decrement(decValue);
        }

        public void ReportHack(string characterId)
        {
            //_statHacks.AddOrUpdate(characterId, 1, (key, oldValue) => oldValue+1);
        }

    }
}