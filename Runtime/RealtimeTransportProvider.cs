using Netick.Unity;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Netick.Transport
{
    [CreateAssetMenu(fileName = nameof(RealtimeTransportProvider), menuName = "Netick/Transport/Realtime/TransportProvider")]
    public class RealtimeTransportProvider : NetworkTransportProvider
    {
        public RealtimeAppSettings RealtimeAppSettings;

        public override NetworkTransport MakeTransportInstance()
        {
            return new RealtimeTransport(this);
        }
    }

    internal class RealtimeConnection : TransportConnection
    {
        public RealtimePeer Peer;

        public override IEndPoint EndPoint => Peer.EndPoint;

        public override int Mtu => 1200;

        public override void Send(IntPtr ptr, int length)
        {
            Peer.Send(ptr, length, isReliable: false);
        }

        public override void SendUserData(IntPtr ptr, int length, TransportDeliveryMethod transportDeliveryMethod)
        {
            bool isReliable = transportDeliveryMethod == TransportDeliveryMethod.Reliable;

            Peer.Send(ptr, length, isReliable);
        }
    }

    public unsafe class RealtimeTransport : NetworkTransport, IRealtimeEventListener
    {
        private RealtimeTransportProvider _provider;
        private RealtimeNetManager _netManager;
        private Dictionary<RealtimePeer, RealtimeConnection> _connections;
        private Queue<RealtimeConnection> _freeConnections;
        private BitBuffer _buffer;
        private byte[] _receiveBuffer;

        public event Action OnRoomCodeUpdated;
        public event Action OnRoomCreateFailed;
        public event Action OnDisconnectedFromRealtime;

        public RealtimeTransport(RealtimeTransportProvider provider)
        {
            _provider = provider;
        }

        public override void Init()
        {
            _netManager = new RealtimeNetManager();
            _netManager.Init(Engine, _provider.RealtimeAppSettings.Settings, this);

            _connections = new(Engine.Config.MaxPlayers);
            _freeConnections = new(Engine.Config.MaxPlayers);

            for (int i = 0; i < Engine.Config.MaxPlayers; i++)
                _freeConnections.Enqueue(new RealtimeConnection());

            _receiveBuffer = new byte[2048];
            _buffer = new BitBuffer(createChunks: false);
        }

        public override void Connect(string address, int port, byte[] connectionData, int connectionDataLength)
        {
            _netManager.Connect(address);
        }

        public override void Disconnect(TransportConnection connection)
        {
            RealtimeConnection c = (RealtimeConnection)connection;
            _netManager.Kick(c.Peer);
        }

        public override void PollEvents()
        {
            _netManager.PollUpdate();
        }

        public override void Run(RunMode mode, int port)
        {
            if (mode == RunMode.Server)
            {
                _netManager.HostRoom();
            }
        }

        public override void Shutdown()
        {
            _netManager.Shutdown();
        }

        void IRealtimeEventListener.OnPeerConnected(RealtimePeer peer)
        {
            RealtimeConnection connection = _freeConnections.Dequeue();
            connection.Peer = peer;

            _connections.Add(peer, connection);

            NetworkPeer.OnConnected(connection);
        }

        void IRealtimeEventListener.OnPeerDisconnected(RealtimePeer peer)
        {
            if (_connections.TryGetValue(peer, out var connection))
            {
                _connections.Remove(peer);
                _freeConnections.Enqueue(connection);

                NetworkPeer.OnDisconnected(connection, TransportDisconnectReason.Timeout);
            }
        }

        void IRealtimeEventListener.OnNetworkReceive(RealtimePeer peer, ArraySegment<byte> bytes)
        {
            if (!_connections.TryGetValue(peer, out var c))
                return;

            Array.Copy(bytes.Array, bytes.Offset, _receiveBuffer, 0, bytes.Count);

            fixed (byte* ptr = _receiveBuffer)
            {
                _buffer.SetFrom(ptr, bytes.Count, bytes.Count);
                NetworkPeer.Receive(c, _buffer);
            }
        }

        public bool TryGetRoomCode(out string roomCode)
           => _netManager.TryGetRoomCode(out roomCode);

        void IRealtimeEventListener.OnConnectedToRoom()
        {
            OnRoomCodeUpdated?.Invoke();
        }

        void IRealtimeEventListener.OnCreateRoomFailed()
        {
            OnRoomCreateFailed?.Invoke();
        }

        void IRealtimeEventListener.OnDisconnectedFromRealtime()
        {
            OnDisconnectedFromRealtime?.Invoke();
        }
    }
}