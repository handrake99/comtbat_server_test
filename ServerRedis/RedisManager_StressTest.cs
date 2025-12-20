using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IdleCs.CombatServer;
using IdleCs.ServerCore;
using IdleCs.Utils;
using StackExchange.Redis;

namespace IdleCs.Managers
{
    public partial class RedisManager 
    {
        private List<string> _roomList;
        public List<string> RoomList 
        {
            get { return _roomList; }
        }
        private List<string> _characterList;
        public List<string> CharacterList 
        {
            get { return _characterList; }
        }

        public async void GetStressTestUserList()
        {
            try
            {
                CorgiLog.Log(CorgiLogType.Info, "Try to Get UserList for StressTest");

                var db = _redis.GetDatabase(CombatServerConfigConst.REDIS_DB_INDEX);
                var roomKey = CorgiString.Format("test-rooms-{0}", CombatServerConfig.Instance.ServerIndex);
                //var roomKey = CorgiString.Format("test-rooms");
                var asyncState = db.SetMembersAsync(roomKey);
                var resultList = await asyncState;

                if (resultList == null)
                {
                    throw new CorgiException("failed to Get UserList for StressTest");
                }

                _roomList = new List<string>();
                _characterList = new List<string>();
                foreach (var result in resultList)
                {
                    var infoStr = (string) result;
                    var infos = infoStr.Split('-');

                    if (infos.Length != 2)
                    {
                        continue;
                    }

                    var roomId = infos[0];
                    var characterId = infos[1];

                    _roomList.Add(roomId);
                    _characterList.Add(characterId);
                }
            }
            catch (Exception e)
            {
                CorgiLog.Log(CorgiLogType.Error, "Occur exception : {0}", e.ToString());
                throw;
            }
        }
    }
}