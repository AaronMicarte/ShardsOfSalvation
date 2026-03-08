using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns a boss only after all tracked minions are dead, then drops the boss from above.
/// Attach this to an always-active scene object (for example: GameManager).
/// </summary>
public class BossSpawnAfterMinions : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("Boss root GameObject to spawn.")]
    private GameObject bossObject;

    [SerializeField, Tooltip("Minions that must die before the boss spawns.")]
    private List<Enemy> minionEnemies = new List<Enemy>();

    [SerializeField, Tooltip("Optional exact landing point. If empty, uses boss object's current position.")]
    private Transform bossLandingPoint;

    [Header("Start State")]
    [SerializeField, Tooltip("If true, force bossObject inactive at start.")]
    private bool forceBossInactiveOnStart = true;

    [Header("Drop Animation")]
    [SerializeField, Tooltip("How high above the landing point the boss starts.")]
    private float dropHeight = 8f;

    [SerializeField, Tooltip("How long the drop takes in seconds.")]
    private float dropDuration = 1.2f;

    [SerializeField, Tooltip("Easing for the drop movement. X=time(0..1), Y=progress(0..1).")]
    private AnimationCurve dropCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [SerializeField, Tooltip("If true, disables Enemy behavior until the boss lands.")]
    private bool disableEnemyAIWhileDropping = true;

    private bool bossSpawnStarted;

    void Awake()
    {
        if (bossObject == null)
        {
            Debug.LogWarning($"{name}: Boss reference is missing in {nameof(BossSpawnAfterMinions)}.");
            return;
        }

        if (forceBossInactiveOnStart && bossObject.activeSelf)
            bossObject.SetActive(false);
    }

    void Update()
    {
        if (bossSpawnStarted || bossObject == null)
            return;

        if (AreAllMinionsDead())
        {
            bossSpawnStarted = true;
            StartCoroutine(SpawnBossDropRoutine());
        }
    }

    private bool AreAllMinionsDead()
    {
        if (minionEnemies == null || minionEnemies.Count == 0)
            return false;

        // Unity null-check handles destroyed objects. Destroyed minions are treated as dead.
        for (int i = 0; i < minionEnemies.Count; i++)
        {
            var minion = minionEnemies[i];
            if (minion == null)
                continue;

            if (!minion.IsDead())
                return false;
        }

        return true;
    }

    private IEnumerator SpawnBossDropRoutine()
    {
        Vector3 landingPos = bossLandingPoint != null ? bossLandingPoint.position : bossObject.transform.position;
        Vector3 startPos = landingPos + Vector3.up * Mathf.Max(0f, dropHeight);

        if (!bossObject.activeSelf)
            bossObject.SetActive(true);

        Enemy bossEnemy = bossObject.GetComponent<Enemy>();
        bool hadEnemy = bossEnemy != null;
        bool originalEnemyEnabled = hadEnemy && bossEnemy.enabled;

        if (hadEnemy && disableEnemyAIWhileDropping)
            bossEnemy.enabled = false;

        bossObject.transform.position = startPos;

        float duration = Mathf.Max(0.01f, dropDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = dropCurve != null ? dropCurve.Evaluate(t) : t;
            bossObject.transform.position = Vector3.LerpUnclamped(startPos, landingPos, eased);
            yield return null;
        }

        bossObject.transform.position = landingPos;

        var rb = bossObject.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        if (hadEnemy && disableEnemyAIWhileDropping)
            bossEnemy.enabled = originalEnemyEnabled;

        Debug.Log($"{name}: Boss spawned after all minions died.");
    }
}
