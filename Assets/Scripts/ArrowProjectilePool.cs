using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple pool for ArrowProjectile instances. Pre-instantiates a number of projectiles and reuses them to avoid GC & allocations.
/// Assign the Arrow projectile prefab and place this component in the scene (e.g., on a GameManager object).
/// </summary>
public class ArrowProjectilePool : MonoBehaviour
{
    public static ArrowProjectilePool Instance;

    [Tooltip("Projectile prefab to pool (must have ArrowProjectile component)")]
    public GameObject projectilePrefab;
    [Tooltip("Initial size of the pool")]
    public int initialSize = 20;
    [Tooltip("If true, the pool will instantiate new instances when empty")]
    public bool allowGrow = true;

    private Queue<ArrowProjectile> pool = new Queue<ArrowProjectile>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Debug.LogWarning("Multiple ArrowProjectilePool instances exist. Using the first created instance.");

        if (projectilePrefab == null)
        {
            Debug.LogWarning("ArrowProjectilePool: projectilePrefab is not assigned.");
            return;
        }

        for (int i = 0; i < Mathf.Max(0, initialSize); i++)
        {
            var go = Instantiate(projectilePrefab, transform);
            go.SetActive(false);
            var ap = go.GetComponent<ArrowProjectile>();
            if (ap == null)
            {
                Debug.LogError("ArrowProjectilePool: projectilePrefab is missing ArrowProjectile component.");
                Destroy(go);
                continue;
            }
            ap.pooled = true;
            ap.pool = this;
            pool.Enqueue(ap);
        }
    }

    public ArrowProjectile Spawn(Vector3 pos, Vector2 dir, string ownerTag = "Enemy")
    {
        ArrowProjectile ap = null;
        if (pool.Count > 0)
        {
            ap = pool.Dequeue();
        }
        else if (allowGrow && projectilePrefab != null)
        {
            var go = Instantiate(projectilePrefab, transform);
            ap = go.GetComponent<ArrowProjectile>();
            if (ap == null) { Destroy(go); return null; }
            ap.pooled = true;
            ap.pool = this;
        }
        else
        {
            Debug.LogWarning("ArrowProjectilePool: No available projectiles in pool and growth disallowed.");
            return null;
        }

        // Activate and initialize
        ap.gameObject.SetActive(true);
        // Detach from pool so the projectile appears at the world root (not under UI/EventSystem etc.)
        ap.transform.SetParent(null);
        ap.transform.localScale = Vector3.one;
        ap.ResetForPool(dir, pos, ownerTag);
        return ap;
    }

    public void Return(ArrowProjectile ap)
    {
        if (ap == null) return;
        // Reparent back under the pool so the scene stays tidy while the projectile is inactive
        ap.transform.SetParent(transform);
        ap.gameObject.SetActive(false);
        pool.Enqueue(ap);
    }
}