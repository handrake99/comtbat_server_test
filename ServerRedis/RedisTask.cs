using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IdleCs.GameLogic;
using IdleCs.ServerCore;
using IdleCs.Utils;
using StackExchange.Redis;

namespace IdleCs.CombatServer.ServerRedis
{
    public class RedisTask
    {
        public CorgiServerObject Owner { get; set; }
        public RedisRequest Parent { get; set; }
        
        public RedisRequestType RequestType { get; protected set; }
        
        public int IsComplete { get; protected set; }

        public RedisTask(RedisRequestType requsetType)
        {
            RequestType = requsetType;
            IsComplete = 0;
        }

        protected void InvokeInner(Task<RedisValue> task)
        {
            var thisTask = new Task(async () =>
            {
                var result = await task;
                
                if (string.IsNullOrEmpty((string)result))
                {
                    Parent.SerializeMethod("OnTaskKeyValueCompleted", this, string.Empty);
                }
                else
                {
                    Parent.SerializeMethod("OnTaskKeyValueCompleted", this, (string) result);
                }

            });
            
            thisTask.Start();
        }
        
        protected void InvokeInner<T>(Task<T> task) where T : IComparable, IComparable<T>
        {
            var thisTask = new Task(async () =>
            {
                var result = await task;
                var resultStr = result.ToString();
                
                Parent.SerializeMethod("OnTaskKeyValueCompleted", this, resultStr);
            });
            
            thisTask.Start();
        }
        
        protected void InvokeInner(Task<RedisValue[]> task)
        {
            var thisTask = new Task(async () =>
            {
                var result = await task;
                
                if(result.Length <= 0)
                {
                    Parent.SerializeMethod("OnTaskSetCompleted", this, new List<string>());
                    return;
                }
                
                List<string> retStrs = new List<string>();

                foreach (var curJson in result)
                {
                    string curStr = (string)curJson;
                    retStrs.Add(curStr);
                }
                
                Parent.SerializeMethod("OnTaskSetCompleted", this, retStrs);
            });
            
            thisTask.Start();
        }

        public virtual void Invoke()
        {
            throw new NotImplementedException();
        }
        
        public virtual void OnTaskKeyValueCompleted(string retValue)
        {
        }
        
        public virtual void OnTaskSetCompleted(List<string> retValues)
        {
            
        }
    }

    public static class RedisTaskFactory
    {
        public static RedisTask Create(RedisRequestType requestType, string requestKey)
        {
            switch (requestType)
            {
                case RedisRequestType.RoomCoordinateInfo:
                    return new RedisTaskRoomCoordinateInfo(requestKey, requestType);
                case RedisRequestType.CharaterInfo:
                    return new RedisTaskCharacterInfo(requestKey, requestType);
                case RedisRequestType.RoomInfo:
                    return new RedisTaskRoomInfo(requestKey, requestType);
                case RedisRequestType.RoomStatus:
                    return new RedisTaskRoomStatus(requestKey, requestType);
                case RedisRequestType.RoomDeckInfo:
                    return new RedisTaskRoomDeckInfo(requestKey, requestType);
                case RedisRequestType.PartyLogAll:
                    return new RedisTaskPartyLogAll(requestKey, requestType);
                case RedisRequestType.DungeonAuth:
                    return new RedisTaskDungeonAuth(requestKey, requestType);
                case RedisRequestType.WorldBossCurHP:
                    return new RedisTaskWorldBossCurHP(requestKey, requestType);
                case RedisRequestType.WorldBossMaxHP:
                    return new RedisTaskWorldBossMaxHP(requestKey, requestType);
                case RedisRequestType.GetRiftInfo:
                    return new RedisTaskGetRiftInfo(requestKey, requestType);
                case RedisRequestType.EnemyInfo:
                    return new RedisTaskEnemyInfo(requestKey, requestType);
                default:
                    CorgiLog.LogError("invalid redis task type {0}", requestType.ToString());
                    return null;
            }
        }
    }
}