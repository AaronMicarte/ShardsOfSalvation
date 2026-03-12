using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Player controller (movement, jumping, and simple attack flow).
/// Focus is on readable, testable methods: input reading, movement, animation updates, and attacks are separated.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Player : MonoBehaviour
{
    [Header("Global Combat Preset")]
    [SerializeField, Tooltip("When enabled, combat/rage values are force-applied from script so all floors use the same player tuning")]
    private bool forceGlobalCombatPreset = true;

    [Header("Movement")]
    [SerializeField, Tooltip("Horizontal move speed")] private float moveSpeed = 8f;
    [SerializeField, Tooltip("Jump impulse force")] private float jumpForce = 8f;
    [SerializeField, Tooltip("Allow movement while airborne")] private bool airControl = true;
    [SerializeField, Tooltip("Multiplier applied to horizontal input (set to -1 to invert controls)")] private float inputMultiplier = 1f;

    [Header("Rage Mode")]
    [SerializeField, Tooltip("Enable/disable rage mode features globally for this player")] private bool enableRageMode = true;
    [SerializeField, Tooltip("Animator trigger used to enter rage mode once (prevents AnyState re-entry loops)")] private string rageStartTriggerParam = "RageStart";
    [SerializeField, Tooltip("Animator bool parameter used to switch into rage animation graph")] private string rageBoolParam = "Raging";
    [SerializeField, Tooltip("Default rage duration in seconds when StartRageMode() is used")] private float rageDuration = 10f;
    [Space, Header("Rage Audio")]
    [SerializeField, Tooltip("Optional clip to play when rage starts")] private AudioClip rageClip = null;
    [SerializeField, Range(0f, 1f), Tooltip("Volume at which to play the rage clip")] private float rageVolume = 1f;
    [SerializeField, Tooltip("If >0, stop the rage clip after this many seconds (useful for looping or long clips)")] private float rageSoundMaxDuration = 0f;
    [SerializeField, Tooltip("Fade-out duration when the rage sound stops (set 0 for instant)")] private float rageSoundFadeDuration = 0.5f;
    [SerializeField, Tooltip("Multiplier applied to movement speed while rage is active")] private float rageMoveSpeedMultiplier = 1.35f;
    [SerializeField, Tooltip("Multiplier applied to all damage while rage is active (basic + skill1 + percent)")] private float rageDamageMultiplier = 1.5f;
    [SerializeField, Tooltip("Multiplier applied to crit chance while rage is active")] private float rageCritChanceMultiplier = 1.5f;
    [SerializeField, Tooltip("Multiplier applied to crit damage multiplier while rage is active")] private float rageCritMultiplierMultiplier = 1.5f;
    [SerializeField, Tooltip("Multiplier applied to Skill1 hit radius while rage is active")] private float rageSkill1HitRadiusMultiplier = 2f;
    [SerializeField, Tooltip("Multiplier applied to Skill1 forward hit range (offset X) while rage is active")] private float rageSkill1RangeMultiplier = 2.2f;
    [SerializeField, Tooltip("Multiplier applied to dash distance while rage is active")] private float rageDashDistanceMultiplier = 1.4f;
    [SerializeField, Tooltip("Multiplier applied to dash duration while rage is active (higher = longer dash time)")] private float rageDashDurationMultiplier = 1.2f;
    [SerializeField, Tooltip("Animator state name used for rage idle (optional)")] private string rageIdleStateName = "rageidle";
    [SerializeField, Tooltip("Animator state name used for rage run (optional)")] private string rageRunStateName = "ragerun";
    [SerializeField, Tooltip("Animator state name used for rage jump (optional)")] private string rageJumpStateName = "ragejump";
    [SerializeField, Tooltip("Animator state name used for rage dash (optional)")] private string rageDashStateName = "ragedash";
    [SerializeField, Tooltip("Animator state name used when the player gets hit during rage (optional)")] private string rageHeavyDamageStateName = "rageheavydamage";
    [SerializeField, Tooltip("Optional animator trigger for rage heavy-damage reactions (uses state fallback when missing)")] private string rageHeavyDamageTriggerParam = "OnHitRageHeavyDamage";

    [Header("Audio")]
    [SerializeField, Tooltip("AudioSource for playing skill sounds")] private AudioSource audioSource;
    [SerializeField, Tooltip("AudioClip to play for Skill1")] private AudioClip skill1Clip; [SerializeField, Tooltip("AudioClip to play for the basic Attack")] private AudioClip attackClip;
    [SerializeField, Range(0f, 1f), Tooltip("Volume for attack sound when using PlayOneShot")] private float attackVolume = 1f; [SerializeField, Tooltip("AudioClip to play for Dash")] private AudioClip dashClip;
    [SerializeField, Range(0f, 1f), Tooltip("Volume for dash sound when using PlayOneShot")] private float dashVolume = 1f;
    [SerializeField, Tooltip("AudioClip to play when rage heavy-damage reaction starts")] private AudioClip rageHeavyDamageReactClip;
    [SerializeField, Range(0f, 1f), Tooltip("Volume for rage heavy-damage reaction SFX")] private float rageHeavyDamageReactVolume = 1f;
    [SerializeField, Tooltip("AudioClip to play on rage heavy-damage impact frame")] private AudioClip rageHeavyDamageImpactClip;
    [SerializeField, Range(0f, 1f), Tooltip("Volume for rage heavy-damage impact SFX")] private float rageHeavyDamageImpactVolume = 1f;
    [SerializeField, Tooltip("AudioClip to play for footsteps while walking")] private AudioClip footstepClip;
    [SerializeField, Range(0f, 1f), Tooltip("Volume for footstep sound when using PlayOneShot")] private float footstepVolume = 1f;
    [SerializeField, Tooltip("Seconds between footsteps while walking (lower = faster steps)")] private float footstepInterval = 0.4f;
    [SerializeField, Range(0.5f, 1.5f), Tooltip("Min pitch applied randomly to footsteps")] private float footstepPitchMin = 0.95f;
    [SerializeField, Range(0.5f, 1.5f), Tooltip("Max pitch applied randomly to footsteps")] private float footstepPitchMax = 1.05f;

    // Expose safe API for other systems to change the player's input multiplier
    public void SetInputMultiplier(float m) => inputMultiplier = m;
    public float GetInputMultiplier() => inputMultiplier;
    [ContextMenu("Toggle Input Inversion (Editor)")]
    private void ToggleInputInversion()
    {
        inputMultiplier = -inputMultiplier;
    }

    [Header("Ground Check")]
    [SerializeField, Tooltip("Transform at the player's feet used for ground checks (optional, will be created if empty)")] private Transform groundCheck;
    [SerializeField, Tooltip("Layers considered ground")] private LayerMask groundLayer;
    [SerializeField, Tooltip("Radius used for ground overlap check")] private float groundCheckRadius = 0.1f;
    [SerializeField, Tooltip("Use a box ground check instead of a circle")] private bool useBoxGroundCheck = true;
    [SerializeField, Tooltip("Size of the box used for ground checks (world units)")] private Vector2 groundCheckBoxSize = new Vector2(0.6f, 0.1f);

    [Header("Animation")]
    [SerializeField, Tooltip("Animator that controls player animations (optional)")] private Animator animator;
    [SerializeField, Tooltip("Float parameter name to drive speed (set in Animator). If empty, animations are not driven by code.")] private string speedParam = "Speed";
    [SerializeField, Tooltip("Damp time used when updating the speed parameter (0 = no damping)")] private float speedDampTime = 0.05f;
    [SerializeField, Tooltip("Animator trigger parameter name used to start the jump animation (AnyState -> Jump)")] private string jumpTrigger = "Jump";
    [SerializeField, Tooltip("Animator bool parameter name used to indicate the player is grounded (optional)")] private string groundedParam = "isGrounded";

    [Header("Combat")]
    [SerializeField, Tooltip("Animator trigger name used to start the attack animation")] private string attackTrigger = "Attack";
    [SerializeField, Tooltip("Animator bool parameter name to set while attacking (optional)")] private string attackBoolParam = "isAttacking";
    [SerializeField, Tooltip("Duration (s) to lock movement during attacks. If 0, code will wait until the animator exits the 'attack' state.")] private float attackDuration = 0.4f;
    [SerializeField, Tooltip("Seconds between allowed basic attacks (cooldown). Default: 0.5s")] private float attackCooldown = 0.8f;
    [SerializeField, Tooltip("Minimum damage dealt by player's basic attack (flat)")] private int attackDamage = 12;
    [SerializeField, Tooltip("Maximum damage dealt by player's basic attack (flat)")] private int attackDamageMax = 15;
    [SerializeField, Tooltip("If true, player's basic attack uses percentage of enemy max HP instead of flat damage (use Grounded Mode carefully)")] private bool attackUsesPercent = false;
    [SerializeField, Tooltip("Percent of enemy max HP when attack uses percent (0..1)")] private float attackPercent = 0.25f;
    [SerializeField, Tooltip("Chance (0..1) for attack to be critical")] private float attackCritChance = 0.13f;
    [SerializeField, Tooltip("Critical damage multiplier (applied to flat damage or percent)")] private float attackCritMultiplier = 1.3f;
    [SerializeField, Tooltip("Radius used to detect enemies for attack hit")] private float attackHitRadius = 0.5f;
    [SerializeField, Tooltip("Offset from player pivot where attack is centered (local space)")] private Vector2 attackHitOffset = new Vector2(0.6f, 0f);
    [SerializeField, Tooltip("Layer mask used to detect enemies")] private LayerMask enemyLayer;

    [Header("Skill1 Combat")]
    [SerializeField, Tooltip("Minimum damage dealt by Skill1 attack")] private int skill1Damage = 22;
    [SerializeField, Tooltip("Maximum damage dealt by Skill1 attack")] private int skill1DamageMax = 25;
    [SerializeField, Tooltip("Chance (0..1) for Skill1 to be critical")] private float skill1CritChance = 0.1f;
    [SerializeField, Tooltip("Critical damage multiplier for Skill1")] private float skill1CritMultiplier = 1.2f;
    [SerializeField, Tooltip("Radius used to detect enemies for Skill1 hit")] private float skill1HitRadius = 0.8f;
    [SerializeField, Tooltip("Offset from player pivot where Skill1 is centered (local space)")] private Vector2 skill1HitOffset = new Vector2(0.6f, 0f);

    [Header("Rage Heavy Damage Combat")]
    [SerializeField, Tooltip("Minimum damage dealt by rage heavy-damage hit. Runtime enforces this stays above Skill1 damage.")]
    private int rageHeavyDamageDamage = 32;
    [SerializeField, Tooltip("Maximum damage dealt by rage heavy-damage hit")]
    private int rageHeavyDamageDamageMax = 35;
    [SerializeField, Tooltip("Chance (0..1) for rage heavy-damage to crit")]
    private float rageHeavyDamageCritChance = 0.15f;
    [SerializeField, Tooltip("Critical damage multiplier for rage heavy-damage")]
    private float rageHeavyDamageCritMultiplier = 1.55f;
    [SerializeField, Tooltip("Radius used to detect enemies for rage heavy-damage")]
    private float rageHeavyDamageHitRadius = 1.0f;
    [SerializeField, Tooltip("Offset from player pivot where rage heavy-damage is centered (local space)")]
    private Vector2 rageHeavyDamageHitOffset = new Vector2(0.7f, 0f);

    [Header("Damage Popup")]
    [SerializeField, Tooltip("Prefab used to display floating damage text (optional)")] private DamagePopup damagePopupPrefab;
    [SerializeField, Tooltip("Base world-space offset from player for popup text (positive x is in front of player)")] private Vector3 damagePopupOffset = new Vector3(0.15f, 0.65f, 0f);
    [SerializeField, Tooltip("Random popup spread around the base offset (kept intentionally small)")] private Vector2 damagePopupRandomRange = new Vector2(0.08f, 0.08f);
    [SerializeField, Tooltip("Radius in world units used for a circular random offset around the player")]
    private float damagePopupRadius = 0.5f; // setable in inspector for larger spread
    [SerializeField] private Color damagePopupColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color critPopupColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color missPopupColor = new Color(0.7f, 0.7f, 0.7f, 1f);
    [SerializeField] private Color percentPopupColor = new Color(0.35f, 1f, 0.9f, 1f);

    [Header("Attack Fallback")]
    [SerializeField, Tooltip("Automatically apply hit if animation event is missing")] private bool autoApplyAttack = true;
    [SerializeField, Tooltip("Delay (seconds) before auto-applying hit if animation event is missing")] private float attackHitDelay = 0.15f;

    [Header("Rage Heavy Damage Input")]
    [SerializeField, Tooltip("If true, taking damage while raging auto-triggers the rage heavy-damage reaction. Disable for manual key-only control.")]
    private bool autoTriggerRageHeavyDamageOnHit = false;
    [SerializeField, Tooltip("Key used to apply heavy-damage to the player while raging")]
    private KeyCode rageHeavyDamageKey = KeyCode.R;
    [SerializeField, Tooltip("Minimum seconds between rage heavy-damage reaction triggers to avoid animation loops")]
    private float rageHeavyDamageReactionCooldown = 0.35f;

    // runtime attack hit state
    private bool attackHitApplied = false;
    private Coroutine applyAttackCoroutine = null;

    // remember which way the player is considered "facing" (1 = right, -1 = left).
    // using the last nonzero horizontal input is more reliable than inspecting
    // localScale.x, which may be negative even when the character is looking right
    // depending on how the art was authored.  this value is used for pop‑up
    // placement and any other logic that needs the facing direction.
    private float lastFacingDir = 1f;

    // --- Skill1 (configurable animation-based skill) ---
    [SerializeField, Tooltip("Animator trigger name used to start Skill1 (optional)")] private string skill1Trigger = "Skill1";
    [SerializeField, Tooltip("Duration (s) to lock movement during Skill1 (0 = wait for animator). Default: 0.7s")] private float skill1Duration = 0.7f;
    [SerializeField, Tooltip("Seconds between allowed Skill1 uses (cooldown). Default: 1s")] private float skill1Cooldown = 1f;

    [Header("Combat Events")]
    [SerializeField] private UnityEvent onAttackHit;
    [SerializeField] private UnityEvent onAttackEnd;
    [SerializeField, Tooltip("Invoked on the animation frame where Skill1 should hit (optional)")] private UnityEvent onSkill1Hit;
    [SerializeField, Tooltip("Invoked when the Skill1 animation ends (optional)")] private UnityEvent onSkill1End;

    [Header("Jumping")]
    [SerializeField, Tooltip("Maximum number of consecutive jumps (1 = single jump, 2 = double jump)")] private int maxJumps = 2;

    [Header("Dash")]
    [SerializeField, Tooltip("Distance to dash (units)")] private float dashDistance = 5f;
    [SerializeField, Tooltip("Dash duration in seconds")] private float dashDuration = 0.15f;
    [SerializeField, Tooltip("Seconds between dashes (cooldown)")] private float dashCooldown = 0.5f;
    [SerializeField, Tooltip("Max time between taps to count as a double-tap")] private float doubleTapTime = 0.25f;
    [SerializeField, Tooltip("Animator trigger name used to start Dash (optional)")] private string dashTrigger = "Dash";
    [SerializeField, Tooltip("Enable player's ability to dash (can be toggled at runtime)")] private bool enableDash = true;

    [SerializeField, Tooltip("When true, temporarily ignore collisions with the specified enemy layers while dashing")] private bool passThroughEnemiesOnDash = true;
    [SerializeField, Tooltip("Layers considered 'enemy' for pass-through (set to the layers enemies use)")] private LayerMask enemyCollisionMask = 0; // set this in the Inspector to your enemies' layer(s)


    [Header("Trail")]
    [SerializeField, Tooltip("Prefab used for afterimage trail (must have SpriteRenderer)")] private GameObject trailPrefab;
    [SerializeField, Tooltip("Seconds an afterimage fades out")] private float trailLifetime = 0.4f;
    [SerializeField, Tooltip("Seconds between spawned afterimages while dashing")] private float trailInterval = 0.05f;

    private float lastTapTimeLeft = -Mathf.Infinity;
    private float lastTapTimeRight = -Mathf.Infinity;
    private float lastDashTime = -Mathf.Infinity;

    // When non-infinite, prevents dashing until Time.time >= dashDisabledUntil
    private float dashDisabledUntil = -Mathf.Infinity;
    // --- blind (evasion) status: when active, player's hits have a chance to miss
    private float blindUntilTime = -Mathf.Infinity;
    private float blindChance = 0f; // 0..1

    // Optional timed movement speed override (used by debuffs)
    private float moveSpeedOverrideUntil = -Mathf.Infinity;
    private float moveSpeedOverrideValue = -1f;

    private bool isDashing = false;

    // Trail runtime
    private SpriteRenderer spriteRenderer;
    private Coroutine trailRoutine;
    private Coroutine footstepRoutine;

    // Tracks which enemy layers (if any) were temporarily ignored while dashing so we can restore them
    private System.Collections.Generic.List<int> currentlyIgnoredEnemyLayers = null;

    // Reference to PlayerHealth to grant dash invulnerability
    private PlayerHealth playerHealth;

    // --- runtime state ---
    private Rigidbody2D rb;
    private Vector3 initialScale;
    private float horizontal;
    private bool jumpRequested;
    private bool isGrounded;
    private int jumpsRemaining;
    private bool isAttacking;
    private float lastAttackTime = -Mathf.Infinity;
    private float lastSkill1Time = -Mathf.Infinity;

    // Cached animator parameter checks (avoid repeated string lookups) 
    private bool animHasSpeedParam;
    private bool animHasAttackTrigger;
    private bool animHasAttackBool;
    private bool animHasJumpTrigger;
    private bool animHasGroundedBool;
    private bool animHasSkill1Trigger;
    private bool animHasDashTrigger;
    private bool animHasRageStartTrigger;
    private bool animHasRageBool;
    private bool animHasRageHeavyDamageTrigger;

    private bool isRaging;
    private bool rageEntryPlaying; // lock movement during initial ragemode animation
    private float rageUntilTime = -Mathf.Infinity;
    private Coroutine rageCoroutine;
    private Coroutine rageHeavyDamageRecoverRoutine;
    private bool rageHeavyDamageInProgress;
    private float lastRageHeavyDamageReactionTime = -Mathf.Infinity;

    public bool IsAttacking => isAttacking;
    public bool IsDashing => isDashing;
    public bool IsRaging => isRaging;

    /// <summary>
    /// Enable/disable the player's ability to dash at runtime.
    /// The dash can also be temporarily blocked by a timed debuff (see ApplyDashDisableForSeconds).
    /// </summary>
    public void SetDashEnabled(bool enabled) => enableDash = enabled;
    public bool IsDashEnabled() => enableDash && Time.time >= dashDisabledUntil;

    /// <summary>
    /// Temporarily disable the player's ability to dash for `seconds` (cancels any active dash immediately).
    /// This is used by boss attacks and traps to reliably prevent dashing for a duration.
    /// </summary>
    public void ApplyDashDisableForSeconds(float seconds)
    {
        float until = Time.time + Mathf.Max(0f, seconds);
        dashDisabledUntil = Mathf.Max(dashDisabledUntil, until);
        try { CancelDash(); } catch { }
        Debug.Log($"Player '{name}': dash disabled for {seconds}s (until {dashDisabledUntil:F2})");
    }

    /// <summary>
    /// Accessors for the player's base movement speed and a helper to temporarily override it.
    /// Use `SetMoveSpeedForSeconds` when you need to apply a temporary slow/down effect that restores automatically.
    /// </summary>
    public float GetMoveSpeed() => moveSpeed;
    public void SetMoveSpeed(float speed) => moveSpeed = speed;

    public float GetCurrentMoveSpeed()
    {
        if (Time.time < moveSpeedOverrideUntil)
            return moveSpeedOverrideValue;
        return moveSpeed;
    }

    public float GetDashDisabledRemainingSeconds() => Mathf.Max(0f, dashDisabledUntil - Time.time);
    public bool IsBlinded() => Time.time < blindUntilTime && blindChance > 0f;
    public float GetBlindRemainingSeconds() => Mathf.Max(0f, blindUntilTime - Time.time);

    /// <summary>
    /// Temporarily blinds the player for <paramref name="seconds"/>.  While blinded,
    /// each attack has <paramref name="chance"/> probability to miss completely.
    /// </summary>
    public void ApplyBlindForSeconds(float seconds, float chance)
    {
        blindUntilTime = Mathf.Max(blindUntilTime, Time.time + Mathf.Max(0f, seconds));
        blindChance = Mathf.Clamp01(chance);
        Debug.Log($"Player '{name}': blinded {seconds}s at {blindChance * 100f:0.#}% chance.");
    }

    public float GetRageRemainingSeconds() => isRaging ? Mathf.Max(0f, rageUntilTime - Time.time) : 0f;

    public void StartRageMode() => ActivateRageForSeconds(rageDuration);

    public void ActivateRageForSeconds(float seconds)
    {
        if (!enableRageMode) return;

        float duration = seconds;
        if (duration <= 0f)
        {
            duration = 10f;
            Debug.LogWarning($"Player '{name}': Rage duration was <= 0. Falling back to {duration}s.");
        }

        rageUntilTime = Mathf.Max(rageUntilTime, Time.time + duration);

        if (!isRaging)
        {
            isRaging = true;
            rageEntryPlaying = true; // prevent movement until ragemode animation finishes
            // immediately halt horizontal motion
            horizontal = 0f;
            if (rb != null)
            {
                var v = rb.linearVelocity;
                v.x = 0f;
                rb.linearVelocity = v;
            }
            if (animator != null && animHasRageStartTrigger)
                animator.SetTrigger(rageStartTriggerParam);
            SetRageAnimatorState(true);

            // play rage audio if available
            if (rageClip != null)
            {
                if (audioSource == null) audioSource = GetComponent<AudioSource>();
                if (audioSource != null)
                {
                    audioSource.PlayOneShot(rageClip, rageVolume);
                    if (rageSoundMaxDuration > 0f)
                        StartCoroutine(StopRageSoundAfter(rageSoundMaxDuration));
                }
            }
        }

        if (rageCoroutine != null) StopCoroutine(rageCoroutine);
        rageCoroutine = StartCoroutine(RageTimerRoutine());
    }

    public void StopRageMode()
    {
        rageUntilTime = -Mathf.Infinity;

        if (rageCoroutine != null)
        {
            StopCoroutine(rageCoroutine);
            rageCoroutine = null;
        }

        if (!isRaging) return;

        isRaging = false;
        SetRageAnimatorState(false);
    }

    public void SetMoveSpeedForSeconds(float speed, float seconds)
    {
        moveSpeedOverrideValue = speed;
        moveSpeedOverrideUntil = Mathf.Max(moveSpeedOverrideUntil, Time.time + Mathf.Max(0f, seconds));
        Debug.Log($"Player '{name}': move speed overridden to {speed} for {seconds}s (until {moveSpeedOverrideUntil:F2})");
    }

    private void ApplyGlobalCombatPreset()
    {
        if (!forceGlobalCombatPreset) return;

        // Apply one shared combat tuning across all scenes/floors.
        attackDuration = 0.4f;
        attackCooldown = 0.8f;
        attackDamage = 12;
        attackDamageMax = 15;
        attackUsesPercent = false;
        attackPercent = 0f;
        attackCritChance = 0.13f;
        attackCritMultiplier = 1.3f;
        attackHitRadius = 0.5f;
        attackHitOffset = new Vector2(0.6f, 0f);

        skill1Damage = 22;
        skill1DamageMax = 25;
        skill1CritChance = 0.15f;
        skill1CritMultiplier = 1.2f;
        skill1HitRadius = 0.8f;
        skill1HitOffset = new Vector2(0.6f, 0f);

        rageMoveSpeedMultiplier = 1.35f;
        rageDamageMultiplier = 1.5f;
        rageCritChanceMultiplier = 1.5f;
        rageCritMultiplierMultiplier = 1.5f;
        rageSkill1HitRadiusMultiplier = 2f;
        rageSkill1RangeMultiplier = 2.2f;
        rageDashDistanceMultiplier = 1.4f;
        rageDashDurationMultiplier = 1.2f;

        rageHeavyDamageDamage = 32;
        rageHeavyDamageDamageMax = 35;
        rageHeavyDamageCritChance = 0.22f;
        rageHeavyDamageCritMultiplier = 1.55f;
        rageHeavyDamageHitRadius = 1f;
        rageHeavyDamageHitOffset = new Vector2(0.7f, 0f);
    }

    private IEnumerator RageTimerRoutine()
    {
        while (isRaging && Time.time < rageUntilTime)
            yield return null;

        rageCoroutine = null;
        StopRageMode();
    }

    private IEnumerator StopRageSoundAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (audioSource != null)
        {
            if (rageSoundFadeDuration > 0f)
            {
                float startVol = audioSource.volume;
                float t = 0f;
                while (t < rageSoundFadeDuration)
                {
                    t += Time.deltaTime;
                    audioSource.volume = Mathf.Lerp(startVol, 0f, t / rageSoundFadeDuration);
                    yield return null;
                }
            }
            audioSource.Stop();
            audioSource.volume = 1f; // reset for next play
        }
    }

    [ContextMenu("Test Rage Mode")]
    private void TestRageMode() => StartRageMode();

    [ContextMenu("Stop Rage Mode")]
    private void StopRageModeFromMenu() => StopRageMode();

    void Awake()
    {
        ApplyGlobalCombatPreset();

        rb = GetComponent<Rigidbody2D>();
        initialScale = transform.localScale;
        // make sure our facing tracker matches the sprite's initial orientation
        lastFacingDir = Mathf.Sign(initialScale.x == 0f ? 1f : initialScale.x);
        jumpsRemaining = maxJumps;

        if (groundCheck == null)
        {
            var go = new GameObject("GroundCheck");
            go.transform.SetParent(transform);
            go.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            groundCheck = go.transform;
        }

        // Auto-size the box ground check to match the player's collider when available
        var col = GetComponent<Collider2D>();
        if (col != null && useBoxGroundCheck)
        {
            // Use most of the collider width but narrow height so box sits at the feet
            var b = col.bounds;
            groundCheckBoxSize = new Vector2(Mathf.Max(0.1f, b.size.x * 0.9f), Mathf.Max(0.06f, b.size.y * 0.12f));
            // place groundCheck at bottom of collider (world space)
            Vector3 bottomWorld = new Vector3(b.center.x, b.min.y + groundCheckBoxSize.y * 0.5f, transform.position.z);
            groundCheck.position = bottomWorld;
        }

        // Cache components used by trail / visuals
        spriteRenderer = GetComponent<SpriteRenderer>();
        playerHealth = GetComponent<PlayerHealth>();
        if (playerHealth != null && autoTriggerRageHeavyDamageOnHit)
            playerHealth.onHit.AddListener(TriggerRageHeavyDamageReaction);

        CacheAnimatorParameters();
    }

    void OnDestroy()
    {
        if (playerHealth != null && autoTriggerRageHeavyDamageOnHit)
            playerHealth.onHit.RemoveListener(TriggerRageHeavyDamageReaction);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying) return;
        ApplyGlobalCombatPreset();
        var col = GetComponent<Collider2D>();
        if (col != null && useBoxGroundCheck)
        {
            var b = col.bounds;
            groundCheckBoxSize = new Vector2(Mathf.Max(0.1f, b.size.x * 0.9f), Mathf.Max(0.06f, b.size.y * 0.12f));
            var go = transform.Find("GroundCheck");
            if (go != null) groundCheck = go;
            if (groundCheck != null)
                groundCheck.position = new Vector3(b.center.x, b.min.y + groundCheckBoxSize.y * 0.5f, transform.position.z);
        }

        if (attackCritMultiplier < 1f) attackCritMultiplier = 1f;
        if (skill1CritMultiplier < 1f) skill1CritMultiplier = 1f;
        if (rageHeavyDamageCritMultiplier < 1f) rageHeavyDamageCritMultiplier = 1f;

        attackDamage = Mathf.Max(1, attackDamage);
        attackDamageMax = Mathf.Max(attackDamage, attackDamageMax);
        skill1Damage = Mathf.Max(1, skill1Damage);
        skill1DamageMax = Mathf.Max(skill1Damage, skill1DamageMax);
        rageHeavyDamageDamage = Mathf.Max(1, rageHeavyDamageDamage);
        rageHeavyDamageDamageMax = Mathf.Max(rageHeavyDamageDamage, rageHeavyDamageDamageMax);
    }
#endif

    void Update()
    {
        // If player is dead, ignore all input and actions
        if (playerHealth != null && playerHealth.IsDead()) return;

        // detect end of entry animation
        if (rageEntryPlaying && animator != null)
        {
            var st = animator.GetCurrentAnimatorStateInfo(0);
            if (!st.IsName("ragemode"))
                rageEntryPlaying = false;
        }

        if (isRaging)
            SetRageAnimatorState(true);

        ReadInput();
        HandleAttackInput();
        HandleSkillInput();
        HandleDashInput();
        HandleRageHeavyDamageInput();
        HandleSpriteFlip();
        UpdateFootsteps();

        // debug/test: press Y to start rage mode (inspect in editor)
#if UNITY_EDITOR || DEVELOPMENT_BUILD
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        if (Keyboard.current != null && Keyboard.current.yKey.wasPressedThisFrame)
            StartRageMode();
#else
        if (Input.GetKeyDown(KeyCode.Y))
            StartRageMode();
#endif
#endif
    }

    void FixedUpdate()
    {
        // If player is dead, skip physics/movement updates
        if (playerHealth != null && playerHealth.IsDead()) return;

        DoGroundCheck();
        HandleMovement();
        UpdateAnimator();
        HandleJump();
    }

    // ------------------------ Input ------------------------
    private void ReadInput()
    {
        if (rageEntryPlaying)
        {
            horizontal = 0f;
            // ignore jump requests as well
            jumpRequested = false;
            return;
        }
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        horizontal = 0f;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) horizontal -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) horizontal += 1f;
            if (Keyboard.current.spaceKey.wasPressedThisFrame) jumpRequested = true;
        }
        if (Gamepad.current != null)
        {
            var stick = Gamepad.current.leftStick.ReadValue();
            horizontal = Mathf.Clamp(horizontal + stick.x, -1f, 1f);
            if (Gamepad.current.buttonSouth.wasPressedThisFrame) jumpRequested = true;
        }
#else
        horizontal = Input.GetAxisRaw("Horizontal");
        if (Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.Space))
            jumpRequested = true;
#endif
        // Apply any input multiplier (allows inversion and other input modifiers)
        horizontal *= inputMultiplier;
    }

    private void HandleAttackInput()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        bool attackRequested = (Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.kKey.wasPressedThisFrame))
                              || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                              || (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame);
#else
        bool attackRequested = Input.GetButtonDown("Fire1") || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.K) || Input.GetMouseButtonDown(0);
#endif
        if (attackRequested)
        {
            TryAttack();
        }
    }

    private void HandleRageHeavyDamageInput()
    {
        if (!isRaging) return;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        bool heavyDamageRequested = Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
        bool heavyDamageRequested = Input.GetKeyDown(rageHeavyDamageKey);
#endif
        if (!heavyDamageRequested) return;

        // Manual trigger for testing/controls: does not damage the player.
        TriggerRageHeavyDamageReaction();
    }

    private void HandleSkillInput()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        bool skillRequested = (Keyboard.current != null && (Keyboard.current.eKey.wasPressedThisFrame))
                              || (Gamepad.current != null && Gamepad.current.rightShoulder.wasPressedThisFrame);
#else
        bool skillRequested = Input.GetKeyDown(KeyCode.E);
#endif
        if (skillRequested)
            TrySkill1();
    }

    private void HandleDashInput()
    {
        if (rageEntryPlaying) return; // don't allow dash during start animation
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        if (Keyboard.current != null)
        {
            if (Keyboard.current.dKey.wasPressedThisFrame) CheckDashDirection(1);
            if (Keyboard.current.aKey.wasPressedThisFrame) CheckDashDirection(-1);
        }
        if (Gamepad.current != null)
        {
            var stick = Gamepad.current.leftStick.ReadValue();
            if (stick.x > 0.8f) CheckDashDirection(1);
            else if (stick.x < -0.8f) CheckDashDirection(-1);
        }
#else
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) CheckDashDirection(1);
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) CheckDashDirection(-1);
#endif
    }

    private void CheckDashDirection(int dir)
    {
        // Early exit if we can't dash right now (respect explicit enable and timed debuffs)
        if (!IsDashEnabled()) return;
        if (isDashing || isAttacking) return;

        // respect input inversion so dash follows player controls:
        int effectiveDir = (inputMultiplier < 0f) ? -dir : dir;

        if (effectiveDir == 1)
        {
            if (Time.time - lastTapTimeRight <= doubleTapTime && Time.time >= lastDashTime + dashCooldown)
                StartCoroutine(DoDash(effectiveDir));
            lastTapTimeRight = Time.time;
        }
        else
        {
            if (Time.time - lastTapTimeLeft <= doubleTapTime && Time.time >= lastDashTime + dashCooldown)
                StartCoroutine(DoDash(effectiveDir));
            lastTapTimeLeft = Time.time;
        }
    }

    private IEnumerator DoDash(int dir)
    {
        isDashing = true;
        // Play dash sound (uses existing AudioSource if assigned, otherwise tries GetComponent<AudioSource>())
        if (dashClip != null)
        {
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource != null) audioSource.PlayOneShot(dashClip, dashVolume);
        }
        lastDashTime = Time.time;
        if (playerHealth != null) playerHealth.SetDashInvulnerable(true);
        if (animator != null && animHasDashTrigger)
        {
            if (isRaging && !string.IsNullOrEmpty(rageDashStateName))
                animator.Play(rageDashStateName, 0, 0f);
            else
                animator.SetTrigger(dashTrigger);
        }

        // Optionally ignore collisions with enemy layers while dashing (track them so we can restore on cancel)
        currentlyIgnoredEnemyLayers = null;
        if (passThroughEnemiesOnDash && enemyCollisionMask != 0)
        {
            currentlyIgnoredEnemyLayers = new System.Collections.Generic.List<int>();
            int playerLayer = gameObject.layer;
            for (int i = 0; i < 32; i++)
            {
                if ((enemyCollisionMask.value & (1 << i)) != 0)
                {
                    Physics2D.IgnoreLayerCollision(playerLayer, i, true);
                    currentlyIgnoredEnemyLayers.Add(i);
                }
            }
        }

        // start spawning afterimages while dashing
        if (trailPrefab != null && spriteRenderer != null)
            trailRoutine = StartCoroutine(TrailRoutine());

        float effectiveDashDistance = GetEffectiveDashDistance();
        float effectiveDashDuration = GetEffectiveDashDuration();
        float speed = effectiveDashDistance / Mathf.Max(0.0001f, effectiveDashDuration);
        float end = Time.time + effectiveDashDuration;

        while (Time.time < end)
        {
            // abort early if player died during the dash
            if (playerHealth != null && playerHealth.IsDead()) break;
            var v = rb.linearVelocity;
            v.x = dir * speed;
            rb.linearVelocity = v;
            yield return null;
        }

        // Stop spawning trails
        if (trailRoutine != null)
        {
            StopCoroutine(trailRoutine);
            trailRoutine = null;
        }

        var final = rb.linearVelocity;
        final.x = 0f;
        rb.linearVelocity = final;

        isDashing = false;
        if (playerHealth != null) playerHealth.SetDashInvulnerable(false);

        if (isRaging && animator != null)
            PlayRageLocomotionState();

        // Restore previously ignored layers
        if (currentlyIgnoredEnemyLayers != null)
        {
            int playerLayer = gameObject.layer;
            foreach (var l in currentlyIgnoredEnemyLayers)
                Physics2D.IgnoreLayerCollision(playerLayer, l, false);
            currentlyIgnoredEnemyLayers = null;
        }
    }

    /// <summary>
    /// Cancel an in-progress dash immediately and restore any temporary state (invulnerability, ignored layers, trails).
    /// </summary>
    public void CancelDash(bool setCooldown = true)
    {
        if (!isDashing) return;

        isDashing = false;

        // stop spawning trails
        if (trailRoutine != null)
        {
            StopCoroutine(trailRoutine);
            trailRoutine = null;
        }

        // clear invulnerability
        if (playerHealth != null) playerHealth.SetDashInvulnerable(false);

        // zero horizontal velocity
        if (rb != null)
        {
            var v = rb.linearVelocity;
            v.x = 0f;
            rb.linearVelocity = v;
        }

        // restore ignored layers if any
        if (currentlyIgnoredEnemyLayers != null)
        {
            int playerLayer = gameObject.layer;
            foreach (var l in currentlyIgnoredEnemyLayers)
                Physics2D.IgnoreLayerCollision(playerLayer, l, false);
            currentlyIgnoredEnemyLayers = null;
        }

        if (setCooldown) lastDashTime = Time.time;
    }

    // ------------------------ Dash Trail ------------------------
    private IEnumerator TrailRoutine()
    {
        while (isDashing)
        {
            // stop spawning trails if the player died
            if (playerHealth != null && playerHealth.IsDead()) yield break;
            SpawnTrail();
            yield return new WaitForSeconds(trailInterval);
        }
    }

    private void SpawnTrail()
    {
        if (trailPrefab == null || spriteRenderer == null) return;
        var go = Instantiate(trailPrefab, transform.position, transform.rotation);
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null && spriteRenderer != null)
        {
            sr.sprite = spriteRenderer.sprite;
            sr.flipX = spriteRenderer.flipX;
            sr.sortingLayerID = spriteRenderer.sortingLayerID;
            sr.sortingOrder = spriteRenderer.sortingOrder - 1;
        }
        StartCoroutine(FadeAndDestroy(go, trailLifetime));
    }

    private IEnumerator FadeAndDestroy(GameObject obj, float life)
    {
        var sr = obj.GetComponent<SpriteRenderer>();
        if (sr == null) { Destroy(obj, life); yield break; }
        var start = sr.color;
        float t = 0f;
        while (t < life)
        {
            t += Time.deltaTime;
            float a = 1f - (t / life);
            sr.color = new Color(start.r, start.g, start.b, start.a * a);
            yield return null;
        }
        Destroy(obj);
    }

    // ------------------------ Movement / Jumping ------------------------
    private void DoGroundCheck()
    {
        if (groundCheck == null)
        {
            isGrounded = Physics2D.OverlapCircle(transform.position + new Vector3(0f, -0.5f, 0f), groundCheckRadius, groundLayer);
            if (isGrounded) jumpsRemaining = maxJumps;
            return;
        }

        if (useBoxGroundCheck)
            isGrounded = Physics2D.OverlapBox(groundCheck.position, groundCheckBoxSize, 0f, groundLayer);
        else
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        if (isGrounded)
            jumpsRemaining = maxJumps;
    }

    private void HandleMovement()
    {
        if (rageEntryPlaying) return; // lock movement during ragemode animation
        if (isDashing) return;
        if (isGrounded || airControl)
        {
            var v = rb.linearVelocity;
            v.x = horizontal * GetEffectiveMoveSpeed();
            rb.linearVelocity = v;
        }
    }

    private void HandleJump()
    {
        if (jumpRequested && jumpsRemaining > 0)
        {
            Jump();
            jumpsRemaining--;
        }
        jumpRequested = false;
    }

    private void Jump()
    {
        var v = rb.linearVelocity;
        v.y = 0f; // reset vertical velocity for consistent jumps
        rb.linearVelocity = v;
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        if (animator != null)
        {
            if (isRaging && !string.IsNullOrEmpty(rageJumpStateName))
                animator.Play(rageJumpStateName, 0, 0f);
            else if (animHasJumpTrigger)
                animator.SetTrigger(jumpTrigger);
        }
    }

    [ContextMenu("Force Jump (Editor)")]
    private void ForceJump() => Jump();

    // ------------------------ Animation / Visuals ------------------------
    private void CacheAnimatorParameters()
    {
        if (animator == null) return;
        animHasSpeedParam = AnimatorHasParameter(speedParam);
        animHasAttackTrigger = AnimatorHasParameter(attackTrigger);
        animHasAttackBool = !string.IsNullOrEmpty(attackBoolParam) && AnimatorHasParameter(attackBoolParam);
        animHasJumpTrigger = !string.IsNullOrEmpty(jumpTrigger) && AnimatorHasParameter(jumpTrigger);
        animHasGroundedBool = !string.IsNullOrEmpty(groundedParam) && AnimatorHasParameter(groundedParam);
        animHasSkill1Trigger = !string.IsNullOrEmpty(skill1Trigger) && AnimatorHasParameter(skill1Trigger);
        animHasDashTrigger = !string.IsNullOrEmpty(dashTrigger) && AnimatorHasParameter(dashTrigger);
        animHasRageStartTrigger = !string.IsNullOrEmpty(rageStartTriggerParam) && AnimatorHasParameter(rageStartTriggerParam);
        animHasRageBool = !string.IsNullOrEmpty(rageBoolParam) && AnimatorHasParameter(rageBoolParam);
        animHasRageHeavyDamageTrigger = !string.IsNullOrEmpty(rageHeavyDamageTriggerParam) && AnimatorHasParameter(rageHeavyDamageTriggerParam);
    }

    private void SetRageAnimatorState(bool value)
    {
        if (animator == null || !animHasRageBool) return;
        animator.SetBool(rageBoolParam, value);
    }

    private float GetEffectiveMoveSpeed()
    {
        float baseSpeed = GetCurrentMoveSpeed();
        if (!isRaging) return baseSpeed;
        return baseSpeed * Mathf.Max(0f, rageMoveSpeedMultiplier);
    }

    private int RollDamageRange(int minDamage, int maxDamage)
    {
        int min = Mathf.Max(1, minDamage);
        int max = Mathf.Max(min, maxDamage);
        // Random.Range(int,int) is min-inclusive, max-exclusive.
        return Random.Range(min, max + 1);
    }

    private int GetEffectiveSkill1DamageCeiling()
    {
        float m = isRaging ? Mathf.Max(0f, rageDamageMultiplier) : 1f;
        return Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(skill1Damage, skill1DamageMax) * m));
    }

    private int GetEffectiveAttackDamage()
    {
        int rolledBaseDamage = RollDamageRange(attackDamage, attackDamageMax);
        float m = isRaging ? Mathf.Max(0f, rageDamageMultiplier) : 1f;
        return Mathf.Max(1, Mathf.RoundToInt(rolledBaseDamage * m));
    }

    private float GetEffectiveAttackPercent()
    {
        float m = isRaging ? Mathf.Max(0f, rageDamageMultiplier) : 1f;
        return attackPercent * m;
    }

    private float GetEffectiveAttackCritChance()
    {
        float m = isRaging ? Mathf.Max(0f, rageCritChanceMultiplier) : 1f;
        return Mathf.Clamp01(attackCritChance * m);
    }

    private float GetEffectiveAttackCritMultiplier()
    {
        float m = isRaging ? Mathf.Max(0f, rageCritMultiplierMultiplier) : 1f;
        return Mathf.Max(1f, attackCritMultiplier * m);
    }

    private int GetEffectiveSkill1Damage()
    {
        int rolledBaseDamage = RollDamageRange(skill1Damage, skill1DamageMax);
        float m = isRaging ? Mathf.Max(0f, rageDamageMultiplier) : 1f;
        return Mathf.Max(1, Mathf.RoundToInt(rolledBaseDamage * m));
    }

    private float GetEffectiveSkill1CritChance()
    {
        float m = isRaging ? Mathf.Max(0f, rageCritChanceMultiplier) : 1f;
        return Mathf.Clamp01(skill1CritChance * m);
    }

    private float GetEffectiveSkill1CritMultiplier()
    {
        float m = isRaging ? Mathf.Max(0f, rageCritMultiplierMultiplier) : 1f;
        return Mathf.Max(1f, skill1CritMultiplier * m);
    }

    private float GetEffectiveSkill1HitRadius()
    {
        float m = isRaging ? Mathf.Max(0f, rageSkill1HitRadiusMultiplier) : 1f;
        return skill1HitRadius * m;
    }

    private Vector2 GetEffectiveSkill1HitOffset()
    {
        float m = isRaging ? Mathf.Max(0f, rageSkill1RangeMultiplier) : 1f;
        return new Vector2(skill1HitOffset.x * m, skill1HitOffset.y);
    }

    private int GetEffectiveRageHeavyDamage()
    {
        int effectiveSkill1Ceiling = GetEffectiveSkill1DamageCeiling();
        int rolledHeavy = RollDamageRange(rageHeavyDamageDamage, rageHeavyDamageDamageMax);
        // Guarantee heavy damage is always stronger than Skill1 by at least 1.
        return Mathf.Max(rolledHeavy, effectiveSkill1Ceiling + 1);
    }

    private float GetEffectiveRageHeavyDamageCritChance()
    {
        float m = isRaging ? Mathf.Max(0f, rageCritChanceMultiplier) : 1f;
        return Mathf.Clamp01(rageHeavyDamageCritChance * m);
    }

    private float GetEffectiveRageHeavyDamageCritMultiplier()
    {
        return Mathf.Max(1f, rageHeavyDamageCritMultiplier);
    }

    private float GetEffectiveRageHeavyDamageHitRadius()
    {
        float m = isRaging ? Mathf.Max(0f, rageSkill1HitRadiusMultiplier) : 1f;
        return rageHeavyDamageHitRadius * m;
    }

    // Spawn a floating damage text near the player only (world-space),
    // with a random offset in a circle around the hero.
    private void SpawnDamagePopup(string message, Color color)
    {
        if (damagePopupPrefab == null) return;

        // pick a random point inside a circle of radius damagePopupRadius
        Vector2 rndCircle = Random.insideUnitCircle * damagePopupRadius;

        // small base offset in front of the character (optional)
        Vector3 baseOffset = new Vector3(Mathf.Abs(damagePopupOffset.x) * lastFacingDir,
                                         damagePopupOffset.y,
                                         damagePopupOffset.z);

        Vector3 worldPos = transform.position + baseOffset + new Vector3(rndCircle.x, rndCircle.y, 0f);

        var popup = Instantiate(damagePopupPrefab.gameObject);
        popup.transform.SetParent(null);
        popup.transform.position = worldPos;
        popup.transform.rotation = Quaternion.identity;
        popup.transform.localScale = Vector3.one;
        popup.GetComponent<DamagePopup>().Setup(message, color);

        // debug positions (remove when satisfied)
        Debug.Log($"Spawned popup at {worldPos} (player {transform.position}), rnd={rndCircle}");
    }


    private float GetEffectiveDashDistance()
    {
        float m = isRaging ? Mathf.Max(0f, rageDashDistanceMultiplier) : 1f;
        return dashDistance * m;
    }

    private float GetEffectiveDashDuration()
    {
        float m = isRaging ? Mathf.Max(0f, rageDashDurationMultiplier) : 1f;
        return Mathf.Max(0.01f, dashDuration * m);
    }

    private bool AnimatorHasParameter(string name)
    {
        if (animator == null) return false;
        foreach (var p in animator.parameters)
            if (p.name == name) return true;
        return false;
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;
        if (animHasSpeedParam)
            animator.SetFloat(speedParam, Mathf.Abs(rb.linearVelocity.x), speedDampTime, Time.deltaTime);
        if (animHasGroundedBool)
            animator.SetBool(groundedParam, isGrounded);

        if (isRaging && isGrounded)
        {
            var st = animator.GetCurrentAnimatorStateInfo(0);
            if (st.IsName(rageJumpStateName))
                PlayRageLocomotionState();
        }
    }

    private void PlayRageLocomotionState()
    {
        if (animator == null) return;
        string target = Mathf.Abs(horizontal) > 0.1f ? rageRunStateName : rageIdleStateName;
        if (!string.IsNullOrEmpty(target))
            animator.Play(target, 0, 0f);
    }

    private void HandleSpriteFlip()
    {
        if (horizontal > 0.1f)
        {
            transform.localScale = new Vector3(Mathf.Abs(initialScale.x), initialScale.y, initialScale.z);
            lastFacingDir = 1f;
        }
        else if (horizontal < -0.1f)
        {
            transform.localScale = new Vector3(-Mathf.Abs(initialScale.x), initialScale.y, initialScale.z);
            lastFacingDir = -1f;
        }
    }

    // ------------------------ Footsteps / Walking Sound ------------------------
    private void UpdateFootsteps()
    {
        bool wantFootsteps = isGrounded && !isDashing && !isAttacking && Mathf.Abs(horizontal) > 0.1f && footstepClip != null;
        if (wantFootsteps && footstepRoutine == null)
            footstepRoutine = StartCoroutine(FootstepRoutine());
        else if (!wantFootsteps && footstepRoutine != null)
        {
            StopCoroutine(footstepRoutine);
            footstepRoutine = null;
        }
    }

    private IEnumerator FootstepRoutine()
    {
        // use assigned AudioSource or try to find one on the GameObject
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null || footstepClip == null) yield break;

        while (true)
        {
            float pitch = Random.Range(footstepPitchMin, footstepPitchMax);
            float oldPitch = audioSource.pitch;
            audioSource.pitch = pitch;
            audioSource.PlayOneShot(footstepClip, footstepVolume);
            audioSource.pitch = oldPitch;

            // Wait while ensuring that we exit quickly when movement stops
            float elapsed = 0f;
            while (elapsed < footstepInterval)
            {
                if (!isGrounded || isDashing || isAttacking || Mathf.Abs(horizontal) <= 0.1f)
                {
                    // stop early if we're no longer moving or eligible for footsteps
                    footstepRoutine = null;
                    yield break;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }

    private void PlayClip(AudioClip clip, float volume)
    {
        if (clip == null) return;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        if (audioSource != null)
            audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        else
            AudioSource.PlayClipAtPoint(clip, transform.position, Mathf.Clamp01(volume));
    }

    // ------------------------ Attack ------------------------
    private void TryAttack()
    {
        if (isAttacking) return;
        if (animator == null)
        {
            return;
        }
        if (!animHasAttackTrigger)
        {
            return;
        }

        if (Time.time < lastAttackTime + attackCooldown)
        {
            return;
        }

        lastAttackTime = Time.time;
        StartCoroutine(DoAttack());
    }

    private void TrySkill1()
    {
        if (isAttacking) return;
        if (animator == null)
        {
            return;
        }
        if (!animHasSkill1Trigger)
        {
            return;
        }

        if (Time.time < lastSkill1Time + skill1Cooldown)
        {
            return;
        }

        lastSkill1Time = Time.time;
        StartCoroutine(DoSkill1());
    }

    private IEnumerator DoAttack()
    {
        isAttacking = true;
        // Play attack sound immediately when attack starts so it occurs even if no enemy is hit
        if (attackClip != null)
        {
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource != null) audioSource.PlayOneShot(attackClip, attackVolume);
        }

        // reset hit state and start fallback if needed
        attackHitApplied = false;
        if (applyAttackCoroutine != null) { StopCoroutine(applyAttackCoroutine); applyAttackCoroutine = null; }
        if (autoApplyAttack)
        {
            applyAttackCoroutine = StartCoroutine(ApplyAttackAtDelay());
        }

        if (animHasAttackBool)
            animator.SetBool(attackBoolParam, true);
        animator.SetTrigger(attackTrigger);

        if (attackDuration > 0f)
        {
            yield return new WaitForSeconds(attackDuration);
        }
        else
        {
            float timeout = 2f;
            float elapsed = 0f;
            while (animator != null && animator.GetCurrentAnimatorStateInfo(0).IsName("attack") && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        if (animHasAttackBool)
            animator.SetBool(attackBoolParam, false);

        // ensure fallback coroutine is stopped
        if (applyAttackCoroutine != null) { StopCoroutine(applyAttackCoroutine); applyAttackCoroutine = null; }

        isAttacking = false;
        onAttackEnd?.Invoke();

    }

    private IEnumerator DoSkill1()
    {
        isAttacking = true;

        if (animHasAttackBool)
            animator.SetBool(attackBoolParam, true);
        if (animHasSkill1Trigger)
            animator.SetTrigger(skill1Trigger);

        if (skill1Duration > 0f)
        {
            yield return new WaitForSeconds(skill1Duration);
        }
        else
        {
            float timeout = 3f;
            float elapsed = 0f;
            while (animator != null && animator.GetCurrentAnimatorStateInfo(0).IsName("skill1") && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        if (animHasAttackBool)
            animator.SetBool(attackBoolParam, false);

        isAttacking = false;
        onSkill1End?.Invoke();
    }
    // Attack events called from Animation Events

    public void OnAttackHit()
    {
        // stop fallback coroutine if running
        if (applyAttackCoroutine != null) { StopCoroutine(applyAttackCoroutine); applyAttackCoroutine = null; }

        // only apply if not already applied by fallback
        if (!attackHitApplied)
            ApplyAttackToEnemies();

        onAttackHit?.Invoke();
    }

    private void ApplyAttackToEnemies()
    {
        if (attackHitRadius <= 0f)
        {
            return;
        }

        // compute center in world space (respect facing)
        float dir = Mathf.Sign(transform.localScale.x == 0 ? 1f : transform.localScale.x);
        Vector2 center = (Vector2)transform.position + new Vector2(attackHitOffset.x * dir, attackHitOffset.y);

        Collider2D[] cols = Physics2D.OverlapCircleAll(center, attackHitRadius, enemyLayer);

        if (attackHitApplied)
            return;

        var hitSet = new System.Collections.Generic.HashSet<Enemy>();
        bool hitAny = false;
        bool attempted = false;
        foreach (var c in cols)
        {
            if (c == null) continue;
            var e = c.GetComponentInParent<Enemy>();
            if (e != null && !hitSet.Contains(e))
            {
                hitSet.Add(e);
                attempted = true;

                // blind check (may miss)
                if (IsBlinded() && Random.value < blindChance)
                {
                    SpawnDamagePopup("Miss", missPopupColor);
                    continue;
                }

                // Determine critical hit
                float critChance = GetEffectiveAttackCritChance();
                float critMult = GetEffectiveAttackCritMultiplier();
                bool isCrit = (critChance > 0f && Random.value < critChance) && e.CanBeCrit;

                if (attackUsesPercent)
                {
                    float basePercent = GetEffectiveAttackPercent();
                    float finalPercent = basePercent * (isCrit ? critMult : 1f);

                    int beforeHp = e.GetCurrentHealth();
                    e.TakePercentDamage(finalPercent, isCrit);
                    int afterHp = e.GetCurrentHealth();
                    int dealt = Mathf.Max(0, beforeHp - afterHp);
                    SpawnDamagePopup(dealt.ToString(), isCrit ? critPopupColor : percentPopupColor);
                }
                else
                {
                    int dmg = GetEffectiveAttackDamage();
                    if (isCrit) dmg = Mathf.Max(1, Mathf.RoundToInt(dmg * critMult));
                    e.TakeDamage(dmg);
                    SpawnDamagePopup(dmg.ToString(), isCrit ? critPopupColor : damagePopupColor);
                }
                hitAny = true;
            }
        }

        if (hitAny || attempted)
        {
            attackHitApplied = true;
        }
    }

    [ContextMenu("Debug Apply Attack")]
    public void DebugApplyAttack() => ApplyAttackToEnemies();

    [ContextMenu("Debug Apply Skill1")]
    public void DebugApplySkill1() => ApplySkill1ToEnemies();

    private void ApplySkill1ToEnemies()
    {
        float effectiveSkill1Radius = GetEffectiveSkill1HitRadius();
        if (effectiveSkill1Radius <= 0f) return;

        float dir = Mathf.Sign(transform.localScale.x == 0 ? 1f : transform.localScale.x);
        Vector2 baseOffset = skill1HitOffset;
        Vector2 effectiveOffset = GetEffectiveSkill1HitOffset();
        Vector2 startCenter = (Vector2)transform.position + new Vector2(baseOffset.x * dir, baseOffset.y);
        Vector2 endCenter = (Vector2)transform.position + new Vector2(effectiveOffset.x * dir, effectiveOffset.y);

        var hitColliders = new System.Collections.Generic.HashSet<Collider2D>();

        // Always include the endpoint impact zone.
        Collider2D[] endCols = Physics2D.OverlapCircleAll(endCenter, effectiveSkill1Radius, enemyLayer);
        foreach (var c in endCols)
            if (c != null) hitColliders.Add(c);

        // Rage Skill1 gets longer forward range; sweep the full segment so targets in the path are also hit.
        Vector2 delta = endCenter - startCenter;
        float sweepDistance = delta.magnitude;
        if (sweepDistance > 0.001f)
        {
            Collider2D[] startCols = Physics2D.OverlapCircleAll(startCenter, effectiveSkill1Radius, enemyLayer);
            foreach (var c in startCols)
                if (c != null) hitColliders.Add(c);

            RaycastHit2D[] sweepHits = Physics2D.CircleCastAll(startCenter, effectiveSkill1Radius, delta.normalized, sweepDistance, enemyLayer);
            foreach (var h in sweepHits)
                if (h.collider != null) hitColliders.Add(h.collider);
        }

        var hitSet = new System.Collections.Generic.HashSet<Enemy>();
        foreach (var c in hitColliders)
        {
            if (c == null) continue;
            var e = c.GetComponentInParent<Enemy>();
            if (e != null && !hitSet.Contains(e))
            {
                hitSet.Add(e);
                // blind check
                if (IsBlinded() && Random.value < blindChance)
                {
                    SpawnDamagePopup("Miss", missPopupColor);
                    continue;
                }
                float critChance = GetEffectiveSkill1CritChance();
                float critMult = GetEffectiveSkill1CritMultiplier();
                bool isCrit = (critChance > 0f && Random.value < critChance) && e.CanBeCrit;
                int dmg = GetEffectiveSkill1Damage();
                if (isCrit) dmg = Mathf.Max(1, Mathf.RoundToInt(dmg * critMult));
                e.TakeDamage(dmg);
                SpawnDamagePopup(dmg.ToString(), isCrit ? critPopupColor : damagePopupColor);
            }
        }
    }

    private void ApplyRageHeavyDamageToEnemies()
    {
        float radius = GetEffectiveRageHeavyDamageHitRadius();
        if (radius <= 0f) return;

        float dir = Mathf.Sign(transform.localScale.x == 0 ? 1f : transform.localScale.x);
        Vector2 center = (Vector2)transform.position + new Vector2(rageHeavyDamageHitOffset.x * dir, rageHeavyDamageHitOffset.y);

        Collider2D[] cols = Physics2D.OverlapCircleAll(center, radius, enemyLayer);
        var hitSet = new System.Collections.Generic.HashSet<Enemy>();

        foreach (var c in cols)
        {
            if (c == null) continue;
            var e = c.GetComponentInParent<Enemy>();
            if (e == null || hitSet.Contains(e)) continue;
            hitSet.Add(e);

            if (IsBlinded() && Random.value < blindChance)
            {
                SpawnDamagePopup("Miss", missPopupColor);
                continue;
            }

            float critChance = GetEffectiveRageHeavyDamageCritChance();
            float critMult = GetEffectiveRageHeavyDamageCritMultiplier();
            bool isCrit = (critChance > 0f && Random.value < critChance) && e.CanBeCrit;

            int dmg = GetEffectiveRageHeavyDamage();
            if (isCrit) dmg = Mathf.Max(1, Mathf.RoundToInt(dmg * critMult));
            e.TakeDamage(dmg);
            SpawnDamagePopup(dmg.ToString(), isCrit ? critPopupColor : damagePopupColor);
        }
    }

    public void EndAttack()
    {

        if (animHasAttackBool)
            animator.SetBool(attackBoolParam, false);
        isAttacking = false;
        onAttackEnd?.Invoke();
    }

    // Skill1 events called from Animation Events
    public void OnSkill1Hit()
    {
        // Play skill1 audio if set
        if (audioSource != null && skill1Clip != null)
        {
            audioSource.PlayOneShot(skill1Clip);
        }

        // Apply damage to enemies in skill hit area
        ApplySkill1ToEnemies();

        onSkill1Hit?.Invoke();
    }

    // Called when the player is hit during rage OR manually via input.
    // This starts the reaction animation; actual outgoing damage is applied by animation event timing.
    private void TriggerRageHeavyDamageReaction()
    {
        if (!isRaging || rageEntryPlaying) return;
        if (isDashing || isAttacking) return;
        if (playerHealth != null && playerHealth.IsDead()) return;
        if (animator == null) return;

        if (Time.time < lastRageHeavyDamageReactionTime + Mathf.Max(0f, rageHeavyDamageReactionCooldown))
            return;

        if (rageHeavyDamageInProgress)
        {
            var current = animator.GetCurrentAnimatorStateInfo(0);
            if (current.IsName(rageHeavyDamageStateName))
                return;
        }

        rageHeavyDamageInProgress = true;
        lastRageHeavyDamageReactionTime = Time.time;
        PlayClip(rageHeavyDamageReactClip, rageHeavyDamageReactVolume);

        if (rageHeavyDamageRecoverRoutine != null)
        {
            StopCoroutine(rageHeavyDamageRecoverRoutine);
            rageHeavyDamageRecoverRoutine = null;
        }

        if (animHasRageHeavyDamageTrigger)
        {
            animator.ResetTrigger(rageHeavyDamageTriggerParam);
            animator.SetTrigger(rageHeavyDamageTriggerParam);
        }
        else if (!string.IsNullOrEmpty(rageHeavyDamageStateName))
            animator.Play(rageHeavyDamageStateName, 0, 0f);

        rageHeavyDamageRecoverRoutine = StartCoroutine(RageHeavyDamageRecoverRoutine());
    }

    // Animation Event: place this on the exact frame where rage heavy-damage should hit.
    public void OnHitRageHeavyDamage()
    {
        if (!isRaging || rageEntryPlaying) return;
        if (!rageHeavyDamageInProgress) return;
        if (playerHealth != null && playerHealth.IsDead()) return;

        PlayClip(rageHeavyDamageImpactClip, rageHeavyDamageImpactVolume);
        ApplyRageHeavyDamageToEnemies();
    }

    private IEnumerator RageHeavyDamageRecoverRoutine()
    {
        float timeout = 2f;
        float elapsed = 0f;

        while (animator != null && elapsed < timeout)
        {
            var st = animator.GetCurrentAnimatorStateInfo(0);
            if (st.IsName(rageHeavyDamageStateName) && st.normalizedTime >= 1f)
                break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        rageHeavyDamageRecoverRoutine = null;
        rageHeavyDamageInProgress = false;
        if (isRaging)
            PlayRageLocomotionState();
    }

    public void EndSkill1()
    {
        if (animHasAttackBool)
            animator.SetBool(attackBoolParam, false);
        isAttacking = false;
        onSkill1End?.Invoke();
    }

    private IEnumerator ApplyAttackAtDelay()
    {
        float wait = Mathf.Max(0.01f, attackHitDelay);
        yield return new WaitForSeconds(wait);

        if (!attackHitApplied)
            ApplyAttackToEnemies();

        applyAttackCoroutine = null;
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            if (useBoxGroundCheck)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(groundCheck.position, groundCheckBoxSize);
            }
            else
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
            }
        }

        // Draw attack hit area
#if UNITY_EDITOR
        if (attackHitRadius > 0f)
        {
            Gizmos.color = Color.red;
            float dir = Mathf.Sign(transform.localScale.x == 0 ? 1f : transform.localScale.x);
            Vector2 center = (Vector2)transform.position + new Vector2(attackHitOffset.x * dir, attackHitOffset.y);
            Gizmos.DrawWireSphere(center, attackHitRadius);
        }
#endif
    }
}
