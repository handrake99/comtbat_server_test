using System;
using IdleCs.Utils;
using IdleCs.GameLogic;
using IdleCs.Managers;


namespace IdleCs.ServerContents
{
    public partial class RoomManager
    {
        void OnSendChatting_Serialized(CorgiServerConnection conn, ChattingType chattingType, string data)
        {
            string roomId = conn.RoomId;
            string characterId = conn.CharacterId;

            if ((string.IsNullOrEmpty(roomId))
                ||  (string.IsNullOrEmpty(characterId)))
            {
                throw new CorgiException("invalid parameter for instance party leave");
            }

            Room room = null;
            if (_roomMap.TryGetValue(roomId, out room) == false)
            {
                throw new CorgiException("failed to instance send chatting ({0})", roomId);
            }
          
            room.SerializeMethod("OnSendChatting", conn, chattingType, data);
            CorgiLog.LogLine("OnSend Chatting ({0})",  roomId);
        }

    }
}

