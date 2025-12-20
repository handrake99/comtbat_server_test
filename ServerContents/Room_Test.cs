using System;
using System.Linq;
using System.Collections.Generic;
using IdleCs.Logger;
using IdleCs.Utils;


namespace IdleCs.ServerContents
{
    public partial class Room
    {
        void Test_Serialized(int index)
        {
            Test_Packet(index, 777, 888, "WTF");
        }

        public void Test_Packet(int index, int protocol, int data1, string data2)
        {
            if ((0 <= index) && (index < _connectionList.Count))
            {
                var conn = _connectionList.ElementAt(index);
                conn.SC_TEST_PACKET(protocol, data1, data2);
            }
        }

        public bool Test_GetKillAllCharacter()
        {
            return _isKillAllCharacter;
        }
        
        public bool Test_GetKillAllMonster()
        {
            return _isKillAllMonster;
        }

        void Test_SetKillAllCharacter_Serialized()
        {
            _isKillAllMonster = true;
            CorgiLog.Log(CorgiLogType.Info, "_isKillAllMonster is true");
        }

        void Test_SetKillAllMonster_Serialized()
        {
            _isKillAllCharacter = true;
            CorgiLog.Log(CorgiLogType.Info, "_isKillAllCharacter is true");
        }
        
        public void Test_ResetKillAllUnit()
        {
            _isKillAllMonster = false;
            _isKillAllCharacter = false;
        }

        public bool Test_BoolValue()
        {
            return _isSomething;
        }

        void Test_BooValue_Serialized(bool isSomething)
        {
            _isSomething = isSomething;
        }
        
        public int Test_IntValue()
        {
            return 0;
        }


        private volatile bool _isSomething = true;       
        private volatile bool _isKillAllMonster = false;
        private volatile bool _isKillAllCharacter = false;
    }
}