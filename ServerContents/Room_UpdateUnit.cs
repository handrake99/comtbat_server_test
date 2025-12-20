using System;
using System.Collections.Generic;
using System.Linq;
using Corgi.Protocol;
using IdleCs.GameLogic;
using IdleCs.GameLogic.SharedInstance;
using IdleCs.Logger;
using IdleCs.ServerUtils;
using IdleCs.Utils;


namespace IdleCs.ServerContents
{
    public partial class Room
    {
        // public void AddJoinUnit(string charDbId)
        // {   
        //     var unit = CreateCharacter(_adventureDungeon, charDbId);
        //     if (null == unit)
        //     {
        //         CorgiCombatLog.LogError(CombatLogCategory.Party, "[party] AddJoinUnit, someone[{0}] join party at room[{1}] failed. can't create character", charDbId, _roomId);
        //         return;
        //     }
        //
        //     _willJoinUnit = unit;
        //     CorgiCombatLog.Log(CombatLogCategory.Party, "[party] AddJoinUnit, someone[{0}] join party at room[{1}]", charDbId, _roomId);
        // }

//        public Unit GetJoinUnit()
//        {
//            return _joinUnit;
//        }

//        public void RemoveJoinUnits()
//        {
//            _joinUnit = null;
//        }

        // public void AddLeaveUnit(string dbId)
        // {
        //     _willLeaveUnitDbId = dbId;   
        //     CorgiCombatLog.Log(CombatLogCategory.Party, "[party] AddLeaveUnit, someone[{0}] leave party at room[{1}]", dbId, _roomId);
        // }

//        public string GetLeaveUnitDbId()
//        {
//            return _leaveUnitDBId;
//        }
//        
//        public void RemoveLeaveUnits()
//        {
//            _leaveUnitDBId = string.Empty;
//        }

        public bool UpdateUnit()
        {
            if (_party.IsPartyUpdated == false)
            {
                return false;
            }
            _adventureDungeon.UpdateMemberInfos(_party.MemberInfos);

            _party.OnPartyUpdated();
            
            return true;
        }
        
//        public void NtfUpdateUnit(Unit joinUnit, string leaveUnitObjectId)
//        {
//            int sendCount = 0;
//            foreach (var conn in _connectionList)
//            {
//                var unit = new SharedCharacter();
//                unit.Init(joinUnit);
//                conn.SC_UPDATE_PARTY_UNIT(unit, leaveUnitObjectId);
//                
//                CorgiLog.Log(CorgiLogType.Info, "ntf to user[{0}] add unit[{1}], leave unit[{2}]", ++sendCount, unit.objectId, leaveUnitObjectId);
//            }
            
            //-클라이언트 요청사항, 
            //    서버에서 SharedDungeon의 CharList가 세팅이 되고 나서 SC_ADVENTURE_COMBATLOG 를 보내고 
            //    그 다음에 SC_UPDATE_PARTY_UNIT 를 보내야한다.
            //-결론
            //    Dungeon.JoinDungeon 이 호출되면 SharedDungeon에 CharList 가 세팅됨, 그 틱에서 SC_UPDATE_PARTY_UNIT 가 호출되어 클라이언트에게 해당 정보를 보내고
            //    SerializeMethod 를 통해서 그 다음 틱에 동기화시킴    
//            SerializeMethod("NtfUpdateUnit", joinUnit, leaveUnitObjectId);   
//            CorgiLog.Log(CorgiLogType.Info, "roomid : {0}, join[{1}][{2}], leave[{3}]", _roomId, joinUnit.ObjectId, joinUnit.ClassType.ToString(), leaveUnitObjectId);
//        }

//        void NtfUpdateUnit_Serialized(Unit joinUnit, string leaveUnitObjectId)
//        {
//            int sendCount = 0;
//            foreach (var conn in _connectionList)
//            {
//                var unit = new SharedCharacter();
//                unit.Init(joinUnit);
//                conn.SC_UPDATE_PARTY_UNIT(unit, leaveUnitObjectId);
//                
//                CorgiLog.Log(CorgiLogType.Info, "ntf to user[{0}] add unit[{1}], leave unit[{2}]", ++sendCount, unit.objectId, leaveUnitObjectId);
//            }
//        }

        public void CachedUpdateUnit(Unit joinUnit, string leaveUnitObjectId)
        {
            _joinedUnit = joinUnit;
            _leftUnitObjetId = leaveUnitObjectId;
        }

        public void NtfUpdateUnit()
        {               
            if ((null == _joinedUnit) && (string.Empty == _leftUnitObjetId))
            {
                return;
            }
            
            int sendCount = 0;
            foreach (var conn in _connectionList)
            {
                var unit = new SharedCharacter();
                unit.Init(_joinedUnit);
                conn.SC_UPDATE_PARTY_UNIT(unit, _leftUnitObjetId);

                CorgiCombatLog.Log(CombatLogCategory.Party, "ntf to user[{0}] add unit[{1}][{2}], leave unit[{3}]", ++sendCount,
                    unit.objectId,
                    _joinedUnit.ClassType.ToString(),
                    _leftUnitObjetId);
            }

            _joinedUnit = null;
            _leftUnitObjetId = string.Empty;
        }
        
        public void NtfUpdateDungeonInfo()
        {
            var sharedDungeon = new SharedDungeon();
            sharedDungeon.Init(_adventureDungeon);
            
            foreach (var conn in _connectionList)
            {
                var unit = new SharedCharacter();
                unit.Init(_joinedUnit);
                conn.SC_UPDATE_DUNGEON_INFO(sharedDungeon);

            }
        }

        public void DungeonStateWaitLog(string dungeonState)
        {
            if (0 == (++_count % 100))
            {
                //CorgiLog.Log(CorgiLogType.Warning, "Dungeon state[{0}],  client will be freeze. roomid[{1}]", dungeonState, _roomId);
                _count = 0;
            }
        }
        
        // private Unit _willJoinUnit = null;
        // private string _willLeaveUnitDbId = string.Empty;

        private Unit _joinedUnit = null;
        private string _leftUnitObjetId = string.Empty;

        private volatile int _count = 0;
    }
}