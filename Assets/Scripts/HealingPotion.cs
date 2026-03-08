using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class HealingPotion : MonoBehaviour
{
    [Header("Healing")]
    [Tooltip("How much HP the potion restores on pickup")]
    public int healAmount = 4;

    [Header("Feedback")]
    public GameObject pickupVFX;
    public AudioClip pickupSfx;
    [Range(0f, 1f)] public float pickupVolume = 1f;

    [Header("Pickup")]
    [Tooltip("Tag used to identify the player. Use 'Player' by default.")]
    public string playerTag = "Player";

    private void Reset()
    {
        // Ensure the collider is a trigger by default for easy setup
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;
        if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag)) return;

        // Support PlayerHealth on the collided object or any parent (robust for child colliders)
        var ph = other.GetComponent<PlayerHealth>() ?? other.GetComponentInParent<PlayerHealth>();
        if (ph == null) return;

        ph.Heal(healAmount);

        // Spawn feedback
        if (pickupVFX != null)
        {
            Instantiate(pickupVFX, transform.position, Quaternion.identity);
        }
        if (pickupSfx != null)
        {
            AudioSource.PlayClipAtPoint(pickupSfx, transform.position, pickupVolume);
        }

        Destroy(gameObject);
    }
}