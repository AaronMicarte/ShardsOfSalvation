using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CraftingStation : MonoBehaviour
{
    [Tooltip("How many shards required to craft (set 0 to craft any collected shards)")]
    [SerializeField] private int shardsRequired = 0;

    void Awake()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (ShardManager.Instance == null) return;

        if (shardsRequired <= 0 || ShardManager.Instance.ShardCount >= shardsRequired)
        {
            ShardManager.Instance.CraftScepter();
            // TODO: trigger stage restore / visual changes here.
        }
    }
}