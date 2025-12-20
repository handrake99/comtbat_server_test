using IdleCs.ServerContents;
using IdleCs.Utils;
using Newtonsoft.Json.Linq;

namespace IdleCs.CombatServer.ServerCommand
{
    public class LogCommand : RedisCommand
    {
        public LogCommand()
        {
            CommandType = CommandType.Log;
        }

        public override void Invoke(JObject json)
        {
            if (CorgiJson.IsValidString(json, "message") == false)
            {
                CorgiLog.LogError("invalid commnad parameter for Log\n");
                return;
            }

            var message = CorgiJson.ParseString(json, "message");
            
            CorgiLog.LogLine("Log Meessage {0}", message);
        }
    }
}