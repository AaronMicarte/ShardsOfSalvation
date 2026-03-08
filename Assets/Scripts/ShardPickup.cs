using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ShardPickup : MonoBehaviour
{
    [Tooltip("Optional particle or visual feedback")]
    [SerializeField] private GameObject pickupVFX;
    [SerializeField, Tooltip("Optional sound to play when shard is collected")] private AudioClip pickupClip;
    [SerializeField, Tooltip("Volume for pickup sound (0-1)"), Range(0f, 1f)] private float pickupVolume = 1f;

    void Awake()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        // Ensure ShardManager exists before adding (silently ignore if missing)
        if (ShardManager.Instance == null)
            return;

        ShardManager.Instance.AddShard();

        if (pickupVFX != null)
            Instantiate(pickupVFX, transform.position, Quaternion.identity);

        if (pickupClip != null)
            AudioSource.PlayClipAtPoint(pickupClip, transform.position, pickupVolume);

        Destroy(gameObject);
    }
}