# 🎯 GyroBird - Mobile Gyroscope Bird Shooting Game

A Unity multiplayer bird shooting game where your smartphone becomes a gyroscope-controlled crosshair to shoot birds on your PC screen in real-time.

![Unity](https://img.shields.io/badge/Unity-2022+-black?logo=unity)
![C#](https://img.shields.io/badge/C%23-11.0-blue?logo=csharp)
![Platform](https://img.shields.io/badge/Platform-Android%20%7C%20Windows-green)

## 🎮 Features

- **📱 Gyroscope Aiming**: Use your phone's gyroscope as a wireless crosshair controller
- **🦅 Bird Shooting**: Aim and shoot flying birds with progressive difficulty
- **🔗 UDP Networking**: Ultra-low latency communication (10-20ms) for responsive shooting
- **📡 QR Code Connection**: Scan QR code to instantly connect phone to PC
- **🎯 Score & Ammo System**: Limited bullets, earn bonus ammo with kill streaks
- **📊 Wave System**: Progressive difficulty with wave breaks
- **🏆 Escape Mechanic**: Don't let too many birds escape or it's game over!
- **💥 Visual Effects**: Pooled hit effects and screen flash feedback

## 🏗️ Architecture

### Client (Android Phone)
- **GyroUdpSender**: Reads gyroscope data and sends orientation + shoot commands via UDP
- **QR Scanner**: Scans QR code containing server IP/port for instant connection
- **PhoneGameUI**: Real-time score, bullets, and escaped birds display
- Sends data at 200Hz for precise aiming

### Server (PC)
- **GyroUIReceiver**: Receives gyroscope data and moves crosshair on screen
- **MouseShooter**: Handles shooting logic with both mouse and gyro input modes
- **BirdSpawner**: Progressive difficulty spawner with wave system
- **GameManager**: Static game state manager with event-driven architecture
- **BirdPool**: Object pooling system for performance optimization
- **HitEffectPool**: Pooled animated hit effects

## 🚀 Getting Started

### Prerequisites
- Unity 2022.3 or higher
- Android device with gyroscope support
- PC and phone on the same local network (for UDP mode)

### Installation

1. Clone the repository:
```bash
git clone https://github.com/BilGameDev/GyroGame.git
cd GyroGame
```

2. Open the project in Unity

3. Configure game settings in **GameManagerConfig** component:
   - Starting bullets
   - Max escaped birds
   - Kill streak bonus system

### Building

#### PC Build (Server)
1. **File > Build Settings**
2. Select **Windows, Mac & Linux Standalone**
3. Click **Build**

#### Android Build (Client)
1. **File > Build Settings**
2. Select **Android**
3. Ensure these permissions are enabled in **AndroidManifest.xml**:
   - Internet Access
   - Camera (for QR scanning)
   - Gyroscope
4. Click **Build**

## 📱 How to Use

### Using QR Code Connection (Recommended)

**On PC:**
1. Launch the PC build
2. A **QR code** will be displayed with connection info
3. Make sure firewall allows UDP port 7777

**On Phone:**
1. Launch the Android app
2. Tap **"Scan QR"**
3. Point camera at the QR code on PC screen
4. Connection established automatically!
5. Aim with phone and tap **"Shoot"** button to fire

### Manual IP Connection

**On PC:**
1. Launch and note your local IP address
2. Start the game

**On Phone:**
1. Enter the PC's IP address manually
2. Enter port (default: 7777)
3. Tap **"Connect"**

## 🎮 Controls

| Action | Input |
|--------|-------|
| Aim Up/Down | Tilt phone up/down (pitch) |
| Aim Left/Right | Turn phone left/right (yaw) |
| Shoot | Tap **"Shoot"** button on phone |
| Calibrate | Press **"Calibrate"** to recenter |
| Restart Game | Press **"Restart"** after game over |

## 🎯 Gameplay Mechanics

### Scoring System
- Each bird shot grants **100 points**
- Kill streak system: Every **2 consecutive kills** grants **+1 bonus bullet**
- Limited ammo creates strategic gameplay

### Win/Lose Conditions
- **Game Over** if bullets run out
- **Game Over** if **10+ birds escape**
- Survive as long as possible for high scores!

### Wave System
- Waves last **30 seconds** with **5-second breaks**
- Bird spawn rate increases each wave
- Bird speed increases progressively
- Maximum **8 simultaneous birds** on screen

## ⚙️ Configuration

### Gyro Sensitivity (GyroUIReceiver)
```csharp
public float sensitivity = 2f;        // Movement multiplier
public float maxTiltAngle = 25f;      // Degrees to reach screen edge
public float smoothPos = 20f;         // Crosshair smoothing
public float deadZone = 1f;           // Ignore small movements
public float horizontalRange = 0.9f;  // 90% of screen width
public float verticalRange = 0.9f;    // 90% of screen height
```

### Game Balance (GameManagerConfig)
```csharp
startingBullets = 10;           // Initial ammo
killsForBonus = 2;              // Kills needed for bonus bullet
bonusBullets = 1;               // Bonus ammo awarded
maxEscapedBirds = 10;          // Max birds allowed to escape
```

### Difficulty (BirdSpawner)
```csharp
startSpawnInterval = 3f;        // Initial spawn delay
minSpawnInterval = 0.5f;        // Fastest spawn rate
speedRange = (2f, 4f);          // Bird speed range
maxSimultaneousBirds = 8;      // Max birds on screen
```

## 🛠️ Technical Details

### Architecture Patterns
- **Observer Pattern**: GameManager events for decoupled UI updates
- **Object Pooling**: BirdPool and HitEffectPool for zero garbage collection
- **Adapter Pattern**: BirdTarget wraps BirdController for shooting interface
- **Factory Pattern**: IHitEffectFactory for effect instantiation
- **SOLID Principles**: Single responsibility throughout codebase

### Networking
- **Protocol**: UDP for minimum latency
- **Data Format**: 
  - Gyro: 16 bytes (Quaternion) + 1 byte (message type)
  - Shoot: 1 byte command
- **Update Rate**: 200Hz sensor polling
- **Latency**: ~10-20ms on local network

### Performance
- **Object Pooling**: Zero runtime allocations for birds and effects
- **Optimized Raycasting**: LayerMask filtering + OverlapPoint for 2D
- **Event-Driven**: No Update() polling for UI updates
- **Frame Rate**: 60+ FPS on both client and server

## 📁 Project Structure

```
Assets/
├── Scripts/
│   ├── BirdController.cs           # Bird AI and movement
│   ├── BirdSpawner.cs              # Wave-based spawning system
│   ├── BirdPool.cs                 # Object pooling for birds
│   ├── GameManager.cs              # Static game state manager
│   ├── GameManagerConfig.cs        # Game settings component
│   ├── GameHUD.cs                  # PC HUD display
│   ├── PhoneGameUI.cs              # Phone UI during gameplay
│   ├── GyroUdpSender.cs            # Phone gyro + shoot sender
│   ├── GyroUIReceiver.cs           # PC crosshair receiver
│   ├── QrConnectionManager.cs      # QR code connection handler
│   ├── AppManager.cs               # App state machine
│   └── Shooting/
│       ├── IShootable.cs           # Shootable target interface
│       ├── BirdTarget.cs           # Bird shooting adapter
│       ├── MouseShooter.cs         # Shooting system (mouse/gyro)
│       ├── HitEffectPool.cs        # Pooled hit animations
│       └── IHitEffectFactory.cs    # Effect factory interface
├── Scenes/
│   └── SampleScene.unity
├── Prefabs/
│   ├── Bird.prefab                 # Bird with BirdController + BirdTarget
│   └── Canvas.prefab               # UI elements
└── Plugins/
    └── Android/
        └── AndroidManifest.xml     # Android permissions
```

## 🔧 Troubleshooting

**Connection fails:**
- Verify both devices on same WiFi network
- Check Windows Firewall allows UDP port 7777
- Try manually entering IP instead of QR scan

**Gyro not responding:**
- Ensure AndroidManifest.xml includes sensor permissions
- Tap **"Calibrate"** to recenter crosshair
- Check device has gyroscope support

**Crosshair too slow/fast:**
- Adjust `sensitivity` in GyroUIReceiver (2.0 = default)
- Modify `smoothPos` for responsiveness (higher = faster)
- Reduce `maxTiltAngle` for smaller movement range

**Birds not spawning:**
- Check BirdPool has prefabs assigned
- Verify BirdSpawner has `StartSpawning()` called
- Check camera bounds are configured

**Shooting not working:**
- Verify MouseShooter has correct LayerMask
- Check BirdTarget has IShootable interface
- Ensure GameManager has bullets remaining

## 📊 Performance Metrics

- **Latency**: ~10-20ms (UDP local network)
- **Update Rate**: 200Hz gyro polling
- **Network Bandwidth**: ~3.5 KB/s per client
- **Memory**: Zero allocations during gameplay (pooling)
- **Frame Rate**: 60+ FPS on both platforms
- **Max Concurrent Birds**: 8 (configurable)

## 🎯 Game Design

### Core Loop
1. Birds spawn from screen edges
2. Player aims with phone gyroscope
3. Tap to shoot - hit grants points
4. Miss wastes bullets
5. Let birds escape = closer to game over
6. Earn bonus bullets with kill streaks
7. Survive waves with increasing difficulty

### Difficulty Scaling
- **Spawn Rate**: Decreases from 3s to 0.5s
- **Bird Speed**: Increases 0.2-0.3 units per wave
- **Bird Count**: +2 birds per wave
- **Escape Speed**: Birds flee faster after timer

## 🚀 Future Enhancements

- [ ] Leaderboards and high score system
- [ ] Multiple bird types with different behaviors
- [ ] Power-ups (slow-mo, rapid fire, etc.)
- [ ] Sound effects and background music
- [ ] Particle effects on bird destruction
- [ ] Multiple difficulty modes
- [ ] Achievement system
- [ ] Multiplayer competitive mode
- [ ] WebGL version for browser play

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🙏 Acknowledgments

- Built with **Unity Engine**
- UDP networking for real-time communication
- QR code integration for seamless connection
- Object pooling patterns for optimization

## 📞 Contact

BilGameDev - [GitHub](https://github.com/BilGameDev)

Project Link: [https://github.com/BilGameDev/GyroGame](https://github.com/BilGameDev/GyroGame)

---

## 💼 Portfolio Summary

*GyroBird demonstrates advanced Unity game development with mobile-to-PC networking, gyroscope sensor integration, and optimized gameplay systems. Implemented UDP networking with QR code pairing, object pooling for zero-allocation gameplay, progressive difficulty systems, and SOLID architectural patterns. Features event-driven game state management, 200Hz sensor polling for 10-20ms latency, and a complete wave-based shooting game with scoring and ammo mechanics.*
