# PcVolumeControlWindows
This is the Windows component of the PcVolumeControl project. It is the server that handles communication with the clients as well as modification of the PC's actualy volume settings.

# Client/Server Protocol
The protocol for interacting with the Windows server is as follows:

The client should open a raw TCP connection that will be used to exchange JSON strings.

When a client first connects it will receive an initial update on the state of the PC's audio systems.

_Example of the full state update sent by the server:_
```json
{
    "version": 4,
    "deviceIds": {
        "0f4090a9-dee2-4563-ba29-0ad6b93d9e22": "Speakers (Realtek High Definition Audio)",
        "c5a32106-264d-40b2-a2e0-74eda397454c": "Headphones (Rift Audio)",
        "c98e4030-926b-4e62-8c83-1c529574cc51": "Headset (5- USB Audio Device)"
    },
    "defaultDevice": {
        "deviceId": "0f4090a9-dee2-4563-ba29-0ad6b93d9e22",
        "name": "Speakers (Realtek High Definition Audio)",
        "masterVolume": 80.0,
        "masterMuted": false,
        "sessions": [
            {
                "name": "OVRServer_x64",
                "id": "{0.0.0.00000000}.{0f4090a9-dee2-4563-ba29-0ad6b93d9e22}|\\Device\\HarddiskVolume2\\Program Files\\Oculus\\Support\\oculus-runtime\\OVRServer_x64.exe%b{00000000-0000-0000-0000-000000000000}",
                "volume": 77.0,
                "muted": false
            },
            {
                "name": "Steam Client Bootstrapper",
                "id": "{0.0.0.00000000}.{0f4090a9-dee2-4563-ba29-0ad6b93d9e22}|\\Device\\HarddiskVolume2\\Program Files (x86)\\Steam\\Steam.exe%b{00000000-0000-0000-0000-000000000000}",
                "volume": 83.0,
                "muted": false
            },
            {
                "name": "qemu-system-i386",
                "id": "{0.0.0.00000000}.{0f4090a9-dee2-4563-ba29-0ad6b93d9e22}|\\Device\\HarddiskVolume2\\Users\\adamw\\AppData\\Local\\Android\\sdk\\emulator\\qemu\\windows-x86_64\\qemu-system-i386.exe%b{00000000-0000-0000-0000-000000000000}",
                "volume": 100.0,
                "muted": false
            },
            {
                "name": "Google Chrome",
                "id": "{0.0.0.00000000}.{0f4090a9-dee2-4563-ba29-0ad6b93d9e22}|\\Device\\HarddiskVolume2\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe%b{00000000-0000-0000-0000-000000000000}",
                "volume": 35.0,
                "muted": false
            }
        ]
    }
}
```

The client will continue getting these full state updates until it disconnects.

The client can perform several actions while connected:
1. Change the default audio device
1. Change the master volume or mute state of the current default device
1. Change the volume or mute state of a given audio session on the current default device

# Client to Server Messages
These are all partial versions of the full state updates that the server sends.

All messages must contain the protocol version. If the protocol version mismatches the server's version, the client will be disconnected.

If any message sent to the server is malformed, the client will be disconnected.

If the client receives an update from the server with a mismatching version, it should disconnect.

## Change the default audio device
All that is required is sending the `defaultDevice` block with the new deviceId:
```json
{
    "defaultDevice": {
        "deviceId": "0f4090a9-dee2-4563-ba29-0ad6b93d9e22"
    },
    "version": 5
}
```

## Change the volume or mute state of the current default device
Identify the current default device with it's device ID, and send the new volume and mute values you would like it to reflect.

You can send one or both volume or mute.
```json
{
    "defaultDevice": {
        "deviceId": "c98e4030-926b-4e62-8c83-1c529574cc51",
        "masterMuted": true,
        "masterVolume": 95.0
    },
    "version": 5
}
```

## Change the volume or mute state of a given audio session on the current default device
_Note: Both Volume AND Mute MUST be provided in session updates!_
```json
{
    "defaultDevice": {
        "deviceId": "c98e4030-926b-4e62-8c83-1c529574cc51",
        "sessions": [
            {
                "id": "{0.0.0.00000000}.{c98e4030-926b-4e62-8c83-1c529574cc51}|\\Device\\HarddiskVolume2\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe%b{00000000-0000-0000-0000-000000000000}",
                "muted": true,
                "volume": 29.0
            }
        ]
    },
    "version": 5
}
```
