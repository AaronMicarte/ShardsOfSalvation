using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;

/// <summary>
/// Temporarily inverts the player's left/right controls. Designed to be started/stopped
/// by a Floor manager or triggered when entering Floor 3.
/// </summary>
public class ControlsInversion : MonoBehaviour
{
    [Header("Target")]
    [SerializeField, Tooltip("Player component to affect")] private Player player;

    [Header("Timing")]
    [SerializeField, Tooltip("If true, inversion runs for a fixed duration then stops automatically")] private bool autoStop = true;
    [SerializeField, Tooltip("Duration of the inversion (seconds) when autoStop is enabled")] private float inversionDuration = 3f;

    [Header("Feedback")]
    [SerializeField, Tooltip("Optional TextMeshProUGUI used to display an informational message (leave empty to disable)")] private TextMeshProUGUI messageText = null;
    [SerializeField, Tooltip("Message to show when controls are swapped")] private string inversionMessage = "Controls swapped!";
    [SerializeField] private float messageDuration = 2f;
    [SerializeField] private float messageFade = 0.4f;

    private float originalMultiplier = 1f;
    private Coroutine inversionCoroutine;
    private Coroutine messageCoroutine;

    void Reset()
    {
        // try to find a Player in the same GameObject or nearby
        player = GetComponent<Player>() ?? (transform.parent != null ? transform.parent.GetComponentInChildren<Player>() : null);
    }

    public void StartInversion()
    {
        if (player == null)
        {
            return;
        }

        if (inversionCoroutine != null) StopCoroutine(inversionCoroutine);
        originalMultiplier = player.GetInputMultiplier();
        player.SetInputMultiplier(-originalMultiplier);
        ShowMessage(inversionMessage, messageDuration);

        if (autoStop)
            inversionCoroutine = StartCoroutine(AutoStop(inversionDuration));
    }

    public void StopInversion()
    {
        if (player == null) return;
        if (inversionCoroutine != null) { StopCoroutine(inversionCoroutine); inversionCoroutine = null; }
        player.SetInputMultiplier(originalMultiplier);
    }

    private IEnumerator AutoStop(float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            yield return null;
        }
        StopInversion();
    }

    // --- simple message helper ---
    public void ShowMessage(string message, float duration = -1f)
    {
        EnsureMessageText();
        if (messageText == null)
        {
            Debug.LogWarning($"ControlsInversion: No message Text assigned and none found in scene. Message '{message}' skipped.");
            return;
        }

        if (duration <= 0f) duration = messageDuration;
        if (messageCoroutine != null) StopCoroutine(messageCoroutine);
        messageCoroutine = StartCoroutine(ShowMessageRoutine(message, duration));
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

            if (n.Contains("message") || n.Contains("notice") || n.Contains("warning") || n.Contains("alert") || n.Contains("control"))
            {
                messageText = t;
                Debug.Log($"ControlsInversion: auto-assigned messageText -> {t.name}");
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

    private IEnumerator ShowMessageRoutine(string message, float duration)
    {
        messageText.text = message;
        Color c = messageText.color;
        c.a = 1f;
        messageText.color = c;
        messageText.gameObject.SetActive(true);

        float elapsed = 0f;
        float fadeStart = Mathf.Max(0f, duration - messageFade);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (elapsed >= fadeStart)
            {
                float f = (elapsed - fadeStart) / Mathf.Max(0.0001f, messageFade);
                Color cc = messageText.color;
                cc.a = Mathf.Lerp(1f, 0f, f);
                messageText.color = cc;
            }
            yield return null;
        }

        messageText.gameObject.SetActive(false);
        messageCoroutine = null;
    }

    [ContextMenu("Test Inversion")]
    private void TestInversion() => StartInversion();
}