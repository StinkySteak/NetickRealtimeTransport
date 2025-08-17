using System;

namespace Netick.Transport
{
    public interface IRealtimeEventListener
    {
        void OnPeerConnected(RealtimePeer peer);
        void OnPeerDisconnected(RealtimePeer peer);
        void OnNetworkReceive(RealtimePeer peer, ArraySegment<byte> bytes);
        void OnConnectedToRoom();
        void OnCreateRoomFailed();
        void OnDisconnectedFromRealtime();
    }
}
