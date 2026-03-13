using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class PauseMenuController : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";

    private static PauseMenuController instance;

    private Canvas rootCanvas;
    private GameObject panel;
    private TextMeshProUGUI livesText;
    private TextMeshProUGUI statusText;
    private Button resumeButton;
    private Button retryButton;
    private Button mainMenuButton;

    private bool isOpen;
    private float timeScaleBeforePause = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureControllerExistsOnLoad()
    {
        var scene = SceneManager.GetActiveScene();
        if (!ShouldEnableInScene(scene.name))
            return;

        if (FindFirstObjectByType<PauseMenuController>() != null)
            return;

        var go = new GameObject("PauseMenuController");
        DontDestroyOnLoad(go);
        go.AddComponent<PauseMenuController>();
    }

    private static bool ShouldEnableInScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return false;

        if (sceneName.Equals("MainMenu"))
            return false;

        return true;
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
        HideMenuImmediate();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;

        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (resumeButton != null) resumeButton.onClick.RemoveListener(OnResumePressed);
        if (retryButton != null) retryButton.onClick.RemoveListener(OnRetryPressed);
        if (mainMenuButton != null) mainMenuButton.onClick.RemoveListener(OnMainMenuPressed);
    }

    private void Update()
    {
        if (!ShouldEnableInScene(SceneManager.GetActiveScene().name))
            return;

        // If another overlay has already paused gameplay, don't open a second pause layer.
        if (!isOpen && Mathf.Approximately(Time.timeScale, 0f))
            return;

        if (IsPlayerDead())
            return;

        if (!WasEscapePressedThisFrame())
            return;

        if (isOpen)
            ResumeGame();
        else
            OpenMenu();
    }

    private bool WasEscapePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    private bool IsPlayerDead()
    {
        var health = FindFirstObjectByType<PlayerHealth>();
        return health != null && health.IsDead();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool enabledHere = ShouldEnableInScene(scene.name);

        if (!enabledHere)
        {
            HideMenuImmediate();
            if (rootCanvas != null)
                rootCanvas.gameObject.SetActive(false);
            return;
        }

        BuildUiIfNeeded();
        if (rootCanvas != null)
            rootCanvas.gameObject.SetActive(true);

        HideMenuImmediate();
    }

    private void BuildUiIfNeeded()
    {
        if (rootCanvas != null)
            return;

        var canvasGo = new GameObject("PauseMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        rootCanvas = canvasGo.GetComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = 2500;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        panel = new GameObject("PausePanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasGo.transform, false);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        var panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.68f);

        var card = new GameObject("PauseCard", typeof(RectTransform), typeof(Image), typeof(Outline));
        card.transform.SetParent(panel.transform, false);
        var cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(600f, 460f);
        var cardImage = card.GetComponent<Image>();
        cardImage.color = new Color(0.20f, 0.11f, 0.03f, 0.95f);
        var cardOutline = card.GetComponent<Outline>();
        cardOutline.effectColor = new Color(0.73f, 0.52f, 0.24f, 0.95f);
        cardOutline.effectDistance = new Vector2(3f, -3f);

        var title = CreateLabel(card.transform, "PAUSED", 44f, new Color(0.90f, 0.72f, 0.35f, 1f));
        var titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -32f);
        titleRect.sizeDelta = new Vector2(520f, 72f);

        livesText = CreateLabel(card.transform, string.Empty, 30f, new Color(0.95f, 0.84f, 0.58f, 1f));
        var livesRect = livesText.rectTransform;
        livesRect.anchorMin = new Vector2(0.5f, 1f);
        livesRect.anchorMax = new Vector2(0.5f, 1f);
        livesRect.pivot = new Vector2(0.5f, 1f);
        livesRect.anchoredPosition = new Vector2(0f, -96f);
        livesRect.sizeDelta = new Vector2(520f, 54f);

        statusText = CreateLabel(card.transform, string.Empty, 22f, new Color(0.95f, 0.62f, 0.45f, 1f));
        var statusRect = statusText.rectTransform;
        statusRect.anchorMin = new Vector2(0.5f, 1f);
        statusRect.anchorMax = new Vector2(0.5f, 1f);
        statusRect.pivot = new Vector2(0.5f, 1f);
        statusRect.anchoredPosition = new Vector2(0f, -146f);
        statusRect.sizeDelta = new Vector2(520f, 44f);

        resumeButton = CreateMenuButton(card.transform, "Resume", new Vector2(0f, -220f));
        retryButton = CreateMenuButton(card.transform, "Retry", new Vector2(0f, -290f));
        mainMenuButton = CreateMenuButton(card.transform, "Main Menu", new Vector2(0f, -360f));

        resumeButton.onClick.AddListener(OnResumePressed);
        retryButton.onClick.AddListener(OnRetryPressed);
        mainMenuButton.onClick.AddListener(OnMainMenuPressed);
    }

    private TextMeshProUGUI CreateLabel(Transform parent, string text, float fontSize, Color color)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    private Button CreateMenuButton(Transform parent, string text, Vector2 anchoredPosition)
    {
        var buttonGo = new GameObject(text + "Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline), typeof(Shadow));
        buttonGo.transform.SetParent(parent, false);

        var rect = buttonGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(360f, 54f);

        var image = buttonGo.GetComponent<Image>();
        image.color = new Color(0.68f, 0.49f, 0.23f, 0.96f);

        var button = buttonGo.GetComponent<Button>();
        var colors = button.colors;
        colors.normalColor = new Color(0.68f, 0.49f, 0.23f, 0.96f);
        colors.highlightedColor = new Color(0.79f, 0.60f, 0.31f, 1f);
        colors.pressedColor = new Color(0.54f, 0.37f, 0.15f, 1f);
        colors.disabledColor = new Color(0.34f, 0.27f, 0.18f, 0.9f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        var outline = buttonGo.GetComponent<Outline>();
        outline.effectColor = new Color(0.14f, 0.08f, 0.02f, 1f);
        outline.effectDistance = new Vector2(2f, -2f);

        var shadow = buttonGo.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
        shadow.effectDistance = new Vector2(1f, -1f);

        var labelGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(buttonGo.transform, false);
        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var label = labelGo.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 28f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.08f, 0.04f, 0f, 1f);
        label.raycastTarget = false;

        return button;
    }

    private void OpenMenu()
    {
        if (panel == null)
            return;

        UpdateLivesUi();
        panel.SetActive(true);
        isOpen = true;

        timeScaleBeforePause = Mathf.Approximately(Time.timeScale, 0f) ? 1f : Time.timeScale;
        Time.timeScale = 0f;
        AudioListener.pause = true;
    }

    private void HideMenuImmediate()
    {
        if (panel != null)
            panel.SetActive(false);

        isOpen = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    private void ResumeGame()
    {
        if (!isOpen)
            return;

        if (panel != null)
            panel.SetActive(false);

        isOpen = false;
        Time.timeScale = Mathf.Max(0.0001f, timeScaleBeforePause);
        AudioListener.pause = false;
    }

    private void UpdateLivesUi()
    {
        int remaining = Player.GetRetryLivesRemaining();
        int max = Player.GetMaxRetryLivesPerRun();

        if (livesText != null)
            livesText.text = $"Lives Remaining: {remaining}/{max}";

        bool canRetry = remaining > 0;
        if (retryButton != null)
            retryButton.interactable = canRetry;

        if (statusText != null)
        {
            statusText.text = canRetry
                ? "Retry consumes 1 life"
                : $"No lives left. Go Main Menu to refresh to {max}";
        }
    }

    private void OnResumePressed()
    {
        ResumeGame();
    }

    private void OnRetryPressed()
    {
        if (Player.GetRetryLivesRemaining() <= 0)
        {
            UpdateLivesUi();
            return;
        }

        Player.ConsumeRetryLife();
        Player.RestoreDropBuffStacksFromStageCheckpoint();
        PlayerHealth.ResetSavedHP();

        isOpen = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void OnMainMenuPressed()
    {
        isOpen = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;

        Player.ResetSavedDropBuffStacks();
        Player.ResetRetryLives();
        PlayerHealth.ResetSavedHP();

        SceneManager.LoadScene(MainMenuSceneName);
    }
}
