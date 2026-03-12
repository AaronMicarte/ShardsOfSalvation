using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;

[DisallowMultipleComponent]
public class MainMenuController : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainMenuPanel;
    public GameObject instructionsPanel;

    [Header("UI")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI subtitleText;
    public TextMeshProUGUI instructionsText;
    public Button mainPlayButton;
    public Button instructionsPlayButton;
    public Button instructionsBackButton;
    public Button quitButton;

    [Header("Audio")]
    public AudioClip menuBgm;
    public AudioClip buttonClickSfx;
    public AudioSource audioSource; // optional: assign an AudioSource to play BGM/SFX

    [Header("Behavior")]
    [Tooltip("Scene name to load when user presses Start")] public string sceneToLoad = "IntroScene";
    [Tooltip("Fade duration for panels")] public float panelFadeDuration = 0.35f;
    [Tooltip("If true, reset any saved player HP when starting a new game from this menu")]
    public bool resetPlayerHPOnStart = true;

    // internal
    CanvasGroup mainMenuCanvasGroup;
    CanvasGroup instructionsCanvasGroup;

    void OnEnable()
    {
        BindButtonListeners();
    }

    void OnDisable()
    {
        UnbindButtonListeners();
    }

    void Awake()
    {
        if (mainMenuPanel != null)
        {
            mainMenuCanvasGroup = mainMenuPanel.GetComponent<CanvasGroup>();
            if (mainMenuCanvasGroup == null) mainMenuCanvasGroup = mainMenuPanel.AddComponent<CanvasGroup>();
        }

        if (instructionsPanel != null)
        {
            instructionsCanvasGroup = instructionsPanel.GetComponent<CanvasGroup>();
            if (instructionsCanvasGroup == null) instructionsCanvasGroup = instructionsPanel.AddComponent<CanvasGroup>();
            instructionsPanel.SetActive(false);
            instructionsCanvasGroup.alpha = 0f;
            instructionsCanvasGroup.interactable = false;
            instructionsCanvasGroup.blocksRaycasts = false;
        }

        // fallback audio source
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null && (menuBgm != null || buttonClickSfx != null))
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
        }

        // default instructions text (if not assigned)
        if (instructionsText != null && string.IsNullOrWhiteSpace(instructionsText.text))
        {
            instructionsText.text = "Controls:\n\n- Move: Arrow keys / A D\n- Jump: Space\n- Attack: J / Left Click\n- Dash: K / Right Click\n- Interact: E\n\nTips:\n- Combine dash with attack for special moves.\n- Find pickups to upgrade your abilities.\n\nGood luck and have fun!";
        }
    }

    void Start()
    {
        // Play BGM if assigned
        if (menuBgm != null && audioSource != null)
        {
            audioSource.clip = menuBgm;
            audioSource.loop = true;
            audioSource.Play();
        }
    }

    void BindButtonListeners()
    {
        // These bindings are optional and only applied when button refs are assigned in Inspector.
        if (mainPlayButton != null) mainPlayButton.onClick.AddListener(OnPlayPressed);
        if (instructionsPlayButton != null) instructionsPlayButton.onClick.AddListener(OnStartPressed);
        if (instructionsBackButton != null) instructionsBackButton.onClick.AddListener(OnBackPressed);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitPressed);
    }

    void UnbindButtonListeners()
    {
        if (mainPlayButton != null) mainPlayButton.onClick.RemoveListener(OnPlayPressed);
        if (instructionsPlayButton != null) instructionsPlayButton.onClick.RemoveListener(OnStartPressed);
        if (instructionsBackButton != null) instructionsBackButton.onClick.RemoveListener(OnBackPressed);
        if (quitButton != null) quitButton.onClick.RemoveListener(OnQuitPressed);
    }

    public void OnPlayPressed()
    {
        // show instructions panel with fade
        if (mainMenuPanel != null) StartCoroutine(FadeOut(mainMenuCanvasGroup));
        if (instructionsPanel != null)
        {
            instructionsPanel.SetActive(true);
            StartCoroutine(FadeIn(instructionsCanvasGroup));
        }
        PlayClickSfx();
    }

    public void OnBackPressed()
    {
        // hide instructions, show main menu
        if (instructionsPanel != null) StartCoroutine(HideInstructions());
        PlayClickSfx();
    }

    public void OnStartPressed()
    {
        PlayClickSfx();
        // optional small delay for sound to play
        // Optionally reset persisted player HP so this starts a fresh run
        if (resetPlayerHPOnStart) PlayerHealth.ResetSavedHP();
        Player.ResetSavedDropBuffStacks();
        StartCoroutine(LoadSceneAfterDelay(0.12f));
    }

    public void OnQuitPressed()
    {
        PlayClickSfx();
#if UNITY_EDITOR
        // stop Play Mode in the Editor for easier testing
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    IEnumerator LoadSceneAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!string.IsNullOrEmpty(sceneToLoad)) SceneManager.LoadScene(sceneToLoad);
    }

    IEnumerator HideInstructions()
    {
        yield return FadeOut(instructionsCanvasGroup);
        instructionsPanel.SetActive(false);
        yield return FadeIn(mainMenuCanvasGroup);
    }

    IEnumerator FadeIn(CanvasGroup cg)
    {
        if (cg == null) yield break;
        cg.interactable = true;
        cg.blocksRaycasts = true;
        float t = 0f;
        while (t < panelFadeDuration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Clamp01(t / panelFadeDuration);
            yield return null;
        }
        cg.alpha = 1f;
    }

    IEnumerator FadeOut(CanvasGroup cg)
    {
        if (cg == null) yield break;
        cg.interactable = false;
        cg.blocksRaycasts = false;
        float start = cg.alpha;
        float t = 0f;
        while (t < panelFadeDuration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Clamp01(1f - (t / panelFadeDuration));
            yield return null;
        }
        cg.alpha = 0f;
    }

    void PlayClickSfx()
    {
        if (buttonClickSfx == null) return;
        if (audioSource != null) audioSource.PlayOneShot(buttonClickSfx, 1f);
        else AudioSource.PlayClipAtPoint(buttonClickSfx, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
    }
}
