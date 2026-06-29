Architecture Overview (Shooting / QR Connection)
=================================================

Goals:
- Apply SOLID: each class has one clear reason to change.
- Use interfaces (IShootable, IHitEffectFactory) for decoupling.
- Favor composition (BirdTarget wraps BirdController for hit logic).
- Keep systems open for extension: new shooters (touch, gyro) can implement same flow.

Components:
1. IShootable: Target contract (IsAlive, OnShot).
2. BirdTarget: Adapter; translates OnShot into effect + lifecycle.
3. IHitEffectFactory: Abstracts effect creation/pooling.
4. HitEffectPool: Concrete pooled animated effect implementation.
5. ControllerPacket: Versioned phone-to-TV packet parsing/writing with sequence IDs.
6. VirtualPointerAimModel: Converts calibrated phone attitude into normalized aim and screen position.
7. GyroUIReceiver: UDP receiver and virtual pointer owner; exposes pointer, shoot, calibrate, and restart events.
8. GyroUdpSender: Phone-side New Input System attitude sender plus repeated command packets.
9. MouseShooter: Input-to-hit orchestration, aim assist, and shot feedback.
10. QrConnectionManager: Parses QR payloads and configures GyroUdpSender.

Design Patterns:
- Adapter (BirdTarget around BirdController).
- Factory (IHitEffectFactory producing pooled effects).
- Pooling (object reuse to avoid allocation spikes).
- Dependency Inversion (MouseShooter depends on IShootable interface, not concrete bird class).

Extending:
- Add TouchShooter: replicate MouseShooter with touch input.
- Add different hit effect: implement IHitEffectFactory, assign in BirdTarget.
- Add score system: subscribe to an event fired in BirdTarget.OnShot.

Usage Steps:
1. Attach BirdController + BirdTarget to bird prefab, assign HitEffectPool instance to BirdTarget.
2. Place MouseShooter in scene; set targetLayers to bird layer.
3. Assign InputSystem_Actions/Player/Attack to MouseShooter.shootAction and UI/Point to pointAction when possible.
4. Assign GyroUIReceiver to MouseShooter.pointerReceiver when possible. If left empty, MouseShooter finds it at runtime.
5. Add QrConnectionManager and link GyroUdpSender; call OnQrScanned from your QR scan completion.

Input Feel:
- MouseShooter uses the New Input System. If action references are not assigned, it falls back to Mouse.current, Touchscreen.current, Space, and Enter.
- GyroUdpSender uses AttitudeSensor instead of the legacy Input.gyro API.
- GyroUIReceiver treats the phone as a virtual pointer, not a raw gyro cursor.
- GyroUdpSender includes sequence/time in aim packets and repeats command packets three times. GyroUIReceiver dedupes command sequence IDs.
- GyroUIReceiver maps vertical aim from the phone screen normal and horizontal aim from the phone nose/top direction, so twist/roll is ignored for aiming.
- MouseShooter applies conservative aim assist only in Gyro mode and only when pointer confidence is healthy.
- Tune GyroUIReceiver smoothPos, sensitivity, deadZone, maxTiltAngle, curvePower, and latencyCompensation first.
- Tune MouseShooter assistRadiusPixels and assistStrength second. Keep assist subtle enough that the player feels accurate, not corrected.

Safety:
- BirdTarget auto-adds CircleCollider2D if missing.
- HitEffectPool gracefully handles empty frame lists.
