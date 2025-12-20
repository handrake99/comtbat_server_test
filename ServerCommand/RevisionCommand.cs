using System;
using IdleCs.Logger;
using Newtonsoft.Json.Linq;

using IdleCs.Managers;
using IdleCs.ServerContents;
using IdleCs.Utils;



namespace IdleCs.CombatServer.ServerCommand
{
    public class RevisionCommand : RedisCommand
    {
        public RevisionCommand()
        {
            CommandType = CommandType.Revision;
        }

        public override void Invoke(JObject json)
        {
            if (CorgiJson.IsValidInt(json, "revision") == false)
            {
                CorgiLog.LogError("invalid commnad parameter for Revision\n");
                return;
            }

            var revision = CorgiJson.ParseInt(json, "revision");
            CorgiLog.LogLine("Receive Revision Updated Command {0}", revision);

            bool changedRevision = false;
            var curRevision = ServerGameDataManager.Instance.CurRevision; 
            
            try
            {
                changedRevision = ServerGameDataManager.Instance.LoadData();
            }
            catch (Exception e)
            {
                CorgiLog.Log(CorgiLogType.Fatal, "Occur exception[{0}] when revision command coming", e.ToString());
            }

            if (false == changedRevision)
            {
                CorgiLog.Log(CorgiLogType.Error, "Can't change revision[{0}]", revision);
                return;
            }

            CorgiLog.Log(CorgiLogType.Info, "Changed revision [{0}] to [{1}]", curRevision, revision);
            
            Console.Title = String.Format("combat server index[{0}] ip[{1}] port:[{2}], revision[{3}]",
                CombatServerConfig.Instance.Server.Index,
                CombatServerConfig.Instance.Server.UserBindIP,
                CombatServerConfig.Instance.Server.UserBindPort,
                GameDataManager.Instance.Revision);
        }
    }
}
