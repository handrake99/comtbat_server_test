using System; 
using IdleCs.Network;

namespace IdleCs.ServerSystem
{
    public class AliveSignal
    {
         public DateTime Past { get; set; }

         public AliveSignal()
         {
             Past = DateTime.Now;
         }
    }
}