using System.Net;
using System.Net.Sockets;
using System.Text;
using IdleCs.GameLogic;
using IdleCs.Logger;
using IdleCs.Managers;
using IdleCs.Network.NetLib;
using IdleCs.Utils;

using IdleCs.ServerUtils;

namespace IdleCs.ServerContents
{
    // for client(app) to combat server connection
    public partial class CorgiServerConnection : TConnection<CorgiServerConnection>
    {
        // sync by self
        private ulong LastRecvTime = 0;
        
        // sync by join
        public string RoomId;
        public string CharacterId;
        
        // sync by room
        public UserState UserState;
            
        // sync by chatting manager
        public ChattingType ChattingType;
        public string ChannelKey;
        public int ChannelIndex;
        

        public CorgiServerConnection()
        {
            UserState = UserState.Connected;
            ChannelIndex = 0;
        }
        
        public override bool Init(Socket InSocket)
        {
            if (base.Init(InSocket) == false)
            {
                return false;
            }

            return true;
        }
        
        protected void CheckConnection_Serialized(long reqId, string token)
        {
            LastRecvTime = CorgiTime.UtcNowULong;

            SC_CHECK_CONNECTION(reqId, token);
        }

        public override void OnConnect()
        {
            var endPoint = this.mSocket.RemoteEndPoint as IPEndPoint;
            if (endPoint != null)
            {
                //CorgiLog.Log(CorgiLogType.Debug, "[{0}] Connected from {1}:{2}", LogType.Login.ToString(), endPoint.Address, endPoint.Port);
            }
            else
            {
                CorgiLog.Log(CorgiLogType.Error, "[{0}] Unknown connected", LogType.Login.ToString());
            }
            
            StatDataManager.Instance.Increment(StatisticType.ConnectionCount, 1);
        }

        public override bool Receive()
        {
            return base.Receive();
        }

        public override bool Send(SendPacketBufferUnit Unit)
        {
            return base.Send(Unit);
        }

        public override int OnReceive()
        {
            // ping pong test
            // var recvLength = mRecvPacketBuffer.Length;
            // var recvStr = Encoding.Default.GetString(mRecvPacketBuffer.Data, 0, recvLength);
            // CorgiLog.LogLine(recvStr);
            //
            // var sendBuffer = Encoding.Default.GetBytes(recvStr);
            //
            // var sendBufferUnit = new SendPacketBufferUnit(sendBuffer);
            // Send(sendBufferUnit);
            
            return base.OnReceive();
        }

        public override void OnSend()
        {
            //CorgiLog.LogLine("\nsend completed\n");
        }

        public override void OnClose()
        {
            var endPoint = this.mSocket.RemoteEndPoint as IPEndPoint;

            if (endPoint != null)
            {
                CorgiCombatLog.Log(CombatLogCategory.User,"OnClose Connection", CharacterId, RoomId);
            }
            
            RoomManager.Instance.SerializeMethod("OnClose",this);
            ChattingManager.Instance.SerializeMethod("RemoveConnection", this);
            
            StatDataManager.Instance.Decrement(StatisticType.ConnectionCount, 1);
            
            base.OnClose();
        }
        
        
    }
}