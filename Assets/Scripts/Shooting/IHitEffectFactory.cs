using UnityEngine;

/// <summary>
/// Factory responsible for producing (ideally pooled) hit effect instances.
/// </summary>
public interface IHitEffectFactory
{
    /// <summary>Create or fetch a hit effect and play it at position.</summary>
    void SpawnHitEffect(Vector2 position);
}
