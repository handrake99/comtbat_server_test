using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using IdleCs.Network.NetLib;
using IdleCs.Utils;
using StackExchange.Redis;

namespace IdleCs.ServerCore
{
    public interface IServerObject
    {
        ulong Id();
        void Serialize(IPacketWriter writer);
        void DeSerialize(IPacketReader reader);
    }

    public interface ISerialiableServerObject
    {
        CorgiSerializer GetSerializer();
    }

    public class CorgiServerObject : IServerObject, ISerialiableServerObject
    {
        private ulong _id = 0;
        private CorgiSerializer _serializer;

        public bool IsRunning { get; protected set; }
        public CorgiSerializer GetSerializer()
        {
            return _serializer;
        }

        public ulong Id()
        {
            return _id;
        }

        public CorgiServerObject()
        {
            _serializer = new CorgiSerializer();
            _id = 0;
            IsRunning = true;
        }

        /// <summary>
        /// for Serialized Methods
        /// 1. define serialized Method by private with "_Serialized"
        /// 2. call by SerializeMethod with params
        /// </summary>
        /// <param name="methodName"></param>
        /// <param name="inParams"></param>
        /// <exception cref="CorgiException"></exception>
        public void SerializeMethod(string methodName, params Object[] inParams)
        {
            SerializeMethod(this, methodName, inParams);
//            try
//            {
//                if (methodName == String.Empty)
//                {
//                    throw new CorgiException("invalid method name ({0})", methodName);
//                }
//
//                var functionName = string.Format("{0}_Serialized", methodName);
//                var type = this.GetType();
//                var method = type.GetMethod(functionName, BindingFlags.NonPublic | BindingFlags.Instance);
//                if (method == null)
//                {
//                    throw new CorgiException("invalid method.  should implement ({0})", functionName);
//                }
//                if (method.IsPrivate == false)
//                {
//                    throw new CorgiException("invalid method constraint. should be private ({0})", methodName);
//                }
//
//                var task = new CorgiSerializerTask(this, methodName, inParams);
//                
//                _serializer.Serialize(task);
//
//            }
//            catch (Exception e)
//            {
//                CorgiLog.LogError(e.Message);
//                CorgiLog.LogError("[ERROR] method : {0}");
//                
//                // Just Logging
//                return;
//            }
        }

        public static void SerializeMethod(ISerialiableServerObject thisObject, string methodName, params Object[] inParams)
        {
            try
            {
                if (methodName == String.Empty)
                {
                    throw new CorgiException("invalid method name ({0})", methodName);
                }

                var functionName = string.Format("{0}_Serialized", methodName);
                var type = thisObject.GetType();
                var method = type.GetMethod(functionName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                if (method == null)
                {
                    throw new CorgiException("invalid method.  should implement ({0})", functionName);
                }
//                if (method.IsPrivate == false)
//                {
//                    throw new CorgiException("invalid method constraint. should be private ({0})", methodName);
//                }

                var task = new CorgiSerializerTask(thisObject, methodName, inParams);

                var serializer = thisObject.GetSerializer();
                serializer.Serialize(task);

            }
            catch (Exception e)
            {
                CorgiLog.LogError(e.Message);
                CorgiLog.LogError("[ERROR] method : {0}", methodName);
                
                // Just Logging
                return;
            }
            
        }
        
        public virtual void Serialize(IPacketWriter writer)
        {
            throw new System.NotImplementedException();
        }

        public virtual void DeSerialize(IPacketReader reader)
        {
            throw new System.NotImplementedException();
        }

        protected void DoDestroy()
        {
            _serializer.DoDestroy();
        }

        public bool HaveToDestroy()
        {
            return _serializer.HaveToDestroy();
        }

    }
}