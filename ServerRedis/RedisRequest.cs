using System.Collections.Generic;
using System.Threading.Tasks;
using IdleCs.ServerCore;
using StackExchange.Redis;

namespace IdleCs.CombatServer.ServerRedis
{
    
    public class RedisRequest : CorgiServerObject
    {
        private CorgiServerObject _thisObject;
        private string _characterId;
        private string _callbackName;
        private List<RedisTask> _reqList = new List<RedisTask>();
        
        private int _completedCount = 0 ;
        private bool _isCompleted = false;

        public string CharacterId => _characterId;

        public RedisRequest(CorgiServerObject thisObject, string characterId, string callbackName)
        {
            _thisObject = thisObject;
            _characterId = characterId;
            _callbackName = callbackName;
        }

        public CorgiServerObject ThisObject()//-use for logging only. don't use contents
        {
            return _thisObject;
        }

        public void AddRedisTask(RedisTask redisTask)
        {
            redisTask.Owner = _thisObject;
            redisTask.Parent = this;
            _reqList.Add(redisTask);
        }

        public bool Invoke()
        {
            if (_reqList.Count == 0)
            {
                return false;
            }
            _completedCount = 0;

            foreach (var curTask in _reqList)
            {
                curTask.Invoke();
            }

            return true;
        }

        void OnTaskKeyValueCompleted_Serialized(RedisTask task, string retValue)
        {
            task.OnTaskKeyValueCompleted(retValue);
            _completedCount ++;
            OnCompleted();
        }


        void OnTaskSetCompleted_Serialized(RedisTask task, List<string> retValues)
        {
            task.OnTaskSetCompleted(retValues);
            _completedCount ++;
            OnCompleted();
        }

        void OnCompleted()
        {
            if (_completedCount >= _reqList.Count && _isCompleted == false)
            {
                _thisObject.SerializeMethod(_callbackName, this);
                _isCompleted = true;
            }
        }

        public List<RedisTask> GetRedisTasks()
        {
            return _reqList;
        }
        
    }
}