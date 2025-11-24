// Minimal main thread runner
using System;
using System.Collections.Generic;
using UnityEngine;

public class UnityMainThreadHelper : MonoBehaviour
{
    static readonly Queue<Action> q = new();
    static UnityMainThreadHelper inst;

    [RuntimeInitializeOnLoadMethod]
    static void Init()
    {
        if (inst != null) return;
        var go = new GameObject("UnityMainThreadHelper");
        DontDestroyOnLoad(go);
        inst = go.AddComponent<UnityMainThreadHelper>();
    }

    public static void Run(Action a)
    {
        lock (q) q.Enqueue(a);
    }

    void Update()
    {
        lock (q)
        {
            while (q.Count > 0) q.Dequeue()?.Invoke();
        }
    }
}
