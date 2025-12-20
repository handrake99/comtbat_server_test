using System;
using System.Collections.Concurrent;
using System.Threading;
using IdleCs.Utils;

namespace IdleCs.ServerCore
{
    
    public class CorgiSerializer
    {
        ConcurrentQueue<CorgiSerializerTask> _queue = new ConcurrentQueue<CorgiSerializerTask>();
        private long _count = 0;
        private long _isRunning = 0;
        private bool _isDestroy = false; // for destroy
        
        public CorgiSerializer()
        {
            
        }

        public void Serialize(CorgiSerializerTask newTask)
        {
            //int ret = 0;
            long count = 0;
            if (Interlocked.CompareExchange(ref _count, (long) 1, (long) 0) == 0)
            {
                newTask.Process();
                
                // free new task

                if (Interlocked.Decrement(ref _count) > 0)
                {
                    RunQueue();
                }
                
                return ;
            }
            else
            {
                count = Enqueue(newTask);
                if (count == 1)
                {
                    RunQueue();
                }
            }
        }

        public void RunQueue()
        {
            CorgiSerializerTask curTask = null;

            do
            {
                if (_isDestroy)
                {
                    return;
                }
#if DEBUG
                if (Interlocked.CompareExchange(ref _isRunning, (long)1, 0) != 0)
                {
                    throw new CorgiException("Queue is not Running");
                }
#endif
                curTask = Dequeue();
                if (curTask == null)
                {
                    throw new CorgiException("invalid task in queue ({})");
                }

                curTask.Process();
                
#if DEBUG
                if (Interlocked.CompareExchange(ref _isRunning, 0, 1) != 1)
                {
                    throw new CorgiException("Queue is Stopped");
                }
#endif
            } while (Interlocked.Decrement(ref _count) > 0);
        }

        public long Enqueue(CorgiSerializerTask newTask)
        {
            _queue.Enqueue(newTask);
            return Interlocked.Increment(ref _count);
        }

        public CorgiSerializerTask Dequeue()
        {
            CorgiSerializerTask retTask;
            while (_queue.TryDequeue(out retTask) == false) ;
            return retTask;
        }

        // should be called by inner Serialized Fucntion 
        public void DoDestroy()
        {
            _isDestroy = true;
        }
        
        public bool HaveToDestroy()
        {
            return _isDestroy;
        }
    }
}