using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class Stage5BossBatSummoner : MonoBehaviour
{
    [Header("Scene Wiring")]
    [SerializeField] private string stage5SceneName = "Floor5";
    [SerializeField] private string bossNameContains = "Stage-5 Boss";
    [SerializeField] private string batNameContains = "Demon Bat";

    [Header("Summon Rules")]
    [SerializeField] private float summonInterval = 5.5f;
    [SerializeField] private int maxActiveSummons = 3;
    [SerializeField] private int maxTotalSummons = 2;
    [SerializeField] private float minSummonX = 55.99f;
    [SerializeField] private float maxSummonX = 77.76f;
    [SerializeField] private float summonY = 1.13f;
    [SerializeField] private float minSpawnSeparation = 0.9f;

    [Header("Activation")]
    [SerializeField] private float sightCheckRadius = 30f;
    [SerializeField] private LayerMask lineOfSightBlockingMask = 0;

    private Enemy bossEnemy;
    private Transform player;
    private GameObject batTemplate;
    private readonly List<GameObject> activeSummons = new List<GameObject>();
    private int totalSummonsSpawned;

    private bool bossHasSeenPlayer;
    private float nextSummonTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureExistsForScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.name.Equals("Floor5"))
            return;

        if (FindFirstObjectByType<Stage5BossBatSummoner>() != null)
            return;

        var go = new GameObject("Stage5BossBatSummoner");
        go.AddComponent<Stage5BossBatSummoner>();
    }

    private void Start()
    {
        if (!SceneManager.GetActiveScene().name.Equals(stage5SceneName))
        {
            Destroy(gameObject);
            return;
        }

        bossEnemy = ResolveBossEnemy();
        player = ResolvePlayer();
        batTemplate = ResolveBatTemplate();
        nextSummonTime = Time.time + Mathf.Max(0.1f, summonInterval);
    }

    private void Update()
    {
        if (bossEnemy == null || player == null || batTemplate == null)
            return;

        if (!bossHasSeenPlayer)
        {
            if (!CanBossSeePlayer())
                return;

            bossHasSeenPlayer = true;
            nextSummonTime = Time.time + Mathf.Max(0.1f, summonInterval);
        }

        CleanupDestroyedSummons();

        if (totalSummonsSpawned >= Mathf.Max(1, maxTotalSummons))
            return;

        if (activeSummons.Count >= Mathf.Max(1, maxActiveSummons))
            return;

        if (Time.time < nextSummonTime)
            return;

        SpawnBatSummon();
        nextSummonTime = Time.time + Mathf.Max(0.1f, summonInterval);
    }

    private Enemy ResolveBossEnemy()
    {
        var allEnemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (var enemy in allEnemies)
        {
            if (enemy == null)
                continue;

            if (enemy.name.Contains(bossNameContains))
                return enemy;
        }

        return null;
    }

    private Transform ResolvePlayer()
    {
        var playerObj = GameObject.FindWithTag("Player");
        return playerObj != null ? playerObj.transform : null;
    }

    private GameObject ResolveBatTemplate()
    {
        var allEnemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        GameObject template = null;

        foreach (var enemy in allEnemies)
        {
            if (enemy == null || enemy.gameObject == null)
                continue;

            if (!enemy.name.Contains(batNameContains))
                continue;

            if (template == null)
                template = enemy.gameObject;
        }

        return template;
    }

    private bool CanBossSeePlayer()
    {
        if (bossEnemy == null || player == null)
            return false;

        Vector2 bossPos = bossEnemy.transform.position;
        Vector2 playerPos = player.position;
        float distance = Vector2.Distance(bossPos, playerPos);
        if (distance > Mathf.Max(1f, sightCheckRadius))
            return false;

        Vector2 dir = (playerPos - bossPos).normalized;

        if (lineOfSightBlockingMask.value != 0)
        {
            RaycastHit2D hit = Physics2D.Raycast(bossPos, dir, distance, lineOfSightBlockingMask);
            if (hit.collider != null)
                return false;
        }

        return true;
    }

    private void CleanupDestroyedSummons()
    {
        for (int i = activeSummons.Count - 1; i >= 0; i--)
        {
            if (activeSummons[i] == null)
                activeSummons.RemoveAt(i);
        }
    }

    private void SpawnBatSummon()
    {
        Vector3 spawnPos = Vector3.zero;
        bool foundFreeSpot = false;
        float xMin = Mathf.Min(minSummonX, maxSummonX);
        float xMax = Mathf.Max(minSummonX, maxSummonX);
        for (int attempt = 0; attempt < 8; attempt++)
        {
            float x = Random.Range(xMin, xMax);
            spawnPos = new Vector3(x, summonY, batTemplate.transform.position.z);
            if (!IsBatAlreadyNear(spawnPos))
            {
                foundFreeSpot = true;
                break;
            }
        }

        if (!foundFreeSpot)
            return;

        var summon = Instantiate(batTemplate, spawnPos, batTemplate.transform.rotation);
        summon.name = "Demon Bat Summon";

        // Randomize facing so each summon can come from left/right behavior-wise while staying on fixed X.
        Vector3 scale = summon.transform.localScale;
        float absX = Mathf.Abs(scale.x) <= 0.001f ? 1f : Mathf.Abs(scale.x);
        scale.x = (Random.value < 0.5f ? -1f : 1f) * absX;
        summon.transform.localScale = scale;

        activeSummons.Add(summon);
        totalSummonsSpawned++;
    }

    private bool IsBatAlreadyNear(Vector3 spawnPos)
    {
        var allEnemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        float minSeparation = Mathf.Max(0.1f, minSpawnSeparation);

        foreach (var enemy in allEnemies)
        {
            if (enemy == null)
                continue;

            if (!enemy.name.Contains(batNameContains))
                continue;

            if (Vector2.Distance(enemy.transform.position, spawnPos) < minSeparation)
                return true;
        }

        return false;
    }
}