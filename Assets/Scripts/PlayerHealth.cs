using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    public int maxHP = 8;
    public int startHP = 0;

    [Header("Invulnerability")]
    public float invulnerabilityDuration = 0.25f;

    [Header("References")]
    public HPBarSprite hpBar;

    [Header("Events")]
    public UnityEvent onHit;
    public UnityEvent onDeath;
    [Header("Audio")]
    [Tooltip("Optional SFX to play when the player takes damage")]
    public AudioClip hurtClip;
    [Tooltip("Hurt SFX volume"), Range(0f, 1f)]
    public float hurtVolume = 1f;
    [Header("Death")]
    [Tooltip("Animator on the player (optional). If assigned, the trigger in Death Trigger will be used when the player dies.")]
    public Animator animator;
    [Tooltip("Animator trigger parameter to call on death (default 'Dead')")]
    public string deathTrigger = "Dead";
    [Tooltip("Animator state name to wait for during death (optional)")]
    public string deathStateName = "dead";
    [Tooltip("If true, wait for the death animation to finish before doing death cleanup (spawn VFX, disable colliders etc.)")]
    public bool waitForDeathAnimation = true;
    [Tooltip("Components to disable when the player dies (movement scripts, input handlers etc.)")]
    public MonoBehaviour[] disableOnDeath;
    [Tooltip("If true, make the corpse pass-through (turn colliders into triggers or disable them)")]
    public bool makeCorpsePassThrough = true;
    [Tooltip("If true, set colliders to isTrigger; if false, disable colliders entirely")]
    public bool setTriggerOnDeath = true;
    [Tooltip("Optional VFX to spawn when player dies")] public GameObject deathVFX;
    [Tooltip("Optional SFX to play on death")] public AudioClip deathClip;
    [Tooltip("Death SFX volume"), Range(0f, 1f)] public float deathVolume = 1f;
    [Tooltip("Optional Death UI Controller (if not assigned script will try to Find one in scene)")]
    public DeathUIController deathUI;
    private int currentHP;
    private float lastHurtTime = -Mathf.Infinity;
    private bool isDead = false;

    // Cache original physics state so we can restore after death cleanup.
    private Collider2D[] cachedColliders;
    private bool[] cachedColliderIsTrigger;
    private bool[] cachedColliderEnabled;
    private Rigidbody2D[] cachedRigidbodies;
    private bool[] cachedRigidbodySimulated;

    private const string PrefsKey = "Player_CurrentHP"; // persistent key for current HP across scenes

    // When set, player ignores damage (used for dash invulnerability)
    private bool dashInvulnerable = false;

    // runtime helper to detect whether we loaded HP from persistent storage
    private bool loadedFromPrefs = false;

    void Awake()
    {
        if (PlayerPrefs.HasKey(PrefsKey))
        {
            currentHP = Mathf.Clamp(PlayerPrefs.GetInt(PrefsKey), 0, maxHP);
            loadedFromPrefs = true;
        }

        CachePhysicsState();
    }

    /// <summary>
    /// Save the current HP to PlayerPrefs so it persists across scene loads (stages).
    /// </summary>
    private void SaveCurrentHP()
    {
        PlayerPrefs.SetInt(PrefsKey, currentHP);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Remove any saved HP (used when starting a new game from menu).
    /// </summary>
    public static void ResetSavedHP()
    {
        PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.Save();
    }

    public void SetDashInvulnerable(bool v)
    {
        dashInvulnerable = v;
        // Optionally grant a small invulnerability window to avoid immediate re-hits
        if (v) lastHurtTime = Time.time;
    }

    /// <summary>
    /// Apply damage as a percentage of the player's max HP (0..1).
    /// </summary>
    public void TakePercentDamage(float percent, bool isCritical = false)
    {
        percent = Mathf.Clamp01(percent);
        int amount = Mathf.CeilToInt(percent * maxHP);
        if (isCritical) Debug.Log($"Player took CRITICAL {amount} damage ({percent * 100f}% of max).");
        TakeDamage(amount);
    }

    public int enemyHits = 0; // track enemy attacks

    void Reset()
    {
#if UNITY_2023_2_OR_NEWER
        hpBar = FindFirstObjectByType<HPBarSprite>();
#else
        hpBar = FindObjectOfType<HPBarSprite>();
#endif
    }

    void Start()
    {
        // initialize base HP if not loaded from PlayerPrefs
        if (!loadedFromPrefs)
        {
            currentHP = (startHP <= 0) ? maxHP : Mathf.Clamp(startHP, 0, maxHP);
            SaveCurrentHP();
        }
        else if (currentHP <= 0)
        {
            // If we loaded a dead state (e.g., after stopping Play Mode), reset to a safe start HP.
            currentHP = (startHP <= 0) ? maxHP : Mathf.Clamp(startHP, 0, maxHP);
            SaveCurrentHP();
        }

        RestorePhysicsState();
        ReEnableDeathDisabledComponents();

        if (hpBar != null)
        {
            hpBar.maxHP = maxHP;
            hpBar.SetHP(currentHP);
        }
    }

    public void TakeDamage(int damage, bool ignoreDashInvulnerable = false)
    {
        if (damage <= 0 || isDead) return;
        // Respect dash invulnerability unless caller explicitly requests to ignore it
        if (dashInvulnerable && !ignoreDashInvulnerable) { Debug.Log("Player dash invulnerable: damage ignored."); return; }
        if (Time.time < lastHurtTime + invulnerabilityDuration) return;

        lastHurtTime = Time.time;
        currentHP = Mathf.Clamp(currentHP - damage, 0, maxHP);
        enemyHits++; // count attack

        onHit?.Invoke();
        if (hurtClip != null) AudioSource.PlayClipAtPoint(hurtClip, transform.position, hurtVolume);

        if (hpBar != null) hpBar.SetHP(currentHP);
        SaveCurrentHP();

        if (currentHP <= 0) Die();
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || isDead) return;
        currentHP = Mathf.Clamp(currentHP + amount, 0, maxHP);
        if (hpBar != null) hpBar.SetHP(currentHP);
        SaveCurrentHP();
    }

    public void SetHP(int hp)
    {
        currentHP = Mathf.Clamp(hp, 0, maxHP);
        if (hpBar != null) hpBar.SetHP(currentHP);
        SaveCurrentHP();
        if (currentHP <= 0 && !isDead) Die();
    }

    /// <summary>
    /// Restore full health AND reset enemy attacks safely.
    /// Grants optional invulnerability to prevent immediate damage.
    /// </summary>
    public void RestoreFullHealth(bool grantInvulnerability = true)
    {
        currentHP = maxHP;
        enemyHits = 0;        // reset enemy attacks
        isDead = false;
        RestorePhysicsState();
        ReEnableDeathDisabledComponents();
        if (hpBar != null) hpBar.SetHP(currentHP);
        SaveCurrentHP();
        if (grantInvulnerability) lastHurtTime = Time.time;
    }

    public int GetHP() => currentHP;
    public bool IsDead() => isDead;

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        // A life is spent when the player dies, so the death panel immediately shows updated retries.
        Player.ConsumeRetryLife();

        // disable listed components (stop player input / movement)
        if (disableOnDeath != null)
        {
            foreach (var c in disableOnDeath)
            {
                if (c != null) c.enabled = false;
            }
        }

        // play death animation if available
        if (animator != null && !string.IsNullOrEmpty(deathTrigger))
        {
            animator.SetTrigger(deathTrigger);
        }

        // show death UI (tries assigned controller, falls back to FindFirstObjectByType)
        if (deathUI == null) deathUI = FindFirstObjectByType<DeathUIController>();
        if (deathUI != null) deathUI.ShowDeath();

        bool startedDeathAnim = (animator != null && !string.IsNullOrEmpty(deathTrigger));
        if (waitForDeathAnimation && animator != null && !string.IsNullOrEmpty(deathStateName) && startedDeathAnim)
        {
            StartCoroutine(WaitForDeathAnimationAndCleanup());
        }
        else
        {
            DoDeathCleanup();
        }

        SaveCurrentHP();
        onDeath?.Invoke();
    }

    private IEnumerator WaitForDeathAnimationAndCleanup()
    {
        float timeout = 3f;
        float elapsed = 0f;

        while (animator != null && !animator.GetCurrentAnimatorStateInfo(0).IsName(deathStateName) && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (animator == null || elapsed >= timeout)
        {
            DoDeathCleanup();
            yield break;
        }

        while (animator != null && animator.GetCurrentAnimatorStateInfo(0).IsName(deathStateName))
            yield return null;

        DoDeathCleanup();
    }

    private void DoDeathCleanup()
    {
        // spawn death VFX / SFX if assigned
        if (deathVFX != null) Instantiate(deathVFX, transform.position, Quaternion.identity);
        if (deathClip != null) AudioSource.PlayClipAtPoint(deathClip, transform.position, deathVolume);

        // hide HP bar if present
        if (hpBar != null) hpBar.SetHP(0);

        // make corpse pass-through if configured
        if (makeCorpsePassThrough)
        {
            var cols = GetComponentsInChildren<Collider2D>();
            foreach (var c in cols)
            {
                if (c == null) continue;
                if (setTriggerOnDeath) c.isTrigger = true; else c.enabled = false;
            }

            var rb = GetComponent<Rigidbody2D>();
            if (rb != null) rb.simulated = false;
            var childRbs = GetComponentsInChildren<Rigidbody2D>();
            foreach (var r in childRbs) if (r != null) r.simulated = false;
        }

        // keep player GameObject; additional behavior (respawn) is handled by UI buttons
    }

    private void CachePhysicsState()
    {
        cachedColliders = GetComponentsInChildren<Collider2D>(true);
        if (cachedColliders != null)
        {
            cachedColliderIsTrigger = new bool[cachedColliders.Length];
            cachedColliderEnabled = new bool[cachedColliders.Length];
            for (int i = 0; i < cachedColliders.Length; i++)
            {
                var c = cachedColliders[i];
                if (c == null) continue;
                cachedColliderIsTrigger[i] = c.isTrigger;
                cachedColliderEnabled[i] = c.enabled;
            }
        }

        cachedRigidbodies = GetComponentsInChildren<Rigidbody2D>(true);
        if (cachedRigidbodies != null)
        {
            cachedRigidbodySimulated = new bool[cachedRigidbodies.Length];
            for (int i = 0; i < cachedRigidbodies.Length; i++)
            {
                var r = cachedRigidbodies[i];
                if (r == null) continue;
                cachedRigidbodySimulated[i] = r.simulated;
            }
        }
    }

    private void RestorePhysicsState()
    {
        if (cachedColliders != null)
        {
            for (int i = 0; i < cachedColliders.Length; i++)
            {
                var c = cachedColliders[i];
                if (c == null) continue;
                c.enabled = cachedColliderEnabled != null && i < cachedColliderEnabled.Length ? cachedColliderEnabled[i] : true;
                c.isTrigger = cachedColliderIsTrigger != null && i < cachedColliderIsTrigger.Length ? cachedColliderIsTrigger[i] : false;
            }
        }

        if (cachedRigidbodies != null)
        {
            for (int i = 0; i < cachedRigidbodies.Length; i++)
            {
                var r = cachedRigidbodies[i];
                if (r == null) continue;
                r.simulated = cachedRigidbodySimulated != null && i < cachedRigidbodySimulated.Length ? cachedRigidbodySimulated[i] : true;
            }
        }
    }

    private void ReEnableDeathDisabledComponents()
    {
        if (disableOnDeath == null) return;
        foreach (var c in disableOnDeath)
        {
            if (c != null) c.enabled = true;
        }
    }
}
