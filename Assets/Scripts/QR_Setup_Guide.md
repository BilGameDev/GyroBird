# QR Code Scanner & Generator Setup

This system allows easy connection between PC and mobile devices using QR codes.

## Features

- **QR Scanner**: Scan QR codes with mobile camera to connect to PC
- **QR Generator**: Generate connection QR codes on PC for mobile devices
- **Multiple Formats**: Support for simple IP:PORT, JSON, and URI formats
- **Auto-Connection**: Automatically configure network components from QR data

## Dependencies Required

### For Mobile (Scanner):
```
ZXing.Net (ZXing-CSharp) - Unity Package Manager or Asset Store
```

### For PC (Generator):
```
ZXing.Net (ZXing-CSharp) - Unity Package Manager or Asset Store
```

## Setup Instructions

### 1. Install ZXing Package
Add to your `manifest.json` or install via Package Manager:
```json
{
  "dependencies": {
    "com.google.external-dependency-manager": "1.2.17",
    // Add ZXing package here
  }
}
```

### 2. PC Setup (QRGenerator)
1. Add `QRGenerator` component to a GameObject
2. Create UI elements:
   - `RawImage` for QR display
   - `Button` for generate/refresh
   - `Text` for IP address display
   - `InputField` for port/name (optional)
3. Wire components in Inspector
4. Set default port to match your `GyroUIReceiver.listenPort`

### 3. Mobile Setup (QRScanner)
1. Add `QRScanner` component to a GameObject
2. Create UI elements:
   - `RawImage` for camera feed
   - `Button` for start/stop scanning
   - `Text` for status/results
   - `Panel` for scanner UI
3. Wire components in Inspector
4. Assign your `GyroUdpSender` for auto-connection
5. Add camera permissions to manifest (Android) or plist (iOS)

### 4. Integration
```csharp
// In QRScanner Inspector:
// - Assign GyroUdpSender component
// - Enable "Auto Apply Connection"
// - Enable "Close Scanner On Success"

// QR codes will automatically configure:
// - GyroUdpSender.serverIp
// - GyroUdpSender.serverPort
// - Start sending gyro data
```

## Usage Workflow

1. **PC**: Start game, open QR generator
2. **PC**: Click "Generate QR" to create connection code
3. **Mobile**: Open QR scanner in your app
4. **Mobile**: Point camera at PC screen to scan QR
5. **Auto-Connect**: Mobile automatically connects to PC
6. **Ready**: Gyro data flows, shooting works via network

## QR Formats Supported

### Simple Format
```
192.168.1.100:7777
```

### JSON Format
```json
{
  "ip": "192.168.1.100",
  "port": 7777,
  "name": "GyroGame"
}
```

### URI Format
```
gyro://192.168.1.100:7777
```

## Platform Notes

- **Editor**: QR generation works, scanning shows placeholder
- **Mobile**: Full scanning functionality with camera
- **PC**: Full QR generation, no scanning needed
- **Permissions**: Camera access required on mobile

## Troubleshooting

1. **"No camera found"**: Check camera permissions
2. **"ZXing not found"**: Install ZXing package
3. **"Can't connect"**: Verify IP/port and firewall settings
4. **"QR won't scan"**: Ensure good lighting and steady camera

## Integration with Existing Components

The QR system works with your existing network setup:
- `GyroUdpSender` - Configured automatically from QR data
- `GyroUIReceiver` - Receives gyro data on configured port
- `MouseShooter` - Handles gyro shooting via network commands

No changes needed to existing network components!