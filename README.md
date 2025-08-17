## Overview

[Photon Realtime](https://www.photonengine.com/realtime) transport for netick, allows you to host game using relay with multi-region

### Features

| Feature            | Status        |
|--------------------|---------------|
| Relay Room Code    | Beta          |
| Send User Data     | Beta          |
| Session List (matchmaking)       | Not available |
| Connection Request | Not available |

## Installation

### Prerequisites

Unity Editor version 2021 or later.

Install Netick 2 before installing this package.
https://github.com/NetickNetworking/NetickForUnity

### Dependencies
1. [Photon SDK Realtime Unity](https://https://www.photonengine.com/sdks#realtime-unity)

### Steps

- Open the Unity Package Manager by navigating to Window > Package Manager along the top bar.
- Click the plus icon.
- Select Add package from git URL
- Enter https://github.com/StinkySteak/NetickRealtimeTransport
- You can then create an instance by double clicking in the Assets folder and going to `Create > Netick > Transport > Realtime > TransportProvider`

## Accessing Join code
You can attach this script to the NetworkSandbox and let view component access the join code to there.
```cs
public class SandboxRealtime : NetickBehaviour
{
    private RealtimeTransport _transport;

    public override void NetworkStart()
    {
        _transport = Sandbox.Transport as RealtimeTransport;
        _transport.OnRoomCodeUpdated += DebugRoomCode;
            
        // More callbacks available
        //_transport.OnDisconnectedFromRealtime += ...;
        //_transport.OnRoomCreateFailed += ...;

        DebugRoomCode();
    }

    private void DebugRoomCode()
    {
        if (_transport.TryGetRoomCode(out string roomCode))
        {
            Sandbox.Log($"Joinable RoomCode: {roomCode}");
        }
    }
}

```