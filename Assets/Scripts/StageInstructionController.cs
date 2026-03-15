using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class StageInstructionController : MonoBehaviour
{
    private static StageInstructionController instance;

    private Canvas rootCanvas;
    private GameObject topBanner;
    private CanvasGroup topBannerCanvasGroup;
    private TextMeshProUGUI topBannerText;
    private GameObject floor1Hint;
    private TextMeshProUGUI floor1HintText;

    private GameObject stage4Panel;
    private Button stage4ContinueButton;

    private bool stage4PopupShownThisScene;
    private float stage4PopupStartTime;
    private float pauseScaleBeforePopup = 1f;
    private float topBannerShownAt = -Mathf.Infinity;
    private float topBannerVisibleDuration = 6f;

    [SerializeField] private float topBannerHeight = 56f;
    [SerializeField] private float stage4TriggerDistance = 14f;
    [SerializeField] private float topBannerFadeDuration = 0.6f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureControllerExistsOnLoad()
    {
        if (FindFirstObjectByType<StageInstructionController>() != null)
            return;

        var go = new GameObject("StageInstructionController");
        DontDestroyOnLoad(go);
        go.AddComponent<StageInstructionController>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;

        BuildUiIfNeeded();
        ApplyForScene(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;

        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (stage4ContinueButton != null)
            stage4ContinueButton.onClick.RemoveListener(CloseStage4Popup);
    }

    private void Update()
    {
        UpdateTopBannerFade();

        string sceneName = SceneManager.GetActiveScene().name;
        if (!sceneName.Equals("Floor4"))
            return;

        if (stage4PopupShownThisScene)
            return;

        if (!ShouldShowStage4PopupNow())
            return;

        ShowStage4Popup();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyForScene(scene.name);
    }

    private void ApplyForScene(string sceneName)
    {
        BuildUiIfNeeded();

        bool isFloor = sceneName.StartsWith("Floor");
        bool isFloor1 = sceneName.Equals("Floor1");
        if (rootCanvas != null)
            rootCanvas.gameObject.SetActive(isFloor);

        if (!isFloor)
        {
            HideStage4PopupImmediate();
            return;
        }

        if (topBannerText != null)
            topBannerText.text = GetTopInstructionForScene(sceneName);

        topBannerVisibleDuration = GetTopBannerDurationForScene(sceneName);
        topBannerShownAt = Time.unscaledTime;
        if (topBanner != null)
            topBanner.SetActive(true);
        if (topBannerCanvasGroup != null)
            topBannerCanvasGroup.alpha = 1f;

        if (floor1Hint != null)
            floor1Hint.SetActive(isFloor1);
        if (isFloor1 && floor1HintText != null)
            floor1HintText.text = "Tip: Press ESC anytime to open menu and view controls.";

        stage4PopupShownThisScene = false;
        stage4PopupStartTime = Time.unscaledTime;
        HideStage4PopupImmediate();
    }

    private string GetTopInstructionForScene(string sceneName)
    {
        if (sceneName.Equals("Floor1"))
            return "Floor 1 Objective: Defeat enemies, collect the shard, then enter the portal. Double tap A or D to dash.";
        if (sceneName.Equals("Floor2"))
            return "Floor 2 Hazard: Vision is reduced. Stay close and track enemy movement carefully.";
        if (sceneName.Equals("Floor3"))
            return "Floor 3 Hazard: Left and right controls are inverted. Re-adjust before engaging.";
        if (sceneName.Equals("Floor4"))
            return "Floor 4 Warning: Boss skills apply debuffs and burst damage. Watch for the briefing popup.";
        if (sceneName.Equals("Floor5"))
            return "Floor 5 Finale: Defeat the final boss and claim the last shard.";

        return "Objective: Defeat enemies, collect shard, and advance.";
    }

    private float GetTopBannerDurationForScene(string sceneName)
    {
        return 4.6f;
    }

    private void UpdateTopBannerFade()
    {
        if (topBanner == null || topBannerCanvasGroup == null || !topBanner.activeSelf)
            return;

        float elapsed = Time.unscaledTime - topBannerShownAt;
        if (elapsed < topBannerVisibleDuration)
        {
            topBannerCanvasGroup.alpha = 1f;
            return;
        }

        float fade = Mathf.Max(0.01f, topBannerFadeDuration);
        float fadeT = (elapsed - topBannerVisibleDuration) / fade;
        if (fadeT >= 1f)
        {
            topBannerCanvasGroup.alpha = 0f;
            topBanner.SetActive(false);
            return;
        }

        topBannerCanvasGroup.alpha = Mathf.Lerp(1f, 0f, fadeT);
    }

    private bool ShouldShowStage4PopupNow()
    {
        if (stage4Panel == null)
            return false;

        var bossAttack = FindFirstObjectByType<Stage4BossAttack>();
        if (bossAttack == null)
            return false;

        var health = FindFirstObjectByType<PlayerHealth>();
        if (health != null && health.IsDead())
            return false;

        var player = FindFirstObjectByType<Player>();
        if (player == null)
        {
            // Fallback timeout if player reference is temporarily unavailable.
            return Time.unscaledTime - stage4PopupStartTime > 3f;
        }

        float distance = Vector2.Distance(player.transform.position, bossAttack.transform.position);
        return distance <= Mathf.Max(2f, stage4TriggerDistance);
    }

    private void BuildUiIfNeeded()
    {
        if (rootCanvas != null)
            return;

        var canvasGo = new GameObject("StageInstructionCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        rootCanvas = canvasGo.GetComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = 2400;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        BuildTopBanner(canvasGo.transform);
        BuildFloor1Hint(canvasGo.transform);
        BuildStage4Popup(canvasGo.transform);
    }

    private void BuildTopBanner(Transform parent)
    {
        topBanner = new GameObject("TopInstructionBanner", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(CanvasGroup));
        topBanner.transform.SetParent(parent, false);

        var rect = topBanner.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(0f, -topBannerHeight);
        rect.offsetMax = new Vector2(0f, 0f);

        var bg = topBanner.GetComponent<Image>();
        bg.color = new Color(0.20f, 0.11f, 0.03f, 0.72f);

        var outline = topBanner.GetComponent<Outline>();
        outline.effectColor = new Color(0.73f, 0.52f, 0.24f, 0.92f);
        outline.effectDistance = new Vector2(1f, -1f);

        topBannerCanvasGroup = topBanner.GetComponent<CanvasGroup>();
        topBannerCanvasGroup.alpha = 1f;

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(topBanner.transform, false);
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 6f);
        textRect.offsetMax = new Vector2(-16f, -6f);

        topBannerText = textGo.GetComponent<TextMeshProUGUI>();
        topBannerText.fontSize = 31f;
        topBannerText.alignment = TextAlignmentOptions.Center;
        topBannerText.color = new Color(0.95f, 0.84f, 0.58f, 1f);
        topBannerText.textWrappingMode = TextWrappingModes.NoWrap;
        topBannerText.raycastTarget = false;
    }

    private void BuildFloor1Hint(Transform parent)
    {
        floor1Hint = new GameObject("Floor1EscHint", typeof(RectTransform), typeof(Image), typeof(Outline));
        floor1Hint.transform.SetParent(parent, false);

        var rect = floor1Hint.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(18f, -74f);
        rect.sizeDelta = new Vector2(760f, 50f);

        var bg = floor1Hint.GetComponent<Image>();
        bg.color = new Color(0.18f, 0.09f, 0.02f, 0.72f);

        var outline = floor1Hint.GetComponent<Outline>();
        outline.effectColor = new Color(0.95f, 0.84f, 0.58f, 0.95f);
        outline.effectDistance = new Vector2(2f, -2f);

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(floor1Hint.transform, false);

        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 4f);
        textRect.offsetMax = new Vector2(-12f, -4f);

        floor1HintText = textGo.GetComponent<TextMeshProUGUI>();
        floor1HintText.fontSize = 22f;
        floor1HintText.color = new Color(0.95f, 0.84f, 0.58f, 1f);
        floor1HintText.alignment = TextAlignmentOptions.Left;
        floor1HintText.raycastTarget = false;
        floor1HintText.overflowMode = TextOverflowModes.Overflow;
        floor1HintText.textWrappingMode = TextWrappingModes.NoWrap;

        floor1Hint.SetActive(false);
    }

    private void BuildStage4Popup(Transform parent)
    {
        stage4Panel = new GameObject("Stage4SkillPanel", typeof(RectTransform), typeof(Image));
        stage4Panel.transform.SetParent(parent, false);

        var panelRect = stage4Panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var panelBg = stage4Panel.GetComponent<Image>();
        panelBg.color = new Color(0f, 0f, 0f, 0.72f);

        var card = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(Shadow));
        card.transform.SetParent(stage4Panel.transform, false);
        var cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(860f, 520f);

        var cardBg = card.GetComponent<Image>();
        cardBg.color = new Color(0.20f, 0.11f, 0.03f, 0.96f);

        var cardOutline = card.GetComponent<Outline>();
        cardOutline.effectColor = new Color(0.73f, 0.52f, 0.24f, 0.95f);
        cardOutline.effectDistance = new Vector2(3f, -3f);

        var cardShadow = card.GetComponent<Shadow>();
        cardShadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
        cardShadow.effectDistance = new Vector2(2f, -2f);

        var title = CreateText(card.transform, "STAGE 4 ENEMY WARNING", 44f, new Color(0.95f, 0.84f, 0.58f, 1f));
        var titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -28f);
        titleRect.sizeDelta = new Vector2(780f, 70f);

        var body = CreateText(card.transform,
            "Skill 1: Venom burst applies slow + blind debuff.\n" +
            "Your attacks can miss while debuffed.\n\n" +
            "Skill 2: Serpent strike deals direct damage.\n" +
            "Keep distance, dodge timing windows, and punish cooldowns.",
            30f,
            new Color(0.93f, 0.74f, 0.45f, 1f));
        var bodyRect = body.rectTransform;
        bodyRect.anchorMin = new Vector2(0.5f, 1f);
        bodyRect.anchorMax = new Vector2(0.5f, 1f);
        bodyRect.pivot = new Vector2(0.5f, 1f);
        bodyRect.anchoredPosition = new Vector2(0f, -120f);
        bodyRect.sizeDelta = new Vector2(760f, 260f);
        body.alignment = TextAlignmentOptions.TopLeft;
        body.textWrappingMode = TextWrappingModes.Normal;

        stage4ContinueButton = CreateGoldButton(card.transform, "Understood", new Vector2(0f, -430f));
        stage4ContinueButton.onClick.AddListener(CloseStage4Popup);

        stage4Panel.SetActive(false);
    }

    private TextMeshProUGUI CreateText(Transform parent, string text, float size, Color color)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    private Button CreateGoldButton(Transform parent, string text, Vector2 anchoredPos)
    {
        var go = new GameObject(text + "Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline), typeof(Shadow));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = new Vector2(320f, 58f);

        var image = go.GetComponent<Image>();
        image.color = new Color(0.68f, 0.49f, 0.23f, 0.96f);

        var outline = go.GetComponent<Outline>();
        outline.effectColor = new Color(0.14f, 0.08f, 0.02f, 1f);
        outline.effectDistance = new Vector2(2f, -2f);

        var shadow = go.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
        shadow.effectDistance = new Vector2(1f, -1f);

        var btn = go.GetComponent<Button>();

        var label = CreateText(go.transform, text, 28f, new Color(0.08f, 0.04f, 0f, 1f));
        var labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        return btn;
    }

    private void ShowStage4Popup()
    {
        if (stage4Panel == null)
            return;

        stage4PopupShownThisScene = true;
        pauseScaleBeforePopup = Mathf.Approximately(Time.timeScale, 0f) ? 1f : Time.timeScale;
        Time.timeScale = 0f;
        AudioListener.pause = true;

        stage4Panel.SetActive(true);
    }

    private void CloseStage4Popup()
    {
        if (stage4Panel != null)
            stage4Panel.SetActive(false);

        Time.timeScale = Mathf.Max(0.0001f, pauseScaleBeforePopup);
        AudioListener.pause = false;
    }

    private void HideStage4PopupImmediate()
    {
        if (stage4Panel != null)
            stage4Panel.SetActive(false);

        AudioListener.pause = false;
        Time.timeScale = 1f;
    }
}
