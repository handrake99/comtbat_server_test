using System;
using System.Text;
using IdleCs.Logger;
using IdleCs.Managers;
using IdleCs.Network;
using IdleCs.Utils;
using Newtonsoft.Json.Linq;


namespace IdleCs.ServerUtils
{
    /*
[just use log]
    
    <<log usage>>  
        CorgiLog.Log(CorgiLogType.Debug, "{0}", "Debug");
        CorgiLog.Log(CorgiLogType.Error, "{0}", "Error");
        CorgiLog.Log(CorgiLogType.Fatal, "{0}", "Fatal");
        CorgiLog.Log(CorgiLogType.Info, "{0}", "Info");
        CorgiLog.Log(CorgiLogType.Warn, "{0}", "Warn");
     
     
[enum LogType]

    <<summary>>     
     //-you can add anything what helpful type for tracing. 
     
    <<LogType usage>> 
        CorgiLog.Log(CorgiLogType.Info, "[{0}] {1}", LogType.Combat.ToString(), "blah blah..");
     */    
    public enum LogType
    {
        None = 0,
        Login = 1,
        Combat = 3,
        Exception = 4,
        Protocol = 10,
        
        Room = 100,
        RoomNoConnenction = 101,
        RoomConnectionInvalid = 102,
        RoomNoTick = 103,
        RoomFailedForceRemove = 104
    }

    
    
    /*
[class LogHelper]
     
     <<summary>>
            you can write 'login log' easily with room-id and character-id
     
     <<ussage/test>>
            string nick = "sim";
            string skill = "active";

            LogHelper.LoginLog("teset1", "1234", null, true);
            LogHelper.LoginLog("teset2", "", null);
            LogHelper.LoginLog("teset3", null, null);
            LogHelper.LoginLog($"teset4 {nick}", "1234", null, true);
            LogHelper.LoginLog($"teset5 {skill}", "", null);
            LogHelper.LoginLog($"teset6 {nick} {skill}", null, null); 
     
     <<result>>
            log file was written as below
     
            2021-04-21 16:44:16,656 [ERROR] - [Login], characterID[1234] roomID[null] - teset1
            2021-04-21 16:44:16,656 [INFO ] - [Login], characterID[null] roomID[null] - teset2
            2021-04-21 16:44:16,657 [INFO ] - [Login], characterID[null] roomID[null] - teset3
            2021-04-21 16:44:16,657 [ERROR] - [Login], characterID[1234] roomID[null] - teset4 sim
            2021-04-21 16:44:16,657 [INFO ] - [Login], characterID[null] roomID[null] - teset5 active
            2021-04-21 16:44:16,657 [INFO ] - [Login], characterID[null] roomID[null] - teset6 sim active     
     */
    public class LogHelper
    {
        static void Log(
            LogType type, CorgiLogType logLevel, string roomId, string characterId, string nickname, string message)
        {
            string logStr =
                $"[{type}], nickName[{nickname}] : {message}";

            // log to console&file
            CorgiLog.Log(logLevel, logStr);

            if (logLevel == CorgiLogType.Fatal || logLevel == CorgiLogType.Error)
            {
                JObject jsonObject = new JObject();
                jsonObject.Add("ct", CorgiTime.UtcNowULong);
                jsonObject.Add("logLevel", logLevel.ToString());
                jsonObject.Add("category", type.ToString());
                jsonObject.Add("roomId", roomId);
                jsonObject.Add("characterId", characterId);
                jsonObject.Add("nickname", nickname);
                jsonObject.Add("message", message);
                
                // log to redis&admintool
                RedisManager.Instance.SerializeMethod("Log", jsonObject);
            }
        }

        public static void LogAPI(
            LogType type, string roomId, string characterId, string nickname, string message)
        {
            var logLevel = CorgiLogType.Debug;
            
            JObject jsonObject = new JObject();
            jsonObject.Add("ct", CorgiTime.UtcNowULong);
            jsonObject.Add("logLevel", logLevel.ToString());
            jsonObject.Add("category", type.ToString());
            jsonObject.Add("roomId", roomId);
            jsonObject.Add("characterId", characterId);
            jsonObject.Add("nickname", nickname);
            jsonObject.Add("message", message);
            
            RedisManager.Instance.SerializeMethod("Log", jsonObject);
        }
        
        public static void LogRoom(
            LogType type, string roomId, string characterId, string nickname, string message)
        {
            var logLevel = CorgiLogType.Debug;
            
            JObject jsonObject = new JObject();
            jsonObject.Add("ct", CorgiTime.UtcNowULong);
            jsonObject.Add("logLevel", logLevel.ToString());
            jsonObject.Add("category", type.ToString());
            jsonObject.Add("roomId", roomId);
            jsonObject.Add("characterId", characterId);
            jsonObject.Add("nickname", nickname);
            jsonObject.Add("message", message);
            
            RedisManager.Instance.SerializeMethod("Log", jsonObject);
        }
        
        public static void LogLogin(string contents, string characterId, string roomId, bool isError = false)
        {
            var logLevel = (isError) ? (CorgiLogType.Error) : (CorgiLogType.Info);
            var name = characterId;
            Log(LogType.Login, logLevel, roomId, characterId, name, contents);
            // string format = "[" + LogType.Login.ToString() + "]" + ", characterID[{0}] roomID[{1}] - " + contents;
            // CorgiLog.Log(((true == isError) ? (CorgiLogType.Error) : (CorgiLogType.Info)), format, (string.IsNullOrEmpty(characterID) ? "null" : characterID), (string.IsNullOrEmpty(roomID) ? "null" : roomID));
        }
        public static void LogException(Exception exception, string format, params object[] args)
        {
            var finalMessage = string.Format(format, args) + "\n"+exception;
            
            Log(LogType.Exception, CorgiLogType.Fatal
                , string.Empty, string.Empty, string.Empty, finalMessage);
        }

        public static void LogCombatError(string roomId, string characterId, string nickname,
            string message)
        {
            Log(LogType.Combat, CorgiLogType.Error, roomId, characterId, nickname, message);
        }
        
        public static void LogCombatFatal(string roomId, string characterId, string nickname,
            string message, Exception e)
        {
            var finalMessage = message +"\n"+ e.ToString();
            Log(LogType.Combat, CorgiLogType.Fatal, roomId, characterId, nickname, finalMessage);
        }
    }
} 