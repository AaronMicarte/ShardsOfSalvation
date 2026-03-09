using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

public class IntroDialogueManager : MonoBehaviour
{
    public Image backgroundImage;
    public Image portraitImage;
    public TextMeshProUGUI dialogueText;

    public DialogueLine[] lines;

    [Header("Typewriter")]
    [Tooltip("Seconds per character (smaller = faster)")]
    public float typingDelay = 0.03f;
    [Tooltip("Sound to play per character (optional)")]
    public AudioClip typingSfx;
    [Tooltip("Volume for typing SFX")]
    [Range(0f, 1f)] public float typingSfxVolume = 1f;
    [Tooltip("Minimum seconds between typing SFX plays to avoid loud stacking")]
    public float typingSfxMinInterval = 0.02f;
    [Tooltip("Play SFX every N visible characters (1 = every char, 2 = every 2 chars)")]
    public int typingSfxEveryNChars = 2;
    [Tooltip("Optional AudioSource to play typing SFX (if not assigned, uses PlayClipAtPoint)")]
    public AudioSource typingAudioSource;
    [Tooltip("If true, play the typing SFX as a loop while typing (requires an AudioSource or will create one).")]
    public bool typingSfxLoop = false;
    [Tooltip("Optional sound to play once when a line finishes typing")]
    public AudioClip typingEndSfx;
    [Tooltip("Volume for typing end SFX")]
    [Range(0f, 1f)] public float typingEndSfxVolume = 1f;
    [Header("Scene Flow")]
    [Tooltip("Scene to load after dialogue ends. If left empty, IntroScene -> Floor1 and FinalScene -> MainMenu.")]
    public string sceneToLoadOnComplete = "Floor1";

    private int index = 0;
    private Coroutine typingCoroutine = null;
    private float lastTypingSfxTime = -Mathf.Infinity;

    // runtime audio source created for looping if needed (we create a dedicated source so we never stop shared audio like BGM)
    private AudioSource runtimeLoopAudioSource = null;
    private bool runtimeLoopSourceCreated = false;

    void OnValidate()
    {
        if (typingDelay < 0f) typingDelay = 0f;
        if (typingSfxMinInterval < 0f) typingSfxMinInterval = 0f;
        if (typingSfxEveryNChars < 1) typingSfxEveryNChars = 1;
        typingSfxVolume = Mathf.Clamp01(typingSfxVolume);
    }

    void Start()
    {
        EnsureDefaultFinalSceneLines();

        // always send the final cutscene back to the main menu
        if (SceneManager.GetActiveScene().name == "FinalScene")
        {
            sceneToLoadOnComplete = "MainMenu";
        }

        // Smart default for a copied intro scene used as FinalScene.
        if (string.IsNullOrWhiteSpace(sceneToLoadOnComplete))
            sceneToLoadOnComplete = GetDefaultCompleteScene();

        if (lines == null || lines.Length == 0)
        {
            if (dialogueText != null) dialogueText.text = "";
            return;
        }
        index = 0;
        ShowLine();
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            OnAdvancePressed();
        }
#else
        if (Input.GetKeyDown(KeyCode.Space))
        {
            OnAdvancePressed();
        }
#endif
    }

    void ShowLine()
    {
        if (lines == null || lines.Length == 0 || index < 0 || index >= lines.Length)
        {
            if (dialogueText != null) dialogueText.text = "";
            return;
        }

        if (dialogueText != null)
        {
            // Assign the full text first then reveal via maxVisibleCharacters (TMP-safe)
            dialogueText.text = lines[index].text;
            dialogueText.ForceMeshUpdate();
            dialogueText.maxVisibleCharacters = 0;

            // Start typewriter routine
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            typingCoroutine = StartCoroutine(TypeTextRoutine());
        }

        if (backgroundImage != null && lines[index].background != null)
            backgroundImage.sprite = lines[index].background;

        if (portraitImage != null && lines[index].portrait != null)
            portraitImage.sprite = lines[index].portrait;
    }

    private IEnumerator TypeTextRoutine()
    {
        if (dialogueText == null) { typingCoroutine = null; yield break; }

        dialogueText.ForceMeshUpdate();
        int total = dialogueText.textInfo.characterCount;
        if (total == 0) { typingCoroutine = null; yield break; }

        // If loop mode is enabled, create a dedicated looping AudioSource so we don't interfere with any shared audio (e.g., BGM)
        if (typingSfxLoop && typingSfx != null)
        {
            runtimeLoopAudioSource = gameObject.AddComponent<AudioSource>();
            runtimeLoopSourceCreated = true;
            runtimeLoopAudioSource.clip = typingSfx;
            runtimeLoopAudioSource.loop = true;
            runtimeLoopAudioSource.playOnAwake = false;
            runtimeLoopAudioSource.volume = typingSfxVolume;
            runtimeLoopAudioSource.spatialBlend = 0f;
            runtimeLoopAudioSource.Play();
        }

        for (int i = 1; i <= total; i++)
        {
            dialogueText.maxVisibleCharacters = i;

            // Only play per-character clicks when NOT using loop mode; still respect throttling and "every N chars" setting
            if (!typingSfxLoop && typingSfx != null && (i % typingSfxEveryNChars == 0) && Time.time >= lastTypingSfxTime + typingSfxMinInterval)
            {
                lastTypingSfxTime = Time.time;
                if (typingAudioSource != null)
                    typingAudioSource.PlayOneShot(typingSfx, typingSfxVolume);
                else
                {
                    var camPos = Camera.main != null ? Camera.main.transform.position : transform.position;
                    AudioSource.PlayClipAtPoint(typingSfx, camPos, typingSfxVolume);
                }
            }

            yield return new WaitForSeconds(typingDelay);
        }

        // Typing finished: stop loop (if any) and optionally play a finished sound
        StopTypingLoopAndPlayEndSfx();
        typingCoroutine = null;
    }

    private void FinishTypingImmediate()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }
        if (dialogueText != null)
        {
            dialogueText.ForceMeshUpdate();
            dialogueText.maxVisibleCharacters = dialogueText.textInfo.characterCount;
        }

        // stop any typing loop and play the optional end SFX so audio matches the instant finish
        StopTypingLoopAndPlayEndSfx();
    }

    private void OnAdvancePressed()
    {
        // If typing is in progress, finish it; otherwise advance to next line
        if (typingCoroutine != null)
        {
            FinishTypingImmediate();
            return;
        }
        NextLine();
    }

    private void StopTypingLoopAndPlayEndSfx()
    {
        // stop & destroy runtime loop if we created it
        if (runtimeLoopAudioSource != null)
        {
            if (runtimeLoopAudioSource.isPlaying) runtimeLoopAudioSource.Stop();
            if (runtimeLoopSourceCreated) Destroy(runtimeLoopAudioSource);
            runtimeLoopAudioSource = null;
            runtimeLoopSourceCreated = false;
        }

        // play an end/done sound once if assigned
        if (typingEndSfx != null)
        {
            if (typingAudioSource != null)
                typingAudioSource.PlayOneShot(typingEndSfx, typingEndSfxVolume);
            else
            {
                var camPos = Camera.main != null ? Camera.main.transform.position : transform.position;
                AudioSource.PlayClipAtPoint(typingEndSfx, camPos, typingEndSfxVolume);
            }
        }
    }

    void OnDisable()
    {
        // make sure any runtime audio sources are stopped and cleaned up when the object is disabled
        StopTypingLoopAndPlayEndSfx();
    }

    void NextLine()
    {
        index++;

        if (lines == null || lines.Length == 0 || index >= lines.Length)
        {
            SceneManager.LoadScene(sceneToLoadOnComplete);
            return;
        }

        ShowLine();
    }

    private string GetDefaultCompleteScene()
    {
        string current = SceneManager.GetActiveScene().name;
        if (current == "FinalScene") return "MainMenu";
        return "Floor1";
    }

    private void EnsureDefaultFinalSceneLines()
    {
        if (SceneManager.GetActiveScene().name != "FinalScene") return;
        if (lines != null && lines.Length > 0) return;

        // Auto-story for FinalScene so the ending always has context even before art assets are assigned.
        lines = new DialogueLine[]
        {
            new DialogueLine { text = "So, all five shards are finally together. Cagayan de Oro actually feels... calm now? Wild." },
            new DialogueLine { text = "Streets quiet again, no wreck cars, no shadowy figures. Just jeepneys honking and people going about their day." },
            new DialogueLine { text = "That ninja power? It’s fading out. Funny, I can feel my heartbeat without it now." },
            new DialogueLine { text = "My uniforms are back again, my hair is finally back, I'm me again. A regular guy again." },
            new DialogueLine { text = "Tomorrow it's programming at the school, not monster hunting. Manok ni bobords, again." },
            new DialogueLine { text = "Quest complete. Time for real life. S#*T." }
        };
    }
}
