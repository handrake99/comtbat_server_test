using System;
using IdleCs.Utils;
using Newtonsoft.Json.Linq;

namespace IdleCs.CombatServer.ServerCommand
{
    public enum CommandType
    {
        None = 0
        // Subscribe Command
        , Log                        //-receive
        , Revision                   //-receive
        // ServerCommand
        //, Join                     //-N/A          
        , StageFinish = 4            //-send
        , StageCompleted             //-receive
        , ChallengeStart             //-receive
        , ChallengeFinish            //-send
        , ChallengeCompleted         //-receive
        , AutoHuntingStart           //-receive
        , InstanceDungeonStart       //-receive
        , InstanceDungeonFinish      //-send
        , InstanceDungeonCompleted   //-receive
        , InstanceDungeonStop        //-receive
        , PartyJoin                  //-receive
        , PartyLeave                 //-receive
        , PartyExile                 //-receive
        , RoomDeleted                //-send
        , EventAcquireSkillItem      //-receive
        , EventAcquireEquipItem      //-receive
        
        , RoomStatus                 //-receive
        , RoomKill                 //-receive
        
        
        , WorldBossFinish          //-Send
        , WorldBossCompleted          //-receive
        , WorldBossStop             //-receive
        , WorldBossDead             //-send
        
        , RiftOpen                  //-receive
        , RiftFinish                //-send
        , RiftCompleted             //-receive
        , RiftStop                  //-receive
        , RiftDead                  //-send
        
        , PvpFinish               //-send
        , PvpCompleted            //-receive
        , PvpStop                 //-receive
    }
    
    public abstract class RedisCommand
    {
        public CommandType CommandType { get; protected set; }
        public virtual void Invoke(JObject json)
        {
            throw new NotImplementedException();
        }
        
        protected bool CheckCommandJson(JObject json)
        {
            if (json == null)
            {
                return false;
            }

            if (CorgiJson.IsValidString(json, "roomId") == false)
            {
                return false;
            }
            
            return true;
        }
    }
}