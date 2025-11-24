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
5. MouseShooter: Input-to-hit orchestration; no knowledge of birds.
6. QrConnectionManager: Parses QR payloads and configures GyroUdpSender.

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
3. Add QrConnectionManager and link GyroUdpSender; call OnQrScanned from your QR scan completion.

Safety:
- BirdTarget auto-adds CircleCollider2D if missing.
- HitEffectPool gracefully handles empty frame lists.
