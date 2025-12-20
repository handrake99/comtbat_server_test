using System;
using IdleCs.GameLogic;
using IdleCs.Utils;

using log4net.Core;


namespace IdleCs.Logger
{
    public class CorgiLogServer : ICorgiLog
    {
        private log4net.ILog _log;
        
        public CorgiLogServer() {}

        public bool Initialize(Type type, string logPath)
        {
            try
            {
                log4net.GlobalContext.Properties["LogFilePath"] = logPath;
                log4net.Config.XmlConfigurator.Configure();
                this._log = log4net.LogManager.GetLogger(type);
                
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        public void UnInitialize()
        {
            if (null != _log)
                this._log = null;
        }

        public bool IsDebugEnabled { get { return (null == this._log) ? false : this._log.IsDebugEnabled; } }
        public bool IsErrorEnabled { get { return (null == this._log) ? false : this._log.IsErrorEnabled; } }
        public bool IsFatalEnabled { get { return (null == this._log) ? false : this._log.IsFatalEnabled; } }
        public bool IsInfoEnabled { get { return (null == this._log) ? false : this._log.IsInfoEnabled; } }
        public bool IsWarnEnabled { get { return (null == this._log) ? false : this._log.IsWarnEnabled; } }


        public void Debug(object message)
        {
            _log?.Debug(message);
        }

        public void Info(object message)
        {
            _log?.Info(message);
        }

        public void Warn(object message)
        {
            _log?.Warn(message);
        }

        public void Error(object message)
        {
            _log?.Error(message);
        }

        public void Fatal(object message)
        {
            _log?.Fatal(message);
        }

    }
}