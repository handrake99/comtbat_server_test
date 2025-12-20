using System;
using IdleCs.GameLogic;
using IdleCs.GameLogic.SharedInstance;
using IdleCs.Managers;
using IdleCs.Utils;

using Newtonsoft.Json;

namespace IdleCs.CombatServer.ServerRedis
{
    public class RedisTaskEnemyInfo : RedisTask
    {
        public string CharId { get; private set; }
        public SharedMemberInfo MemberInfo { get; private set; }

        public RedisTaskEnemyInfo(string charId, RedisRequestType requestType) : base(requestType)
        {
            CharId = charId;
        }
        
        public override void Invoke()
        {
            var task = RedisManager.Instance.RequestCharInfo(CharId);
            
            //CorgiLog.LogLine("Load for User Info : {0}", CharId);
            CorgiCombatLog.Log(CombatLogCategory.System, "Get RedisTaskEnemyInfo from Redis. CharacterId[{0}]", CharId);
            
            InvokeInner(task);
        }
        
        public override void OnTaskKeyValueCompleted(string retValue)
        {
            IsComplete++ ;
            if (string.IsNullOrEmpty(retValue))
            {
                MemberInfo = null;
                return;
            }
            
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore, 
                MissingMemberHandling = MissingMemberHandling.Ignore
                    
            };
            var memberInfo = JsonConvert.DeserializeObject<SharedMemberInfo>(retValue, settings );
            if (memberInfo == null || string.IsNullOrEmpty(memberInfo.user.dbId))
            {
                throw new CorgiException($"failed to DeSerialize UserInfo. memberinfo is null or memberInfo.user.dbId is empty. characterId[{CharId}]");
            }

            MemberInfo = memberInfo;
            
        }
    }
}