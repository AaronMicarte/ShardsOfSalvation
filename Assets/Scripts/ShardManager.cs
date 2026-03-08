using System;
using UnityEngine;

public class ShardManager : MonoBehaviour
{
    public static ShardManager Instance { get; private set; }

    public event Action<int> OnShardCountChanged;
    public event Action OnScepterCrafted;

    int shardCount;

    public int ShardCount => shardCount;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void AddShard(int amount = 1)
    {
        shardCount += amount;
        OnShardCountChanged?.Invoke(shardCount);
    }

    public void ResetShards()
    {
        shardCount = 0;
        OnShardCountChanged?.Invoke(shardCount);
    }

    public void CraftScepter()
    {
        if (shardCount <= 0) return;
        OnScepterCrafted?.Invoke();
        ResetShards();
    }
}