using System.Threading;
using System.Threading.Tasks;

using IdleCs.Logger;
using IdleCs.Utils;


namespace IdleCs.ServerUtils
{
    public class TestHolder
    {
        public static void Hold(int index, int holdMs)
        {
            if (0 >= holdMs)
            {
                return;
            }
            
            Task holder = new Task(() =>
            {
                CorgiLog.Log(CorgiLogType.Info, "index[{0}], start hold", index);
                Thread.Sleep(holdMs);               
            });
            
            holder.Start();
            holder.Wait();
            CorgiLog.Log(CorgiLogType.Info, "index[{0}], end hold", index);
        }       
    }


    public class TestHolderManager
    {
        public void Reset(int holdTime)
        {
            HoldTime = holdTime;
        }

        public void Process()
        {
            TestHolder.Hold(0, HoldTime);
        }

        private int HoldTime
        {
            get
            {
                lock (_lock)
                {
                    return _holdTime;
                }
            }
            set
            {
                lock (_lock)
                {
                    _holdTime = value;
                }
            }
        }
        
        private object _lock = new object();
        private int _holdTime = 0;
        
        //-
        public static TestHolderManager Instance
        {
            get
            {
                lock (PadLock)
                {
                    if (_instance == null) {  _instance = new TestHolderManager();  }
                    return _instance;
                }
            }
        }
        
        private static TestHolderManager _instance = null;
        private static readonly object PadLock = new object();
    }
    
}