using System;
using System.Linq;

namespace IdleCs.ServerContents
{
    public partial class RoomManager
    {
        void Test_Serialized(int index)
        {
            if (0 < _roomMap.Count)
            {
                var room = _roomMap.ElementAt(0);
                room.Value.SerializeMethod("Test", index);
            }
        }

        void Test_KillMonster_Serialized()
        {
            if (0 < _roomMap.Count)
            {
                var room = _roomMap.ElementAt(0);
                room.Value.SerializeMethod("Test_SetKillAllMonster");
            }
        }

        void Test_KillCharacter_Serialized()
        {
            Console.WriteLine("Test_SetKillAllCharacter");

            if (0 < _roomMap.Count)
            {
                var room = _roomMap.ElementAt(0);
                room.Value.SerializeMethod("Test_SetKillAllCharacter");
            }
        }

        void Test_ResetKillUnit_Serialized()
        {
            Console.WriteLine("Test_ResetKillAllUnit");

            if (0 < _roomMap.Count)
            {
                var room = _roomMap.ElementAt(0);
                room.Value.SerializeMethod("Test_ResetKillAllUnit");
            }
        }

        void Test_BooValue_Serialized(bool isSomething)
        {
            Console.WriteLine("Test_BooValue_Serialized");

            foreach (var keyValuePair in _roomMap)
            {
                var room = keyValuePair.Value;
                room?.SerializeMethod("Test_BooValue", isSomething);
            }
        }
    }
}

