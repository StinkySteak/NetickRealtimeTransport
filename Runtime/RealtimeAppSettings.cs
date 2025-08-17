using Photon.Realtime;
using UnityEngine;

namespace Netick.Transport
{
    [CreateAssetMenu(fileName = nameof(RealtimeAppSettings), menuName = "Netick/Transport/Realtime/AppSettings")]
    public class RealtimeAppSettings : ScriptableObject
    {
        public AppSettings Settings;
    }
}
