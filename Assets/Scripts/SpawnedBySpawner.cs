using UnityEngine;

public class SpawnedBySpawner : MonoBehaviour
{
    public ReinforcementSpawner owner;

    void OnDestroy()
    {
        if (owner != null)
            owner.NotifySpawnedDestroyed(gameObject);
    }
}
