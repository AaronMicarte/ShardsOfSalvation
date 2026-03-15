using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

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

    [Header("Stage 5 Enemy-Clear Warning")]
    [SerializeField, Tooltip("Optional message text shown when player touches shard before clearing all enemies on Floor5.")]
    private TextMeshProUGUI warningMessageText;
    [SerializeField, Tooltip("Popup message shown when shard cannot be picked yet.")]
    private string defeatEnemiesFirstMessage = "Defeat all enemies first before claiming this shard";
    [SerializeField, Tooltip("Seconds to keep the warning visible.")]
    private float warningMessageDuration = 2f;

    private bool canBePicked = true;
    private bool floatActive = false;
    private float floatTimer = 0f;
    private Vector3 settledPosition;
    private Coroutine warningMessageCoroutine;

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

        if (ShouldRequireEnemyClearBeforePickup() && AreLivingEnemiesRemaining())
        {
            ShowWarningMessage(defeatEnemiesFirstMessage);
            return;
        }

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
        if (AreLivingEnemiesRemaining()) return;
        if (string.IsNullOrWhiteSpace(finalSceneName)) return;

        SceneManager.LoadScene(finalSceneName);
    }

    private bool ShouldRequireEnemyClearBeforePickup()
    {
        if (!allowFinalSceneTransition)
            return false;

        return SceneManager.GetActiveScene().name.Equals("Floor5");
    }

    private bool AreLivingEnemiesRemaining()
    {
        var enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (var enemy in enemies)
        {
            if (enemy == null)
                continue;

            if (!enemy.gameObject.activeInHierarchy)
                continue;

            if (!enemy.IsDead())
                return true;
        }

        return false;
    }

    private void ShowWarningMessage(string text)
    {
        EnsureWarningMessageText();
        if (warningMessageText == null)
            return;

        if (warningMessageCoroutine != null)
            StopCoroutine(warningMessageCoroutine);

        warningMessageCoroutine = StartCoroutine(ShowWarningMessageRoutine(text));
    }

    private IEnumerator ShowWarningMessageRoutine(string text)
    {
        warningMessageText.text = text;
        warningMessageText.gameObject.SetActive(true);
        yield return new WaitForSeconds(warningMessageDuration);
        warningMessageText.gameObject.SetActive(false);
        warningMessageCoroutine = null;
    }

    private void EnsureWarningMessageText()
    {
        if (warningMessageText != null)
            return;

        var all = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in all)
        {
            if (t == null)
                continue;

            string name = t.name.ToLowerInvariant();
            if (name.Contains("message") || name.Contains("warning") || name.Contains("notice") || name.Contains("alert"))
            {
                warningMessageText = t;
                return;
            }
        }

        warningMessageText = GetOrCreateRuntimeWarningText();
    }

    private TextMeshProUGUI GetOrCreateRuntimeWarningText()
    {
        const string canvasName = "Stage5ObjectiveMessageCanvas";
        const string textName = "Stage5ObjectiveMessageText";

        Canvas targetCanvas = null;
        var existingCanvas = GameObject.Find(canvasName);
        if (existingCanvas != null)
            targetCanvas = existingCanvas.GetComponent<Canvas>();

        if (targetCanvas == null)
        {
            var canvasGo = new GameObject(canvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            targetCanvas = canvasGo.GetComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        var existingText = targetCanvas.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var text in existingText)
        {
            if (text != null && text.name == textName)
                return text;
        }

        var textGo = new GameObject(textName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(targetCanvas.transform, false);

        var rect = textGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -120f);
        rect.sizeDelta = new Vector2(1100f, 120f);

        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = 34f;
        tmp.color = new Color(1f, 0.90f, 0.62f, 1f);
        tmp.alignment = TextAlignmentOptions.Top;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;

        textGo.SetActive(false);
        return tmp;
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
