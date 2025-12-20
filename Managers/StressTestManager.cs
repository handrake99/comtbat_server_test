using System.Collections.Generic;
using System.Threading;
using IdleCs.CombatServer;
using IdleCs.ServerContents;
using IdleCs.ServerCore;
using IdleCs.Utils;

namespace IdleCs.Managers
{
    public class StressTestManager: CorgiServerObjectSingleton<StressTestManager>
    {
        private bool _isStressTest = false;
        private int _curIndex = 0;

        private List<string> _roomList;
        private List<string> _characterList;

        private List<CorgiServerTestConnection> _connections = new List<CorgiServerTestConnection>();
        
        public bool IsStressTest
        {
            get { return _isStressTest; }
        }
        public void Initialize()
        {
            
        }


        public void InitializeStressTest()
        {
            // get redis character data
            RedisManager.Instance.GetStressTestUserList();
            
            while (RedisManager.Instance.RoomList == null)
            {
                Thread.Sleep(10);
            }
            
            _roomList = RedisManager.Instance.RoomList;
            _characterList = RedisManager.Instance.CharacterList;
            
            _isStressTest = true;
            CorgiLog.Log(CorgiLogType.Info, "[Stress] Test User Count : {0}", RedisManager.Instance.RoomList.Count);
            
        }

        public void StartStressTest()
        {
            // get stress test mode

            _curIndex = 0;
        }

        void OnConnected_Serialized(CorgiServerTestConnection connection)
        {
            if (_roomList.Count == 0)
            {
                connection.Disconnect();
                return;
            }
            
            var roomId = _roomList[0];
            var characterId= _characterList[0];
            _roomList.RemoveAt(0);
            _characterList.RemoveAt(0);
            
            connection.RoomId = roomId;
            connection.CharacterId = characterId;
            connection.ChallengeTimestamp = CorgiTime.UtcNowULong;
            
            RoomManager.Instance.SerializeMethod("JoinAdventure", connection, roomId, false);

            _connections.Add(connection);
            _curIndex++;
            
            //CorgiLog.Log(CorgiLogType.Info, "[Stress] User Join Room Count {0}", _curIndex);
        }

        void OnClosed_Serialized(CorgiServerTestConnection connection)
        {
            if (_connections.Remove(connection) == false)
            {
                return;
            }

            _roomList.Add(connection.RoomId);
            _characterList.Add(connection.CharacterId);
            
            RoomManager.Instance.SerializeMethod("OnClose", connection);
        }

        // public void Tick()
        // {
        //     if (_curIndex >= _roomList.Count)
        //     {
        //         TickPlayer();
        //         return;
        //     }
        //
        //     for (var i = 0; i < 1; i++)
        //     {
        //         var roomId = _roomList[_curIndex];
        //         var characterId= _characterList[_curIndex];
        //         var newConn = new CorgiServerTestConnection();
        //         
        //         newConn.RoomId = roomId;
        //         newConn.CharacterId = characterId;
        //         newConn.ChallengeTimestamp = CorgiTime.UtcNowULong;
        //         
        //         RoomManager.Instance.SerializeMethod("JoinAdventure", newConn, roomId, false);
        //
        //         _connections.Add(newConn);
        //         _curIndex++;
        //     }
        //     
        //     CorgiLog.Log(CorgiLogType.Info, "[Stress] User Join Room Count {0}", _curIndex);
        // }

        void Tick_Serialized()
        {
            var maxChallengeTime =10000UL;
            var curTimestamp = CorgiTime.UtcNowULong;
            foreach (var conn in _connections)
            {
                if (conn == null)
                {
                    continue;
                }
            
                if (curTimestamp - conn.ChallengeTimestamp > maxChallengeTime)
                {
                    // do this
                    var roomId = conn.RoomId;
                    var characterId = conn.CharacterId;
                    
                    RoomManager.Instance.SerializeMethod("OnChallengeStart", roomId, characterId, 1UL);
                    
                    conn.ChallengeTimestamp = curTimestamp;
                    
                    //CorgiLog.Log(CorgiLogType.Info, "[Stress] User Challenge Start {0}/{1}", roomId, characterId);
                    
                    // 1개만 도전하면 다음 tick으로 넘긴다
                    break;
                }
            }
        }
    }

}