using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

public class PortalTrigger : MonoBehaviour
{
    [SerializeField] string sceneToLoad = "Floor2";
    [Tooltip("Tag used to find remaining enemies (defaults to 'Enemy')")]
    [SerializeField] string enemyTag = "Enemy";

    [Header("UI (optional)")]
    [Tooltip("Assign a TextMeshProUGUI to show messages to the player when trying to enter the portal while enemies remain.")]
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private float messageDuration = 2f;

    [Header("Filtering (optional)")]
    [Tooltip("If assigned, only enemies inside this Collider2D will be considered (useful for per-stage checks)")]
    [SerializeField] private Collider2D areaCollider;

    [Header("Shard Requirement")]
    [Tooltip("If true, player must collect enough shards before this portal can be used")]
    [SerializeField] private bool requireShardPickup = true;
    [Tooltip("Minimum shards required to enter this portal")]
    [SerializeField] private int requiredShardCount = 1;
    [SerializeField] private string missingShardMessage = "Can't enter portal - pick up the shard first";

    private Coroutine messageCoroutine;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (AreEnemiesRemaining())
        {
            ShowMessage("Can't enter portal — enemies remain");
            return;
        }

        if (!HasRequiredShards())
        {
            ShowMessage(missingShardMessage);
            return;
        }

        // Advancing stages commits a new checkpoint for future retries.
        Player.CommitCurrentDropBuffStacksAsStageCheckpoint();

        // Optional: Debug.Log("Player hit portal");
        SceneManager.LoadScene(sceneToLoad);
    }

    private bool AreEnemiesRemaining()
    {
        return CountRemainingEnemiesInArea() > 0;
    }

    private int CountRemainingEnemiesInArea()
    {
        int count = 0;

        // Prefer Enemy components to avoid relying on tags being perfectly configured.
        var enemyComponents = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        var countedObjectIds = new HashSet<int>();
        foreach (var enemy in enemyComponents)
        {
            if (enemy == null || enemy.gameObject == null || !enemy.gameObject.activeInHierarchy)
                continue;

            // If an area collider is assigned, skip enemies outside it
            if (areaCollider != null)
            {
                if (!areaCollider.bounds.Contains(enemy.transform.position))
                    continue;
            }

            countedObjectIds.Add(enemy.gameObject.GetInstanceID());
            if (!enemy.IsDead())
                count++;
        }

        // Fallback: also include tagged objects with no Enemy component.
        if (string.IsNullOrWhiteSpace(enemyTag))
            return count;

        try
        {
            var taggedEnemies = GameObject.FindGameObjectsWithTag(enemyTag);
            if (taggedEnemies == null || taggedEnemies.Length == 0)
                return count;

            foreach (var e in taggedEnemies)
            {
                if (e == null || !e.activeInHierarchy)
                    continue;

                if (countedObjectIds.Contains(e.GetInstanceID()))
                    continue;

                if (areaCollider != null && !areaCollider.bounds.Contains(e.transform.position))
                    continue;

                count++;
            }
        }
        catch (UnityException)
        {
            // Tag not defined; component-based count above is sufficient.
        }

        return count;
    }

    private void ShowMessage(string text)
    {
        EnsureMessageText();
        if (messageText == null)
        {
            Debug.LogWarning($"PortalTrigger: No message Text assigned and none found in scene. Message '{text}' skipped.");
            return;
        }
        if (messageCoroutine != null) StopCoroutine(messageCoroutine);
        messageCoroutine = StartCoroutine(ShowMessageRoutine(text));
    }

    private IEnumerator ShowMessageRoutine(string text)
    {
        messageText.text = text;
        messageText.gameObject.SetActive(true);
        yield return new WaitForSeconds(messageDuration);
        messageText.gameObject.SetActive(false);
        messageCoroutine = null;
    }

    private void EnsureMessageText()
    {
        if (messageText != null) return;
        var all = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in all)
        {
            var n = t.name.ToLowerInvariant();
            if (n.Contains("rage") || n.Contains("buff") || n.Contains("crit") || n.Contains("hud") || n.Contains("status"))
                continue;

            if (n.Contains("message") || n.Contains("notice") || n.Contains("warning") || n.Contains("alert") || n.Contains("portal"))
            {
                messageText = t;
                Debug.Log($"PortalTrigger: auto-assigned messageText -> {t.name}");
                return;
            }
        }

        messageText = GetOrCreateRuntimeMessageText();
    }

    private TextMeshProUGUI GetOrCreateRuntimeMessageText()
    {
        const string canvasName = "GameplayMessageCanvas";
        const string textName = "GameplayMessageText";

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
        for (int i = 0; i < existingText.Length; i++)
        {
            if (existingText[i] != null && existingText[i].name == textName)
                return existingText[i];
        }

        var textGo = new GameObject(textName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(targetCanvas.transform, false);

        var rect = textGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -120f);
        rect.sizeDelta = new Vector2(900f, 120f);

        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = 34f;
        tmp.alignment = TextAlignmentOptions.Top;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        textGo.SetActive(false);
        return tmp;
    }

    private bool HasRequiredShards()
    {
        if (!requireShardPickup) return true;

        int needed = Mathf.Max(1, requiredShardCount);
        if (ShardManager.Instance == null)
        {
            Debug.LogWarning("PortalTrigger: ShardManager not found; portal entry blocked by shard requirement.");
            return false;
        }

        return ShardManager.Instance.ShardCount >= needed;
    }
}