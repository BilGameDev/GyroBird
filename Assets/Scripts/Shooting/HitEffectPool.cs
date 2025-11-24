using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pooled animated sprite effect. Plays through frames then returns to pool.
/// Open/Closed: can swap animation set via inspector without changing logic.
/// </summary>
public class HitEffectPool : MonoBehaviour, IHitEffectFactory
{
    [Header("Animation")]
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float frameRate = 20f;
    [SerializeField] private Vector2 randomScaleRange = new Vector2(0.8f, 1.2f);
    [SerializeField] private bool orientToCamera = true;

    [Header("Pooling")]
    [SerializeField] private int initialPoolSize = 8;
    [SerializeField] private bool expandPool = true;

    private readonly List<HitEffectInstance> pool = new List<HitEffectInstance>();
    private Camera cam;

    void Awake()
    {
        cam = Camera.main;
        for (int i = 0; i < initialPoolSize; i++)
            pool.Add(CreateInstance());
    }

    public void SpawnHitEffect(Vector2 position)
    {
        if (frames == null || frames.Length == 0) return;
        var inst = GetFreeInstance();
        inst.Play(position, frames, frameRate, randomScaleRange, orientToCamera ? cam : null);
    }

    private HitEffectInstance GetFreeInstance()
    {
        for (int i = 0; i < pool.Count; i++)
        {
            if (!pool[i].InUse) return pool[i];
        }
        if (expandPool)
        {
            var created = CreateInstance();
            pool.Add(created);
            return created;
        }
        return pool[0]; // fallback reuse
    }

    private HitEffectInstance CreateInstance()
    {
        var go = new GameObject("HitEffectInstance");
        go.transform.SetParent(transform);
        var inst = go.AddComponent<HitEffectInstance>();
        return inst;
    }
}

/// <summary>
/// Internal component handling animation playback.
/// </summary>
internal class HitEffectInstance : MonoBehaviour
{
    private SpriteRenderer sr;
    private float frameTimer;
    private int frameIndex;
    private Sprite[] activeFrames;
    private float activeFrameRate;
    private Camera orientCam;
    public bool InUse { get; private set; }

    void Awake()
    {
        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 1000; // render on top
    }

    public void Play(Vector2 pos, Sprite[] frames, float frameRate, Vector2 scaleRange, Camera cam)
    {
        activeFrames = frames;
        activeFrameRate = frameRate;
        orientCam = cam;
        frameIndex = 0;
        frameTimer = 0f;
        InUse = true;
        transform.position = pos;
        float s = Random.Range(scaleRange.x, scaleRange.y);
        transform.localScale = Vector3.one * s;
        sr.sprite = activeFrames[0];
        sr.enabled = true;
    }

    void Update()
    {
        if (!InUse) return;

        if (orientCam)
            transform.rotation = orientCam.transform.rotation;

        frameTimer += Time.deltaTime;
        float frameDuration = 1f / activeFrameRate;
        if (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            frameIndex++;
            if (frameIndex >= activeFrames.Length)
            {
                // end
                InUse = false;
                sr.enabled = false;
                return;
            }
            sr.sprite = activeFrames[frameIndex];
        }
    }
}
