using Photon.Realtime;
using System;

namespace Netick.Transport
{
    public class RealtimePeer
    {
        public Player RealtimePlayer;
        public RealtimeEndPoint EndPoint;
        private RealtimeNetManager _netManager;

        internal RealtimePeer(RealtimeNetManager netManager)
        {
            _netManager = netManager;
        }

        public void SetEndPoint(string ipAddress, int port)
        {
            EndPoint = new RealtimeEndPoint();
            EndPoint.SetIPAddress(ipAddress);
            EndPoint.SetPort(port);
        }

        public void Send(IntPtr ptr, int length, bool isReliable)
        {
            _netManager.Send(this, ptr, length, isReliable);
        }
    }
}
