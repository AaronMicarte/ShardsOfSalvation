using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Smoothly varies a Rigidbody2D's gravityScale over time to create "gravity distortion" effects.
/// Designed to be started/stopped by a Floor manager or level event (e.g. when entering floor 2).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class GravityDistortion : MonoBehaviour
{
    [Header("Range")]
    [SerializeField, Tooltip("Minimum gravity scale (floaty)")] private float minGravity = 0.6f;
    [SerializeField, Tooltip("Maximum gravity scale (heavy)")] private float maxGravity = 1.8f;

    [Header("Timing")]
    [SerializeField, Tooltip("Hold time between changes (randomized between minHold and maxHold)")] private float minHold = 2f;
    [SerializeField] private float maxHold = 4f;
    [SerializeField, Tooltip("How long it takes to transition to new gravity (seconds)")] private float transitionTime = 0.5f;

    [Header("Behavior")]
    [SerializeField, Tooltip("If true, distortion starts automatically when this component is enabled")] private bool startOnEnable = false;

    [Header("Feedback")]
    [SerializeField, Tooltip("Optional message to display / log when distortion occurs")] private string distortionMessage = "Dash Disabled!";
    [SerializeField, Tooltip("Optional TextMeshProUGUI used to display the message on screen (leave empty to disable)")] private TextMeshProUGUI messageText = null;
    [SerializeField, Tooltip("How long the message is visible (seconds)")] private float messageDuration = 2f;
    [SerializeField, Tooltip("Fade out duration at the end of the message (seconds)")] private float messageFade = 0.4f;
    [SerializeField, Tooltip("Show message automatically when distortion starts")] private bool showMessageOnStart = true;
    public UnityEvent onDistortionStart;
    public UnityEvent onDistortionEnd;
    public UnityEvent onGravityChanged; // invoked every time gravity value is updated (useful to hook UI or SFX)

    private Rigidbody2D rb;
    private float defaultGravity;
    private Coroutine loopCoroutine;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        defaultGravity = rb != null ? rb.gravityScale : 1f;
    }

    void OnEnable()
    {
        if (startOnEnable)
            StartDistortion();
    }

    void OnDisable()
    {
        StopDistortion();
    }

    /// <summary>
    /// Begin the gravity distortion loop.
    /// </summary>
    public void StartDistortion()
    {
        if (rb == null) return;
        if (loopCoroutine != null) StopCoroutine(loopCoroutine);
        loopCoroutine = StartCoroutine(DistortionLoop());
        onDistortionStart?.Invoke();
        if (showMessageOnStart && !string.IsNullOrEmpty(distortionMessage))
            ShowMessage(distortionMessage, messageDuration);
    }

    /// <summary>
    /// Stop the distortion and smoothly restore normal gravity.
    /// </summary>
    public void StopDistortion()
    {
        if (loopCoroutine != null) StopCoroutine(loopCoroutine);
        loopCoroutine = null;

        // If this component is active we can safely start the smooth restore coroutine.
        // If it's inactive (e.g. GameObject has been disabled) StartCoroutine will fail —
        // instead restore gravity immediately to avoid errors.
        if (this.isActiveAndEnabled)
        {
            StartCoroutine(RestoreGravity());
        }
        else
        {
            if (rb != null)
            {
                rb.gravityScale = defaultGravity;
                onGravityChanged?.Invoke();
            }
        }

        onDistortionEnd?.Invoke();
    }

    private IEnumerator DistortionLoop()
    {
        while (true)
        {
            float target = Random.Range(minGravity, maxGravity);

            // Smoothly transition to target
            float start = rb.gravityScale;
            float t = 0f;
            while (t < transitionTime)
            {
                t += Time.deltaTime;
                float p = Mathf.SmoothStep(0f, 1f, t / Mathf.Max(transitionTime, 0.0001f));
                rb.gravityScale = Mathf.Lerp(start, target, p);
                onGravityChanged?.Invoke();
                yield return null;
            }
            rb.gravityScale = target;
            onGravityChanged?.Invoke();



            // Hold for a randomized period
            float hold = Random.Range(minHold, maxHold);
            yield return new WaitForSeconds(hold);
        }
    }

    private IEnumerator RestoreGravity()
    {
        float start = rb.gravityScale;
        float t = 0f;
        float dur = Mathf.Max(transitionTime, 0.1f);
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / dur);
            rb.gravityScale = Mathf.Lerp(start, defaultGravity, p);
            onGravityChanged?.Invoke();
            yield return null;
        }
        rb.gravityScale = defaultGravity;
        onGravityChanged?.Invoke();
    }

    private Coroutine messageCoroutine;

    /// <summary>
    /// Display a brief message using the optional TextMeshProUGUI assigned to this component.
    /// Safe to call even if no TMP reference is assigned.
    /// </summary>
    public void ShowMessage(string message, float duration = -1f)
    {
        EnsureMessageText();
        if (messageText == null)
        {
            Debug.LogWarning($"GravityDistortion: No message Text assigned and none found in scene. Message '{message}' skipped.");
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

            if (n.Contains("message") || n.Contains("notice") || n.Contains("warning") || n.Contains("alert") || n.Contains("gravity"))
            {
                messageText = t;
                Debug.Log($"GravityDistortion: auto-assigned messageText -> {t.name}");
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

    [ContextMenu("Test Show Message")]
    private void TestShowMessage()
    {
        ShowMessage(distortionMessage, messageDuration);
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

    // Helper for debugging in editor
    void OnValidate()
    {
        if (minGravity < 0f) minGravity = 0f;
        if (maxGravity < minGravity) maxGravity = minGravity;
        if (maxHold < minHold) maxHold = minHold;
        if (transitionTime < 0f) transitionTime = 0f;
    }
}
