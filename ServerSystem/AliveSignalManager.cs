using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Data;
using System.Threading.Tasks;

using IdleCs.Network;
using IdleCs.ServerCore;
using IdleCs.ServerContents;
using IdleCs.CombatServer;
using IdleCs.Logger;
using IdleCs.Managers;
using IdleCs.Utils;


namespace IdleCs.ServerSystem
{
    public class AliveSignalManager : CorgiServerObjectSingleton<AliveSignalManager>
    {
        protected override bool Init()
        {
            base.Init();

            _lastestTick = CorgiTime.UtcNowULong;
            _signaledList = new ConcurrentQueue<AliveSignal>();
            return true;
        }

        public void Start()
        {
            _signaledList.Enqueue(new AliveSignal());    
        }
        
        public void Process()
        {
            var currentTick = CorgiTime.UtcNowULong;
            var intervalTick = CombatServerConfig.Instance.Server.AliveSignalTimeIntervalMS;
            if ((currentTick - _lastestTick) > intervalTick)
            {
                PingToRedis();   
                _lastestTick = currentTick;
            }
        }

        private void PingToRedis()
        {
            //CorgiLog.Log(CorgiLogType.Info, "request ping");
            
            RedisManager.Instance.SerializeMethod("Ping");
        }

        public void PongFromRedis(ConcurrentDictionary<string, Room> roomMap)
        {
            //CorgiLog.Log(CorgiLogType.Info, "response pong");
         
            var roomIdList = new List<string>();
            foreach (var keyValuePair in roomMap)
            {
                roomIdList.Add(keyValuePair.Key);
            }

            RedisManager.Instance.SerializeMethod("RecordServerStatus", roomIdList);            
        }
        
        public void PongFromRedis()
        {
            RedisManager.Instance.SerializeMethod("KeepAlive");
        }

        public bool DelayCheck()
        {
            AliveSignal signal = null;
            if (true == _signaledList.TryDequeue(out signal))
            {
                TimeSpan elapsedSpan = new TimeSpan(DateTime.Now.Ticks - signal.Past.Ticks);
                var validWaitTick = CombatServerConfig.Instance.Server.AliveSignalValidWaitTimeMS;

                if ((validWaitTick) < (elapsedSpan.TotalMilliseconds))
                {
                    CorgiLog.Log(CorgiLogType.Fatal, "Last request time is [{0}], elapsed time ms[{1}]. Will be cleared this server.", signal.Past.ToString("yyyy/MM/dd hh:mm:ss.fff"), elapsedSpan.TotalMilliseconds);
                    return false;
                }
                
                //CorgiLog.Log(CorgiLogType.Fatal, "Last request time is [{0}], elapsed time ms[{1}]. Will be cleared this server. signal que count[{2}]", 
                  //  signal.Past.ToString("yyyy/MM/dd hh:mm:ss.fff"), elapsedSpan.TotalMilliseconds, _signaledList.Count);

            }
            
            _signaledList.Enqueue(new AliveSignal());
            return true;
        }
        
        private ulong _lastestTick = 0;
        private ConcurrentQueue<AliveSignal> _signaledList = null;
    }
}