using System;
using System.Reflection;
using System.Diagnostics;
using IdleCs.GameLogic;
using IdleCs.Logger;
using IdleCs.ServerUtils;
using IdleCs.Utils;

//using UnityEngine;
using Object = System.Object;

namespace IdleCs.ServerCore
{
    public class CorgiSerializerTask
    {
        private Object _thisObject;
        private string _methodName;
        private Object[] _params;
        
        public CorgiSerializerTask(Object inObject, string methodName, params Object[] inParams)
        {
            _thisObject = inObject;
            _methodName = methodName;
            _params = inParams;
        }

        public void Process()
        {
            try
            {
                var functionName = string.Format("{0}_Serialized", _methodName);
                var type = _thisObject.GetType();
                var method = type.GetMethod(functionName, BindingFlags.NonPublic | BindingFlags.Instance);

                if (method == null)
                {
                    throw new CorgiException("invalid method name {0}", functionName);
                }

                method.Invoke(_thisObject, _params);
            }
            catch (Exception e)
            {
                LogHelper.LogException(e, "Occur exception : called {0}.{1}", _thisObject.GetType().Name, _methodName);
                
                //Debug.Assert(false);//-테스트 기간 동안 일단 죽여야 한다. 예외가 발생했을 때, 스택을 건너 뛰면 더 큰 예외가 발생하는 경우가 있음.!!
                
            }
        }
    }
}