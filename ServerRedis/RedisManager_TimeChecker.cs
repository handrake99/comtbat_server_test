using System;
using System.Collections.Concurrent;
using System.Data;

using Newtonsoft.Json.Linq;

using IdleCs.CombatServer;
using IdleCs.Logger;
using IdleCs.Network;
using IdleCs.Utils;

using IdleCs.CombatServer.ServerCommand;
using CommandType = IdleCs.CombatServer.ServerCommand.CommandType;


namespace IdleCs.Managers
{
    public partial class RedisManager
    {
        public class Record
        {
            public Record(string key, string task)
            {
                DateTime = DateTime.Now;
                TaskName = task;
            }
            
            public string Key { get; set; }
            public DateTime DateTime { get; set; }
            public string TaskName { get; set; }
        }

        public bool Push(string key, string task)
        {
            Record oldRecord = null;
            if (false == _taskRecords.TryGetValue(key, out oldRecord))
            {
                var newRecord = new Record(key, task);
                CorgiLog.Log(CorgiLogType.Info, "[redis time checker]");
                
                
                return _taskRecords.TryAdd(key, newRecord);
            }

            CorgiLog.Log(CorgiLogType.Error, "Overlapped key[{0}], task[{1}], time[{2}]", key, oldRecord.TaskName, oldRecord.DateTime.ToString("yyyy/MM/dd hh:mm:ss.fff"));
            return false;
        }

        //-if retrive false. then ..  
        public bool Pop(string key)
        {
            
            //-working

            return false;
        }

        private bool ValidCommandTime(JObject commandJson, CommandType commandType)
        {
            var strCommandTimeStamp = CorgiJson.ParseLong(commandJson, "timestamp");
            
            var commandTimeStamp = Convert.ToUInt64(strCommandTimeStamp);
            var currentTimeStamp = CorgiTime.UtcNowULong;
            
            var maxWaitTimeMs = CombatServerConfig.Instance.Server.RedisRequestTimeOutMS;  
            if (0 >= maxWaitTimeMs)
            {
                //-value is 0 or minus, don't check              
                return true;
            }

            var lateTimeMs = currentTimeStamp - commandTimeStamp;
            if (maxWaitTimeMs > lateTimeMs)
            {
                //-no problem
                return true;
            }

            var roomId = CorgiJson.ParseString(commandJson, "roomId");
            
            CorgiLog.Log(CorgiLogType.Fatal, "Error, RoomId[{0}] Coming command[{1}] from web. has called utc time[{2}][{3}]. current utc time [{4}][{5}]. lateTimeMS[{6}]. max wait time ms[{7}]",
                roomId,
                commandType.ToString(), 
                CorgiTime.ToDateTime(commandTimeStamp),
                commandTimeStamp, 
                CorgiTime.ToDateTime(currentTimeStamp),
                currentTimeStamp,
                lateTimeMs,
                maxWaitTimeMs);

            return false;
        }

        private ConcurrentDictionary<string, Record> _taskRecords = null;
    }
    
}