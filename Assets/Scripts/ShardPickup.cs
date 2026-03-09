using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
public class ShardPickup : MonoBehaviour
{
    [Tooltip("Optional particle or visual feedback")]
    [SerializeField] private GameObject pickupVFX;
    [SerializeField, Tooltip("Optional sound to play when shard is collected")] private AudioClip pickupClip;
    [SerializeField, Tooltip("Volume for pickup sound (0-1)"), Range(0f, 1f)] private float pickupVolume = 1f;

    [Header("Spawn Motion (No Animator Needed)")]
    [SerializeField, Tooltip("If enabled, shard pops up then settles down when spawned.")] private bool playSpawnMotion = true;
    [SerializeField, Tooltip("How high the shard moves upward before dropping back.")] private float spawnPopHeight = 0.75f;
    [SerializeField, Tooltip("Seconds for the upward part of the spawn motion.")] private float spawnPopDuration = 0.12f;
    [SerializeField, Tooltip("Seconds for the downward part of the spawn motion.")] private float spawnDropDuration = 0.18f;
    [SerializeField, Tooltip("Delay before the shard can be picked up after spawning.")] private float pickupEnableDelay = 0.08f;

    [Header("Idle Float")]
    [SerializeField, Tooltip("If enabled, shard gently moves up/down while waiting to be picked.")] private bool idleFloat = true;
    [SerializeField, Tooltip("Vertical bob amount in world units.")] private float floatAmplitude = 0.06f;
    [SerializeField, Tooltip("Bobbing speed in cycles per second.")] private float floatFrequency = 1.8f;

    [Header("Final Scene Transition")]
    [SerializeField, Tooltip("If true, this pickup can trigger FinalScene when collected in Floor5 after final boss defeat.")]
    private bool allowFinalSceneTransition = true;
    [SerializeField, Tooltip("Scene name to load when final Stage 5 shard is collected.")]
    private string finalSceneName = "FinalScene";

    private bool canBePicked = true;
    private bool floatActive = false;
    private float floatTimer = 0f;
    private Vector3 settledPosition;

    void Awake()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    void OnEnable()
    {
        settledPosition = transform.position;
        canBePicked = true;
        floatActive = false;

        if (playSpawnMotion)
            StartCoroutine(SpawnMotionRoutine());
        else
            floatActive = idleFloat;
    }

    void Update()
    {
        if (!floatActive)
            return;

        floatTimer += Time.deltaTime;
        float yOffset = Mathf.Sin(floatTimer * Mathf.PI * 2f * floatFrequency) * floatAmplitude;
        transform.position = settledPosition + Vector3.up * yOffset;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!canBePicked)
            return;

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

        TryLoadFinalSceneAfterPickup();

        Destroy(gameObject);
    }

    private void TryLoadFinalSceneAfterPickup()
    {
        if (!allowFinalSceneTransition) return;
        if (!FinalSequenceState.Stage5BossDefeated) return;

        string sceneName = SceneManager.GetActiveScene().name;
        if (!sceneName.Equals("Floor5")) return;
        if (string.IsNullOrWhiteSpace(finalSceneName)) return;

        SceneManager.LoadScene(finalSceneName);
    }

    private IEnumerator SpawnMotionRoutine()
    {
        canBePicked = false;
        floatActive = false;
        floatTimer = 0f;

        settledPosition = transform.position;
        Vector3 start = settledPosition;
        Vector3 apex = start + Vector3.up * spawnPopHeight;

        float upDuration = Mathf.Max(0.01f, spawnPopDuration);
        float downDuration = Mathf.Max(0.01f, spawnDropDuration);

        float t = 0f;
        while (t < upDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / upDuration);
            float eased = 1f - Mathf.Pow(1f - k, 2f);
            transform.position = Vector3.LerpUnclamped(start, apex, eased);
            yield return null;
        }

        t = 0f;
        while (t < downDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / downDuration);
            float eased = k * k;
            transform.position = Vector3.LerpUnclamped(apex, start, eased);
            yield return null;
        }

        transform.position = settledPosition;
        floatActive = idleFloat;

        if (pickupEnableDelay > 0f)
            yield return new WaitForSeconds(pickupEnableDelay);

        canBePicked = true;
    }
}
