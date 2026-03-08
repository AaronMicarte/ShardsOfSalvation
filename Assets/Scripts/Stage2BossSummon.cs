using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Enemy))]
public class Stage2BossSummon : MonoBehaviour
{
    [Range(0f, 1f)] public float healthThresholdPercent = 0.5f;
    public GameObject miniBossPrefab; // prefab for mini stage-2 boss (assign in inspector)
    public int spawnCount = 10;
    public float spawnRadius = 3f;
    public float spawnDelay = 0.05f;

    [Tooltip("Vertical offset above ground to place spawned mini bosses")]
    public float verticalSnapOffset = 0.12f;
    public LayerMask groundMask = Physics2D.DefaultRaycastLayers;
    public float groundRaycastDistance = 5f;

    public bool onlyOnce = true; // if true, summon only once when threshold is crossed

    private Enemy enemy;
    private bool summoned = false;

    void Start()
    {
        enemy = GetComponent<Enemy>();
        if (miniBossPrefab == null)
            Debug.LogWarning($"{name}: miniBossPrefab is not assigned in {nameof(Stage2BossSummon)}.");
    }

    void Update()
    {
        if (summoned && onlyOnce) return;
        if (enemy == null) return;

        float hp = enemy.GetHealthPercent();
        if (hp <= healthThresholdPercent && (!onlyOnce || !summoned))
        {
            StartCoroutine(SummonRoutine());
        }
    }

    public IEnumerator SummonRoutine()
    {
        if (miniBossPrefab == null) yield break;
        summoned = true;

        Vector3 origin = transform.position;

        for (int i = 0; i < spawnCount; i++)
        {
            // random offset in circle
            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            Vector3 rawPos = origin + new Vector3(offset.x, offset.y, 0f);

            // try to snap down to ground if possible
            Vector2 start = (Vector2)rawPos + Vector2.up * 0.5f;
            RaycastHit2D hit = Physics2D.Raycast(start, Vector2.down, groundRaycastDistance, groundMask);
            Vector3 spawnPos;
            if (hit.collider != null)
            {
                spawnPos = new Vector3(rawPos.x, hit.point.y + verticalSnapOffset, rawPos.z);
            }
            else
            {
                spawnPos = rawPos; // fallback
            }

            var go = Instantiate(miniBossPrefab, spawnPos, Quaternion.identity);

            // initialize mini boss HP to 2 if it has an Enemy component
            var e = go.GetComponent<Enemy>();
            if (e != null)
            {
                e.InitializeSpawnHP(2);
                e.ResetForSpawn();
            }

            // Ensure colliders are enabled and non-trigger so they interact with ground
            var cols = go.GetComponentsInChildren<Collider2D>(true);
            foreach (var c in cols)
            {
                if (c == null) continue;
                c.isTrigger = false;
                c.enabled = true;
            }

            // small stabilization: wait a couple FixedUpdates and nudge upward if overlapping
            StartCoroutine(StabilizeSpawn(go));

            yield return new WaitForSeconds(spawnDelay);
        }

        yield break;
    }

    private IEnumerator StabilizeSpawn(GameObject go)
    {
        if (go == null) yield break;
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        // try a couple of attempts to move upward if overlapping ground colliders
        int attempts = 0;
        const int maxAttempts = 6;
        const float stepY = 0.12f;

        while (attempts < maxAttempts)
        {
            if (go == null) yield break;
            Vector2 pos = go.transform.position;
            Collider2D[] hits = Physics2D.OverlapCircleAll(pos, 0.12f);
            bool foundBlocking = false;
            foreach (var h in hits)
            {
                if (h == null) continue;
                if (h.isTrigger) continue;
                float dy = Mathf.Abs(h.bounds.center.y - pos.y);
                if (dy > 0.5f) continue;
                foundBlocking = true; break;
            }

            if (!foundBlocking) break;

            go.transform.position = go.transform.position + Vector3.up * stepY;
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            attempts++;
            yield return new WaitForFixedUpdate();
        }
    }

    [ContextMenu("Summon Now")]
    public void SummonNow()
    {
        StartCoroutine(SummonRoutine());
    }
}