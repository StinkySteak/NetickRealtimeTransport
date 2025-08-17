using Photon.Client;
using Photon.Realtime;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Netick.Transport
{
    internal class RealtimeNetManager : IConnectionCallbacks, IMatchmakingCallbacks, IOnEventCallback, IInRoomCallbacks
    {
        private RealtimeClient _realtimeClient;
        private NetickEngine _netickEngine;
        private AppSettings _appSettings;

        private string _connectRoomCode;
        private IRealtimeEventListener _listener;
        private const int EventCodeMessage = 1;
        private const int EventCodeKickSelf = 10;

        private Dictionary<int, RealtimePeer> _peers;
        private int[] _sendTargetActorNumber;

        private const string JoinCodePool = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const int JoinCodeLength = 5;

        public void Init(NetickEngine engine, AppSettings appSettings, IRealtimeEventListener listener)
        {
            _netickEngine = engine;
            _appSettings = appSettings;
            _listener = listener;
            _peers = new Dictionary<int, RealtimePeer>(engine.Config.MaxPlayers);

            _realtimeClient = new RealtimeClient(ConnectionProtocol.Udp);
            _realtimeClient.AddCallbackTarget(this);

            _sendTargetActorNumber = new int[1];
        }

        public void Shutdown()
        {
            if (_realtimeClient != null && _realtimeClient.IsConnected)
                _realtimeClient.Disconnect();
        }

        public void Kick(RealtimePeer peer)
        {
            SendKickMessage(peer);
        }

        public void SendKickMessage(RealtimePeer peer)
        {
            SendOptions sendOptions = new()
            {
                Reliability = true
            };

            _sendTargetActorNumber[0] = peer.RealtimePlayer.ActorNumber;

            RaiseEventArgs eventArgs = new();
            eventArgs.TargetActors = _sendTargetActorNumber;

            _realtimeClient.OpRaiseEvent(EventCodeKickSelf, null, eventArgs, sendOptions);
        }

        public void HostRoom()
        {
            _realtimeClient.ConnectUsingSettings(_appSettings);
        }

        public void Connect(string roomCode)
        {
            _connectRoomCode = roomCode;
            _realtimeClient.ConnectUsingSettings(_appSettings);
        }

        public bool TryGetRoomCode(out string roomCode)
        {
            if (_realtimeClient.InRoom)
            {
                roomCode = _realtimeClient.CurrentRoom.Name;
                return true;
            }

            roomCode = null;
            return false;
        }

        public void PollUpdate()
        {
            _realtimeClient.Service();
        }
        public void Send(RealtimePeer peer, IntPtr ptr, int length, bool isReliable)
        {
            SendOptions sendOptions = new()
            {
                Reliability = isReliable
            };

            ByteArraySlicePool pool = _realtimeClient.RealtimePeer.ByteArraySlicePool;
            ByteArraySlice slice = pool.Acquire(length);
            slice.Count = length;
            Marshal.Copy(ptr, slice.Buffer, slice.Offset, length);

            _sendTargetActorNumber[0] = peer.RealtimePlayer.ActorNumber;

            RaiseEventArgs eventArgs = new();
            eventArgs.TargetActors = _sendTargetActorNumber;

            // slice is automatically returned to pool on OpRaiseEvent called
            _realtimeClient.OpRaiseEvent(EventCodeMessage, slice, eventArgs, sendOptions);
        }


        void IConnectionCallbacks.OnConnectedToMaster()
        {
            _realtimeClient.RealtimePeer.ReuseEventInstance = true;
            _realtimeClient.RealtimePeer.UseByteArraySlicePoolForEvents = true;

            if (_netickEngine.IsServer)
            {
                EnterRoomArgs args = new EnterRoomArgs();
                args.RoomName = GenerateRoomCode();

                _realtimeClient.OpCreateRoom(args);
            }
            else if (_netickEngine.IsClient)
            {
                EnterRoomArgs args = new();
                args.RoomName = _connectRoomCode;
                _realtimeClient.OpJoinRoom(args);
            }
        }

        private string GenerateRoomCode()
        {
            char[] joinCode = new char[JoinCodeLength];

            for (int i = 0; i < JoinCodeLength; i++)
            {
                int randomIndexFromPool = UnityEngine.Random.Range(0, JoinCodePool.Length);

                char randomChar = JoinCodePool[randomIndexFromPool];

                joinCode[i] = randomChar;
            }

            return new string(joinCode);
        }

        void IConnectionCallbacks.OnDisconnected(DisconnectCause cause)
        {
            _listener.OnDisconnectedFromRealtime();
        }


        void IMatchmakingCallbacks.OnCreateRoomFailed(short returnCode, string message)
        {
            _listener.OnCreateRoomFailed();
        }

        void IMatchmakingCallbacks.OnJoinedRoom()
        {
            if (_netickEngine.IsClient)
            {
                RealtimePeer peer = new RealtimePeer(this);
                peer.RealtimePlayer = _realtimeClient.CurrentRoom.Players[_realtimeClient.CurrentRoom.MasterClientId];

                SplitAddress(_realtimeClient.GameServerAddress, out string addr, out int port);
                peer.SetEndPoint(addr, port);

                _peers.Add(_realtimeClient.CurrentRoom.MasterClientId, peer);
                _listener.OnPeerConnected(peer);
            }

            _listener.OnConnectedToRoom();
        }

        void IMatchmakingCallbacks.OnJoinRoomFailed(short returnCode, string message)
        {
            _listener.OnPeerDisconnected(null);
        }


        void IMatchmakingCallbacks.OnLeftRoom()
        {
        }

        void IOnEventCallback.OnEvent(EventData photonEvent)
        {
            if (photonEvent.Code == EventCodeMessage)
            {
                if (_peers.TryGetValue(photonEvent.Sender, out RealtimePeer fromPeer))
                {
                    ByteArraySlice arraySlice = (ByteArraySlice)photonEvent.CustomData;

                    ArraySegment<byte> bytes = new ArraySegment<byte>(arraySlice.Buffer, arraySlice.Offset, arraySlice.Count);

                    _listener.OnNetworkReceive(fromPeer, bytes);
                    arraySlice.Release();
                }
            }
            else if (photonEvent.Code == EventCodeKickSelf)
            {
                Debug.Log("I received a code to kick myself!");
                _realtimeClient.Disconnect();

                if (_peers.TryGetValue(photonEvent.Sender, out RealtimePeer fromPeer))
                {
                    _listener.OnPeerDisconnected(fromPeer);
                }
            }
        }

        void IInRoomCallbacks.OnPlayerEnteredRoom(Player newPlayer)
        {
            RealtimePeer peer = new RealtimePeer(this);
            peer.RealtimePlayer = newPlayer;

            SplitAddress(_realtimeClient.GameServerAddress, out string addr, out int port);
            peer.SetEndPoint(addr, port);

            _peers.Add(newPlayer.ActorNumber, peer);
            _listener.OnPeerConnected(peer);
        }

        private void SplitAddress(string realtimeEndpoint, out string address, out int port)
        {
            if (!realtimeEndpoint.Contains("://"))
                realtimeEndpoint = "dummy://" + realtimeEndpoint;

            Uri uri = new Uri(realtimeEndpoint);

            address = uri.Host;
            port = uri.Port;
        }

        void IInRoomCallbacks.OnPlayerLeftRoom(Player otherPlayer)
        {
            if (_peers.TryGetValue(otherPlayer.ActorNumber, out RealtimePeer peer))
            {
                _listener.OnPeerDisconnected(peer);
            }
        }
        void IMatchmakingCallbacks.OnJoinRandomFailed(short returnCode, string message) { }
        void IInRoomCallbacks.OnRoomPropertiesUpdate(PhotonHashtable propertiesThatChanged) { }
        void IInRoomCallbacks.OnPlayerPropertiesUpdate(Player targetPlayer, PhotonHashtable changedProps) { }
        void IConnectionCallbacks.OnConnected() { }
        void IInRoomCallbacks.OnMasterClientSwitched(Player newMasterClient) { }
        void IConnectionCallbacks.OnCustomAuthenticationResponse(Dictionary<string, object> data) { }
        void IConnectionCallbacks.OnCustomAuthenticationFailed(string debugMessage) { }
        void IMatchmakingCallbacks.OnFriendListUpdate(List<FriendInfo> friendList) { }
        void IMatchmakingCallbacks.OnCreatedRoom() { }
        void IConnectionCallbacks.OnRegionListReceived(RegionHandler regionHandler) { }
    }
}
