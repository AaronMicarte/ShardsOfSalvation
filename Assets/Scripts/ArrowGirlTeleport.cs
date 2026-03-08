using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class ArrowGirlTeleport : MonoBehaviour
{
    [Header("Trigger")]
    [Tooltip("If the player is closer than this distance, the enemy will consider teleporting away.")]
    public float triggerDistance = 2.5f;

    [Header("Teleport Distance")]
    [Tooltip("Minimum distance to teleport away")]
    public float minTeleportDistance = 3f;
    [Tooltip("Maximum distance to teleport away")]
    public float maxTeleportDistance = 5f;

    [Header("Behavior")]
    public float cooldown = 3f;
    public int teleportAttempts = 12;
    [Tooltip("If true, play a short telegraph (windup) before teleporting")]
    public bool telegraph = true;
    public float telegraphDuration = 0.22f;

    [Header("Safety Checks")]
    [Tooltip("Layers to treat as blocking when deciding safe teleport positions (Walls, Ground etc.)")]
    public LayerMask obstacleMask = ~0;
    [Tooltip("Layers considered ground (used to snap teleport destination down to ground if possible)")]
    public LayerMask groundMask = Physics2D.DefaultRaycastLayers;
    [Tooltip("How far to raycast down to find ground when evaluating candidate positions")]
    public float groundRaycastDistance = 3f;
    [Tooltip("Radius used to check for overlaps at the candidate spawn point")]
    public float clearanceRadius = 0.28f;

    [Header("Invulnerability / Collision")]
    [Tooltip("If true, temporarily set colliders to trigger to avoid being hit during teleport")]
    public bool invulnerableDuringTeleport = true;
    [Tooltip("Extra time to remain invulnerable after teleport (seconds)")]
    public float postTeleportInvulnerability = 0.12f;

    [Header("Feedback")]
    public GameObject telegraphVFX;
    public GameObject arrivalVFX;
    public AudioClip telegraphSfx;
    public AudioClip teleportSfx;
    [Range(0f, 1f)] public float audioVolume = 1f;
    [Header("Placement")]
    [Tooltip("Vertical offset above ground to place the enemy after teleport (in world units). Helps visually lift them off the tile.")]
    public float teleportElevation = 0.12f;
    [Header("Misc")]
    [Tooltip("Optional explicit player transform to use (if empty will search for object tagged 'Player')")]
    public Transform player;

    [Header("Events")]
    public UnityEvent onTeleportStart;
    public UnityEvent onTeleportEnd;

    // runtime
    private float lastTeleportTime = -Mathf.Infinity;
    private bool isTeleporting = false;
    private Collider2D[] cachedColliders;
    private bool[] originalIsTrigger;
    private Rigidbody2D rb;
    // main collider and bottom offset used to snap to ground properly
    private Collider2D mainCollider;
    private float colliderBottomOffset = 0f;

    void Reset()
    {
        // sensible defaults
        triggerDistance = 2.5f;
        minTeleportDistance = 3f;
        maxTeleportDistance = 5f;
        cooldown = 3f;
        telegraph = true;
        telegraphDuration = 0.22f;
    }

    void OnValidate()
    {
        if (minTeleportDistance < 0f) minTeleportDistance = 0f;
        if (maxTeleportDistance < minTeleportDistance) maxTeleportDistance = minTeleportDistance + 0.1f;
        if (teleportAttempts < 1) teleportAttempts = 1;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        cachedColliders = GetComponentsInChildren<Collider2D>(true);
        originalIsTrigger = new bool[cachedColliders.Length];
        for (int i = 0; i < cachedColliders.Length; i++) originalIsTrigger[i] = cachedColliders[i].isTrigger;

        mainCollider = GetComponent<Collider2D>();
        if (mainCollider != null)
        {
            colliderBottomOffset = mainCollider.bounds.extents.y;
        }

        if (player == null)
        {
            var found = GameObject.FindWithTag("Player");
            if (found != null) player = found.transform;
        }
    }

    void Update()
    {
        if (isTeleporting) return;
        if (Time.time < lastTeleportTime + cooldown) return;
        if (player == null) return;

        float d = Vector2.Distance(transform.position, player.position);
        if (d <= triggerDistance)
        {
            // start teleport attempt
            StartCoroutine(TeleportRoutine());
        }
    }

    /// <summary>
    /// Public method to force a teleport attempt (e.g., from animation event or Enemy hook)
    /// </summary>
    public void TryTeleportNow()
    {
        if (isTeleporting) return;
        if (Time.time < lastTeleportTime + cooldown) return;
        StartCoroutine(TeleportRoutine());
    }

    private IEnumerator TeleportRoutine()
    {
        isTeleporting = true;
        onTeleportStart?.Invoke();

        if (telegraph && telegraphVFX != null)
        {
            Instantiate(telegraphVFX, transform.position, Quaternion.identity);
        }
        if (telegraphSfx != null)
        {
            AudioSource.PlayClipAtPoint(telegraphSfx, transform.position, audioVolume);
        }

        if (telegraph) yield return new WaitForSeconds(telegraphDuration);

        // find safe spot (choose left or right relative to enemy — horizontal only)
        Vector2 origin = transform.position;
        Vector2 chosen = origin;
        bool found = false;

        for (int i = 0; i < teleportAttempts; i++)
        {
            // pick a random side: -1 = left, +1 = right relative to the enemy's facing (transform.right)
            float dist = Random.Range(minTeleportDistance, maxTeleportDistance);
            Vector2 dir = (Random.value < 0.5f) ? -(Vector2)transform.right : (Vector2)transform.right;
            Vector2 candidate = origin + dir * dist;

            // check ground support by raycast down first (we start the ray a bit above the candidate)
            RaycastHit2D groundHit = Physics2D.Raycast(candidate + Vector2.up * 1f, Vector2.down, groundRaycastDistance, groundMask);
            if (!groundHit.collider) continue;

            // Snap the candidate's Y to the ground hit point so the enemy doesn't teleport in mid-air and 'fall'
            float groundY = groundHit.point.y;
            candidate.y = groundY + colliderBottomOffset + teleportElevation;

            // check obstacles overlap at the snapped position
            var hits = Physics2D.OverlapCircleAll(candidate, clearanceRadius, obstacleMask);
            if (hits != null && hits.Length > 0) continue;

            // candidate OK
            chosen = candidate;
            found = true;
            break;
        }

        if (!found)
        {
            // fallback: try cardinal directions
            Vector2[] dirs = new Vector2[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down };
            foreach (var dir in dirs)
            {
                Vector2 candidate = origin + dir * (minTeleportDistance);
                RaycastHit2D groundHit = Physics2D.Raycast(candidate + Vector2.up * 1f, Vector2.down, groundRaycastDistance, groundMask);
                if (!groundHit.collider) continue;

                // snap and apply elevation
                float groundY = groundHit.point.y;
                candidate.y = groundY + colliderBottomOffset + teleportElevation;

                var hits = Physics2D.OverlapCircleAll(candidate, clearanceRadius, obstacleMask);
                if (hits != null && hits.Length > 0) continue;

                chosen = candidate; found = true; break;
            }
        }

        if (!found)
        {
            Debug.Log($"{name}: Teleport aborted — no safe position found.");
            isTeleporting = false;
            yield break;
        }

        // Optionally make invulnerable by setting colliders to triggers
        if (invulnerableDuringTeleport)
        {
            for (int i = 0; i < cachedColliders.Length; i++)
            {
                if (cachedColliders[i] == null) continue;
                cachedColliders[i].isTrigger = true;
            }
        }

        // Spawn arrival VFX and play sound at origin (teleport away effect)
        if (arrivalVFX != null) Instantiate(arrivalVFX, transform.position, Quaternion.identity);
        if (teleportSfx != null) AudioSource.PlayClipAtPoint(teleportSfx, transform.position, audioVolume);

        // move rigidbody safely
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false; // temporarily disable to avoid physics jitter
        }

        // teleport: ensure we place exactly on ground if we had a ground check earlier
        transform.position = chosen;
        if (rb != null)
        {
            // set the rigidbody position explicitly to avoid a frame of falling
            rb.position = chosen;
        }

        // arrival feedback
        if (arrivalVFX != null) Instantiate(arrivalVFX, transform.position, Quaternion.identity);
        if (teleportSfx != null) AudioSource.PlayClipAtPoint(teleportSfx, transform.position, audioVolume);

        // restore colliders to their original states BEFORE enabling physics so the object collides with ground
        if (invulnerableDuringTeleport)
        {
            for (int i = 0; i < cachedColliders.Length; i++)
            {
                if (cachedColliders[i] == null) continue;
                cachedColliders[i].isTrigger = originalIsTrigger[i];
            }
        }

        // re-enable physics and ensure we are snapped to ground (avoid a frame of falling)
        if (rb != null)
        {
            rb.simulated = true;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            // final snap: raycast down a short distance and correct Y if ground found
            RaycastHit2D finalGround = Physics2D.Raycast((Vector2)transform.position + Vector2.up * 0.1f, Vector2.down, groundRaycastDistance + 0.5f, groundMask);
            if (finalGround.collider)
            {
                Vector2 finalPos = transform.position;
                finalPos.y = finalGround.point.y + colliderBottomOffset + teleportElevation;
                transform.position = finalPos;
                rb.position = finalPos;
                rb.linearVelocity = Vector2.zero;
            }
        }

        lastTeleportTime = Time.time;
        isTeleporting = false;
        onTeleportEnd?.Invoke();

        yield break;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, triggerDistance);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, minTeleportDistance);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, maxTeleportDistance);
    }
}