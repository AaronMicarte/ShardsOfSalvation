using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ArrowProjectile : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 10f;
    [Tooltip("Maximum distance the projectile will travel before being destroyed")]
    public float maxDistance = 8f;

    [Header("Damage")]
    public int damage = 1;

    [Header("Owner/Filtering")]
    [Tooltip("Tag of the shooter so the projectile won't hit its owner (e.g., 'Player' or 'Enemy')")]
    public string ownerTag = "Player";
    [Tooltip("Layers the projectile can hit (defaults to everything)")]
    public LayerMask hitLayers = ~0;

    [Header("Impact")]
    public GameObject hitVFX;
    public AudioClip hitSfx;
    [Range(0f, 1f)] public float hitVolume = 1f;

    [Header("Options")]
    [Tooltip("If >0, projectile can pass through this many targets before being destroyed (pierce count)")]
    public int pierceCount = 0; // 0 = no pierce
    [Tooltip("Whether to rotate the arrow to face its flight direction")]
    public bool alignRotationToFlight = true;

    // runtime
    private Vector2 origin;
    private Vector2 direction = Vector2.right; // current direction (may be defended by initialDirection)
    private Vector2 initialDirection = Vector2.right; // direction fixed at spawn
    private float traveled = 0f;
    private bool fired = false;

    // Pooling support
    [HideInInspector] public bool pooled = false;
    [HideInInspector] public ArrowProjectilePool pool = null;

    void Start()
    {
        // Detach if parented unexpectedly so the projectile doesn't follow other transforms
        transform.SetParent(null);

        origin = transform.position;
        // If Fire() wasn't called explicitly, assume forward (transform.right)
        if (!fired)
        {
            initialDirection = transform.right;
            direction = initialDirection;
            fired = true;
        }

        // Ensure collider is trigger (recommended for projectiles)
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

        Debug.Log($"ArrowProjectile '{name}' Start: ownerTag={ownerTag}, speed={speed}, maxDistance={maxDistance}, origin={origin}, direction={direction}");
    }

    void OnEnable()
    {
        // Clear runtime state when re-used from a pool
        traveled = 0f;
        fired = false;
    }

    /// <summary>
    /// Reset and initialize this projectile when it is taken from a pool.
    /// </summary>
    public void ResetForPool(Vector2 dir, Vector3 spawnPos, string ownerTag)
    {
        transform.position = spawnPos;
        origin = spawnPos;
        initialDirection = dir.normalized;
        direction = initialDirection;
        traveled = 0f;
        fired = true;
        this.ownerTag = ownerTag;
        transform.SetParent(null);
    }

    void Update()
    {
        if (speed > 0f && fired)
        {
            // Protect against external changes: always use the initialDirection captured at spawn
            if (direction != initialDirection)
            {
                Debug.LogWarning($"Arrow '{name}' direction was modified at runtime. Forcing back to initial direction {initialDirection}.");
                direction = initialDirection;
            }

            Vector2 delta = initialDirection * speed * Time.deltaTime;
            transform.Translate(delta, Space.World);
            traveled += delta.magnitude;

            if (alignRotationToFlight && initialDirection.sqrMagnitude > 0.0001f)
            {
                float angle = Mathf.Atan2(initialDirection.y, initialDirection.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }

            if (traveled >= maxDistance)
            {
                DestroyProjectile();
            }
        }
    }

    /// <summary>
    /// Call this to fire the projectile in a given direction. Must be called after instantiating.
    /// </summary>
    public void Fire(Vector2 dir)
    {
        if (dir.sqrMagnitude <= 0.0001f) return;
        initialDirection = dir.normalized;
        direction = initialDirection;
        fired = true;
        // Ensure this projectile is not parented to any moving object
        transform.SetParent(null);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;
        if (!other.gameObject.activeInHierarchy) return;

        // Ignore owner
        if (!string.IsNullOrEmpty(ownerTag) && other.CompareTag(ownerTag)) { Debug.Log($"Arrow '{name}' ignored collision with owner '{other.name}' (tag={ownerTag})."); return; }

        // Respect hit layers
        if (((1 << other.gameObject.layer) & hitLayers) == 0) { Debug.Log($"Arrow '{name}' ignored collision with '{other.name}' due to layer mask."); return; }

        Debug.Log($"Arrow '{name}' collided with '{other.name}' (layer={other.gameObject.layer}).");

        // Try damageable types
        var ph = other.GetComponent<PlayerHealth>() ?? other.GetComponentInParent<PlayerHealth>();
        if (ph != null)
        {
            ph.TakeDamage(damage);
            OnImpact();
            return;
        }

        var enemy = other.GetComponent<Enemy>() ?? other.GetComponentInParent<Enemy>();
        if (enemy != null)
        {
            enemy.TakeDamage(damage);
            OnImpact();
            return;
        }

        // If we hit something else (wall, tile, etc.), just impact
        OnImpact();
    }

    private void OnImpact()
    {
        if (hitVFX != null) Instantiate(hitVFX, transform.position, Quaternion.identity);
        if (hitSfx != null) AudioSource.PlayClipAtPoint(hitSfx, transform.position, hitVolume);

        if (pierceCount > 0)
        {
            pierceCount--;
            return;
        }

        DestroyProjectile();
    }

    private void DestroyProjectile()
    {
        // If pooled, return to pool for reuse instead of destroying
        if (pooled && pool != null)
        {
            pool.Return(this);
            return;
        }

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 dir = (Application.isPlaying ? (Vector3)direction : transform.right);
        Gizmos.DrawLine(transform.position, transform.position + dir.normalized * maxDistance);
    }
}