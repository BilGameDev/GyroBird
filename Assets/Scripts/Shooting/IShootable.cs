using UnityEngine;

/// <summary>
/// Abstraction for anything that can be shot. Keeps Bird logic decoupled from shooting system.
/// </summary>
public interface IShootable
{
    /// <summary>True while the target can receive shots.</summary>
    bool IsAlive { get; }

    /// <summary>Called by a shooter when a hit is registered (world point of impact).</summary>
    void OnShot(Vector2 hitPoint);
}
