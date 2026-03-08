using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns reinforcement enemies at a given position with optional spawn cap and small random offset.
/// Simple and safe to use for occasional reinforcements; supports a cap and cleanup tracking.
/// </summary>
public class ReinforcementSpawner : MonoBehaviour
{
    [Header("Prefab")]
    public GameObject enemyPrefab;

    [Header("Spawn Settings")]
    [Range(0f, 1f)] public float spawnChance = 0.15f;
    public int spawnCount = 1;
    public float spawnDelay = 0.15f;
    public float spawnRadius = 1f;

    [Header("Spawn Safety")]
    [Tooltip("Layers considered ground for vertical snapping (leave default to use Physics2D default layers)")]
    public LayerMask groundMask = Physics2D.DefaultRaycastLayers;
    [Tooltip("Max distance to search downward for ground when spawning.")]
    public float groundRaycastDistance = 5f;
    [Tooltip("Vertical offset above the ground hit point to place spawned enemy.")]
    public float verticalSnapOffset = 0.12f;
    [Tooltip("Radius used to detect overlaps when spawning (small) to avoid embedding in colliders.")]
    public float spawnOverlapRadius = 0.12f;
    [Tooltip("Max attempts to resolve overlaps by moving upward.")]
    public int maxOverlapResolveAttempts = 8;
    [Tooltip("Vertical step used when resolving overlaps.")]
    public float overlapResolveStepY = 0.15f;
    [Tooltip("If true, force colliders on spawned enemies to isTrigger=false to avoid falling through.")]
    public bool enforceNonTriggerOnSpawn = true;

    [Header("Debug")]
    public bool debugLogging = false;

    [Header("Cap & Tracking")]
    public int maxActive = 10; // max active spawned by this spawner

    // active spawned instances tracked so we can respect maxActive
    private readonly HashSet<GameObject> activeSpawned = new HashSet<GameObject>();

    public void TrySpawnAt(Vector3 position)
    {
        TrySpawnAt(position, spawnCount, spawnChance, spawnDelay);
    }

    public void TrySpawnAt(Vector3 position, int count, float chance, float delay)
    {
        if (enemyPrefab == null)
        {
            if (debugLogging) Debug.LogWarning("ReinforcementSpawner: enemyPrefab is null.");
            return;
        }

        float chance01 = NormalizeChance01(chance);
        if (chance01 <= 0f)
        {
            if (debugLogging) Debug.Log("ReinforcementSpawner: chance is 0, no spawn.");
            return;
        }

        if (Random.value > chance01)
        {
            if (debugLogging) Debug.Log($"ReinforcementSpawner: spawn skipped by chance ({chance01 * 100f:0.#}%).");
            return;
        }

        if (activeSpawned.Count >= maxActive)
        {
            if (debugLogging) Debug.Log("ReinforcementSpawner: maxActive reached, no spawn.");
            return;
        }

        StartCoroutine(SpawnBurstAt(position, count > 0 ? count : spawnCount, delay > 0f ? delay : spawnDelay));
    }

    private IEnumerator SpawnBurstAt(Vector3 position, int count, float delay)
    {
        for (int i = 0; i < count; i++)
        {
            if (activeSpawned.Count >= maxActive) yield break;

            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            Vector3 rawPos = position + new Vector3(offset.x, offset.y, 0f);

            // Try to snap the spawn position down to the ground if possible to avoid floating or spawning under geometry
            Vector3 spawnPos = GetSafeSpawnPosition(rawPos);

            if (debugLogging) Debug.Log($"ReinforcementSpawner: spawning at {spawnPos} (raw {rawPos})");

            var go = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
            activeSpawned.Add(go);

            // Ensure spawned instance uses the same animator controller and sprite settings as the prefab
            var prefabAnim = enemyPrefab.GetComponent<Animator>();
            var goAnim = go.GetComponent<Animator>();
            if (goAnim != null && prefabAnim != null && prefabAnim.runtimeAnimatorController != null)
            {
                goAnim.runtimeAnimatorController = prefabAnim.runtimeAnimatorController;
                goAnim.enabled = true;
                goAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                goAnim.updateMode = AnimatorUpdateMode.Normal;
                goAnim.speed = 1f;
                try { goAnim.Rebind(); goAnim.Update(0f); } catch { }

                // Copy all Animator parameters from prefab to ensure identical starting state
                foreach (var param in prefabAnim.parameters)
                {
                    if (param == null) continue;
                    try
                    {
                        switch (param.type)
                        {
                            case AnimatorControllerParameterType.Bool:
                                goAnim.SetBool(param.name, prefabAnim.GetBool(param.name));
                                break;
                            case AnimatorControllerParameterType.Int:
                                goAnim.SetInteger(param.name, prefabAnim.GetInteger(param.name));
                                break;
                            case AnimatorControllerParameterType.Float:
                                goAnim.SetFloat(param.name, prefabAnim.GetFloat(param.name));
                                break;
                            case AnimatorControllerParameterType.Trigger:
                                // Triggers are typically reset; keep reset behavior for safety
                                goAnim.ResetTrigger(param.name);
                                break;
                        }
                    }
                    catch { }
                }
            }
            var prefabSr = enemyPrefab.GetComponentInChildren<SpriteRenderer>(true);
            var goSr = go.GetComponentInChildren<SpriteRenderer>(true);
            if (prefabSr != null && goSr != null)
            {
                goSr.sortingLayerID = prefabSr.sortingLayerID;
                goSr.sortingOrder = prefabSr.sortingOrder;
            }

            // Reset physics and animator to a safe default so the enemy doesn't fly away or stay dead
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.simulated = true;
            }

            var anim = go.GetComponent<Animator>();
            if (anim != null)
            {
                anim.enabled = true;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                anim.updateMode = AnimatorUpdateMode.Normal;
                anim.speed = 1f;
                // Try to set to idle state to avoid instant-dead animations; guard exceptions
                try { anim.SetBool("isAttacking", false); anim.SetFloat("Speed", 0f); } catch { }
                try { anim.Play("idle", 0, 0f); anim.Update(0f); } catch { }
            }
            // Force colliders to non-trigger if requested so spawned enemies collide with the world
            if (enforceNonTriggerOnSpawn)
            {
                var cols = go.GetComponentsInChildren<Collider2D>(true);
                foreach (var c in cols)
                {
                    if (c == null) continue;
                    c.isTrigger = false;
                    c.enabled = true;
                }
            }
            // If the spawned prefab has an Enemy script, initialize it for spawn (2 HP), ensure it starts in Idle,
            // and make it face the player so it doesn't look backwards.
            var enemyComp = go.GetComponent<Enemy>();
            if (enemyComp != null)
            {
                enemyComp.InitializeSpawnHP(2);
                enemyComp.ResetForSpawn();

                // Face the player if possible (flip on X)
                var player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    Vector3 dir = player.transform.position - go.transform.position;
                    if (dir.sqrMagnitude > 0.001f)
                    {
                        var ls = go.transform.localScale;
                        ls.x = Mathf.Sign(dir.x) * Mathf.Abs(ls.x);
                        go.transform.localScale = ls;
                    }
                }
            }

            // Attach a small marker that notifies this spawner when destroyed so we can keep counts accurate
            var marker = go.AddComponent<SpawnedBySpawner>();
            marker.owner = this;

            // After spawning, schedule a short stabilization to ensure the instance is not trapped inside geometry
            StartCoroutine(StabilizeSpawn(go));

            // Ensure animator applies first frame immediately
            var a = go.GetComponent<Animator>(); if (a != null) { try { a.Update(0f); } catch { } }

            yield return new WaitForSeconds(delay);
        }
    }

    private IEnumerator StabilizeSpawn(GameObject go)
    {
        if (go == null) yield break;
        // Wait a few fixed updates so physics and tilemaps settle
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        int attempts = 0;
        while (attempts < maxOverlapResolveAttempts)
        {
            if (go == null) yield break;

            Vector2 pos = go.transform.position;
            Collider2D[] hits = Physics2D.OverlapCircleAll(pos, spawnOverlapRadius, groundMask);
            bool foundBlocking = false;
            foreach (var h in hits)
            {
                if (h == null) continue;
                if (h.isTrigger) continue;
                float dy = Mathf.Abs(h.bounds.center.y - pos.y);
                if (dy > 0.5f) continue;
                foundBlocking = true;
                break;
            }

            if (!foundBlocking) break;

            // Move the object upward slightly and zero velocity
            go.transform.position = go.transform.position + Vector3.up * overlapResolveStepY;
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            attempts++;
            yield return new WaitForFixedUpdate();
        }

        // After physics stabilization, ensure animator is correct and not stuck in dead
        if (go != null)
        {
            var e = go.GetComponent<Enemy>();
            var a = go.GetComponent<Animator>();
            if (e != null) e.ForceResetAnimator();
            if (a != null && e != null && !e.IsDead() && !string.IsNullOrEmpty(e.DeathStateName))
            {
                var st = a.GetCurrentAnimatorStateInfo(0);
                if (st.IsName(e.DeathStateName))
                {
                    if (debugLogging) Debug.LogWarning($"ReinforcementSpawner: animator stuck in '{e.DeathStateName}' after spawn; forcing '{e.IdleStateName}'.");
                    try { a.Play(e.IdleStateName, 0, 0f); a.Update(0f); } catch { }
                }
            }
        }
    }

    private Vector3 GetSafeSpawnPosition(Vector3 rawPos)
    {
        // Raycast downwards from slightly above the rawPos to find ground
        Vector2 start = rawPos + Vector3.up * 0.5f;
        RaycastHit2D hit = Physics2D.Raycast(start, Vector2.down, groundRaycastDistance, groundMask);
        Vector3 basePos;
        if (hit.collider != null)
        {
            basePos = new Vector3(rawPos.x, hit.point.y + verticalSnapOffset, rawPos.z);
        }
        else
        {
            // If we didn't hit anything, try a small upward correction (avoid spawning under floor geometry)
            RaycastHit2D upHit = Physics2D.Raycast(rawPos, Vector2.up, groundRaycastDistance, groundMask);
            if (upHit.collider != null)
            {
                basePos = new Vector3(rawPos.x, upHit.point.y + verticalSnapOffset, rawPos.z);
            }
            else
            {
                // Fallback: use rawPos
                basePos = rawPos;
            }
        }

        // Ensure we are not overlapping other colliders at spawn. If overlap occurs, attempt to move up in steps.
        for (int i = 0; i < maxOverlapResolveAttempts; i++)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll((Vector2)basePos, spawnOverlapRadius, Physics2D.DefaultRaycastLayers);
            bool foundBlocking = false;
            foreach (var h in hits)
            {
                if (h == null) continue;
                // ignore triggers and the tilemap's triggers, consider only solid colliders
                if (h.isTrigger) continue;
                // If the hit collider is far away in Y (different platform), it's fine
                float dy = Mathf.Abs(h.bounds.center.y - basePos.y);
                if (dy > 0.5f) continue;
                foundBlocking = true;
                break;
            }

            if (!foundBlocking)
                return basePos;

            // move up and try again
            basePos.y += overlapResolveStepY;
        }

        // Final fallback: return original basePos (may overlap)
        return basePos;
    }

    // Called by SpawnedBySpawner when the spawned object is destroyed
    public void NotifySpawnedDestroyed(GameObject g)
    {
        if (g == null) return;
        activeSpawned.Remove(g);
    }

    [ContextMenu("Test Spawn Here")]
    public void TestSpawn()
    {
        TrySpawnAt(transform.position);
    }

    private float NormalizeChance01(float chance)
    {
        if (chance > 1f) chance /= 100f; // allow using 0..100 in inspector
        return Mathf.Clamp01(chance);
    }
}
