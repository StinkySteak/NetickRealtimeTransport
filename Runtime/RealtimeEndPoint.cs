using Netick;

namespace Netick.Transport
{
    public class RealtimeEndPoint : IEndPoint
    {
        private string _ipAddress;
        private int _port;

        public void SetIPAddress(string ipAddress)
            => _ipAddress = ipAddress;
        public void SetPort(int port)
            => _port = port;

        public string IPAddress => _ipAddress;

        public int Port => _port;

        public override string ToString()
        {
            return string.Format("{0}:{1}", _ipAddress, _port);
        }
    }
}
