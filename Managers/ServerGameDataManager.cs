

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using IdleCs.CombatServer;
using IdleCs.Library;
using IdleCs.Logger;
using IdleCs.Utils;

using Newtonsoft.Json;

namespace IdleCs.Managers
{
    public class ServerGameDataManager : Singleton<ServerGameDataManager>
    {
        Dictionary<uint, GameDataManager> _gameDataMap = new Dictionary<uint, GameDataManager>();

        private uint _curRevision = 0;

        public uint CurRevision => _curRevision;
        
        public ServerGameDataManager()
        {}
        
        public GameDataManager GameData
        {
            get { return _gameDataMap[_curRevision]; }
        }
        
        
        private const string BIN_DATA = "binData";
        
        public bool LoadData()
        {
            var revisionInfo = GetRevisionInfo();
            if (revisionInfo == null)
            {
                CorgiLog.Log(CorgiLogType.Error, "revisionInfo is null");
                return false;
            }
            
            var revision = uint.Parse(revisionInfo.revisionInfo.revision);
            _curRevision = revision;
            
            if (_gameDataMap.ContainsKey(revision) )
            {
                return true;
            }
            
            var gameDataRoot = $"{BIN_DATA}_{CombatServerConfig.Instance.ServerIndex}";

            var downloadRoot = $"{gameDataRoot}/rev_{revision}";

            if (!Directory.Exists(downloadRoot))
            {
                Directory.CreateDirectory(downloadRoot);
            }

            var webClient = new WebClient();
            
            foreach (var fileName in revisionInfo.revisionInfo.fileList)
            {
                var url = $"{revisionInfo.revisionInfo.path}/{fileName}";
                try
                {
                    CorgiLog.Log(CorgiLogType.Info, $"Download {fileName} ({revision})");   
                    webClient.DownloadFile(url, $"{downloadRoot}/{fileName}");
                }
                catch (Exception e)
                {
                    CorgiLog.Log(CorgiLogType.Error, "Fail to download : {0}, exception : {1}", url, e.ToString());
                    return false;
                }
            }
            
            CorgiLog.Log(CorgiLogType.Info, $"GameData Download Completed ({revision})");
           
            var newManager = new GameDataManager();
            newManager.LoadLocalGameDataFiles(gameDataRoot, revision);
            if (newManager.GameDataReady == false)
            {
                CorgiLog.Log(CorgiLogType.Error, "Failed to load game data files. revision : {0}", revision);
                return false;
            }
            
            _gameDataMap.Add(revision, newManager);
            GameDataManager.InitSingleton(newManager);
            
            CorgiLog.Log(CorgiLogType.Info, "GameData Load Completed. revision : {0}", revision);
            return true;
        }
        
        [Serializable]
        private class RevisionInfo
        {
            // public string id;
            public string revision ="";
            public string path = "";
            public string[] fileList = null;
            // public string[] checkSumList;
            // public string ct;
        }
        
        [Serializable]
        private class RevisionInfoResponse
        {
            public RevisionInfo revisionInfo = null;
        }
        
        private static RevisionInfoResponse GetRevisionInfo()
        {
            try
            {
                var jsonString = RedisManager.Instance.GetRevisionInfo();
                var revisionInfo = JsonConvert.DeserializeObject<RevisionInfoResponse>(jsonString);
                return revisionInfo;
            }
            catch (Exception e)
            {
                CorgiLog.LogError(e);
                return null;
            }
            
        }
    }
}