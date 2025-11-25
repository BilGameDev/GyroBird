# 🎯 GyroGame - Wireless Gyroscope Laser Pointer

A Unity multiplayer application that turns your smartphone into a gyroscope-controlled laser pointer, moving a sphere on your PC screen in real-time.

![Unity](https://img.shields.io/badge/Unity-2022+-black?logo=unity)
![C#](https://img.shields.io/badge/C%23-11.0-blue?logo=csharp)
![Platform](https://img.shields.io/badge/Platform-Android%20%7C%20Windows-green)

## 🎮 Features

- **📱 Gyroscope Control**: Use your phone's gyroscope as a wireless motion controller
- **🔗 UDP Networking**: Low-latency communication using UDP protocol
- **🎯 Laser Pointer Mode**: Move the sphere precisely by tilting your phone
- **📡 Unity Relay Integration**: Connect across different networks using Unity Relay with join codes
- **🔄 Real-time Calibration**: Recenter the pointer on-the-fly
- **📊 Smooth Movement**: Advanced interpolation with dead zones and sensitivity controls

## 🏗️ Architecture

### Client (Android Phone)
- **GyroUdpSender**: Reads gyroscope data and sends orientation updates via UDP
- **NetworkDiscovery**: Discovers servers on local network or connects via relay join code
- Sends data at 200Hz for precise tracking

### Server (PC)
- **GyroPointerReceiver**: Receives gyroscope data and moves the sphere
- **LaserSphereBinder**: Binds the sphere to the player's controller
- **RelayManager**: Manages Unity Relay connections with join codes
- Applies smoothing, dead zones, and screen-bound constraints

## 🚀 Getting Started

### Prerequisites
- Unity 2022.3 or higher
- Android device with gyroscope support
- Unity Multiplayer Services (for relay mode)

### Installation

1. Clone the repository:
```bash
git clone https://github.com/BilGameDev/GyroGame.git
cd GyroGame
```

2. Open the project in Unity

3. Set up Unity Services:
   - Go to **Edit > Project Settings > Services**
   - Link your Unity Cloud Project
   - Enable Unity Relay

### Building

#### PC Build (Server)
1. **File > Build Settings**
2. Select **Windows, Mac & Linux Standalone**
3. Click **Build**

#### Android Build (Client)
1. **File > Build Settings**
2. Select **Android**
3. Ensure these permissions are enabled:
   - Internet Access
   - Gyroscope
4. Click **Build**

## 📱 How to Use

### Using Relay (Recommended - Works Across Networks)

**On PC:**
1. Launch the PC build
2. Click **"Start Server"**
3. Share the displayed **6-character join code**

**On Phone:**
1. Launch the Android app
2. Enter the **join code**
3. Click **"Connect"**
4. Point your phone like a laser pointer!

### Using Local Network (UDP)

**On PC:**
1. Launch and note your local IP address
2. Start the UDP server

**On Phone:**
1. Enter the PC's IP address
2. Connect and start controlling!

## 🎮 Controls

| Action | Input |
|--------|-------|
| Move Up/Down | Tilt phone up/down (pitch) |
| Move Left/Right | Turn phone left/right (yaw) |
| Calibrate | Press **"Calibrate"** button |

## ⚙️ Configuration

### Sensitivity Settings (GyroPointerReceiver)
```csharp
public float sensitivity = 2f;        // Movement multiplier
public float maxTiltAngle = 25f;      // Degrees to reach screen edge
public float smoothPos = 20f;         // Movement smoothing (higher = faster)
public float deadZone = 1f;           // Ignore movements below this angle
public float horizontalRange = 0.9f;  // % of screen width used
public float verticalRange = 0.9f;    // % of screen height used
```

### Gyro Update Rate (GyroUdpSender)
```csharp
Input.gyro.updateInterval = 0.005f; // 200Hz updates
```

## 🛠️ Technical Details

- **Networking**: UDP for low-latency (local) + Unity Relay (internet)
- **Data Format**: Quaternion (16 bytes) transmitted per frame
- **Coordinate Conversion**: Handles Android → Unity coordinate space transformation
- **Phone Orientation**: 90° rotation applied so screen faces forward
- **Movement Algorithm**: Euler angle extraction → sensitivity multiplier → screen bounds clamping → lerp smoothing

## 📁 Project Structure

```
Assets/
├── Scripts/
│   ├── GyroUdpSender.cs          # Phone gyro sender
│   ├── GyroPointerReceiver.cs    # PC sphere controller
│   ├── RelayManager.cs            # Unity Relay integration
│   ├── SimpleNetworkStarter.cs   # Server/Client starter
│   ├── LaserSphereBinder.cs      # Binds sphere to player
│   ├── GyroLaserController.cs    # Netcode gyro controller
│   └── GyroUIHelper.cs           # UI helper functions
├── Scenes/
│   └── SampleScene.unity
├── Prefabs/
│   ├── Player.prefab              # Network player object
│   └── Canvas.prefab              # UI canvas
└── Plugins/
    └── Android/
        └── AndroidManifest.xml    # Android permissions
```

## 🔧 Troubleshooting

**Gyro returns zeros:**
- Check AndroidManifest.xml includes SENSOR permissions
- Ensure device supports gyroscope
- Wait 0.5s after app starts for sensor initialization

**Sphere not moving:**
- Verify LaserSphere is assigned in GyroPointerReceiver Inspector
- Check both PC and phone console logs
- Ensure firewall allows UDP port 7777

**Connection fails:**
- For UDP: Check firewall settings and verify both devices on same network
- For Relay: Verify Unity Services is linked and active in Project Settings

**Movement not responsive:**
- Increase `sensitivity` value (try 2.5 or 3.0)
- Increase `smoothPos` for faster response
- Reduce `deadZone` to 0.5 or lower

## 📊 Performance

- **Latency**: ~10-20ms (UDP local network)
- **Update Rate**: 200Hz sensor polling
- **Network Bandwidth**: ~3.2 KB/s per client
- **Frame Rate**: 60+ FPS on both client and server

## 🎯 Use Cases

- Wireless presentation pointer
- Remote game controller
- VR/AR pointer input system
- Motion-controlled UI navigation
- Educational demonstrations

## 🚀 Future Enhancements

- [ ] Multiple phone support (multiplayer)
- [ ] Click/tap to shoot functionality
- [ ] Haptic feedback on phone
- [ ] Customizable pointer visuals
- [ ] Auto-discovery via mDNS
- [ ] WebRTC for browser support

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🙏 Acknowledgments

- Built with **Unity Netcode for GameObjects**
- Uses **Unity Multiplayer Services**
- Inspired by smartphone VR pointer systems

## 📞 Contact

BilGameDev - [GitHub](https://github.com/BilGameDev)

Project Link: [https://github.com/BilGameDev/GyroGame](https://github.com/BilGameDev/GyroGame)

---

## 💼 Portfolio Summary

*GyroGame demonstrates advanced Unity networking, mobile sensor integration, and real-time multiplayer synchronization. Built a client-server architecture using UDP for local networks and Unity Relay for internet connectivity, transforming Android gyroscope data into precise 2D pointer control with sub-frame latency. Implemented custom coordinate space transformations, smoothing algorithms, and sensitivity controls for optimal user experience.*
