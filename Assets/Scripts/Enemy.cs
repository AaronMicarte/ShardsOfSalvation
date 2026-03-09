using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Simple enemy behavior:
/// - Detect player within <see cref="detectionRadius" />
/// - Approach player and attack when in <see cref="attackRange" />
/// - Returns to start position when the player leaves
/// This version extracts helpers, caches Animator checks, and keeps public inspector fields minimal.
/// </summary>
[RequireComponent(typeof(Animator))]
public class Enemy : MonoBehaviour
{
    [Header("Target")]
    [SerializeField, Tooltip("Override the player target (optional). If empty, script will search for GameObject tagged 'Player'.")] private Transform target;

    [Header("Movement")]
    [SerializeField] private float detectionRadius = 8f;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField, Tooltip("If true, flip sprite by localScale.x. Otherwise rotate to face target.")] private bool useFacingFlip = true;

    [Header("Home")]
    [SerializeField, Tooltip("Return to spawn position when player leaves detection range")] private bool returnToStart = true;
    [SerializeField, Tooltip("How close (in units) to the start position before stopping")] private float homeThreshold = 0.05f;
    private Vector3 startPosition;

    [Header("Attack")]
    [SerializeField] private float attackCooldown = 1.2f;
    [SerializeField, Tooltip("Animator state name used to consider the enemy 'in attack'.")] private string attackStateName = "attack";
    [SerializeField, Tooltip("If true, choose a random attack variant and set 'attackIndex' int parameter on the Animator. Animator states should be named by concatenating Attack State Base + index (e.g. 'attack-1').")] private bool useAttackVariants = false;
    [SerializeField, Tooltip("Number of attack variants (1 = no variants)")] private int attackVariantCount = 1;
    [SerializeField, Tooltip("When using variants, this base name is prefixed to the variant index to form the state name (default 'attack-').")] private string attackStateBaseName = "attack-";
    private string runtimeAttackStateName = null;
    [SerializeField, Tooltip("If true, enemy attacks once per approach until the player leaves detection range.")] private bool singleAttackPerApproach = false;
    [SerializeField, Tooltip("Seconds to pause movement after performing an attack")] private float attackRecovery = 0.5f;
    [SerializeField, Tooltip("How much HP this enemy removes from the player on hit (used by OnAttackHit)")] private int damageAmount = 1;
    [SerializeField, Tooltip("Maximum seconds to allow the attack animation before forcefully ending the attack (fallback if animation events are missing)")] private float maxAttackDuration = 0;
    [SerializeField, Tooltip("If true, attacks still apply damage even if the player leaves attack range before the hit frame.")] private bool attackHitsOutOfRange = false;

    [System.Serializable]
    public struct AttackVariantConfig
    {
        [Tooltip("Hit radius for this variant. Use <=0 to fallback to default attackHitRadius.")]
        public float hitRadius;
        [Tooltip("If true, use a box (rectangle) for this variant instead of a circle.")]
        public bool useBox;
        [Tooltip("Box size (width=x forward, height=y) used when Use Box is enabled for this variant.")]
        public Vector2 boxSize;
        [Tooltip("Local offset from pivot for this variant. Leave as (0,0) to use default Attack Hit Offset.")]
        public Vector2 hitOffset;
    }

    [SerializeField, Tooltip("Per-variant hit area configs. Index 0 = attack-1, index 1 = attack-2, etc.")]
    private AttackVariantConfig[] attackVariantConfigs;

    [SerializeField, Tooltip("If true, use a box (rectangle) for the attack hit area instead of a circle.")]
    private bool useBoxHitArea = false;
    [SerializeField, Tooltip("Size of the box (width=x, height=y) used when Use Box Hit Area is enabled.")]
    private Vector2 attackHitBoxSize = new Vector2(1.6f, 0.8f);
    [SerializeField, Tooltip("If true, freeze movement while the Animator 'isAttacking' is true (useful for lasers).")]
    private bool lockMovementWhileAttacking = true;
    private bool movementLockedByAttack = false;

    [Header("Hit Area")]
    [SerializeField, Tooltip("Local offset from the enemy pivot where attacks originate (X is forward). This is relative to the enemy transform and will be flipped by facing when using facing flip.")] private Vector2 attackHitOffset = new Vector2(0.6f, 0f);
    [SerializeField, Tooltip("Radius (world units) of the attack hit area used to detect valid targets")] private float attackHitRadius = 0.6f;
    [SerializeField, Tooltip("Layer mask used to detect valid attack targets (set to Player layer)")] private LayerMask attackTargetMask = 0;

    [Header("AI Behavior")]
    [SerializeField, Tooltip("If true, enemy will retreat after each attack for a short duration.")] private bool retreatAfterAttack = true;
    [SerializeField, Tooltip("Seconds to retreat after an attack finishes.")] private float retreatDuration = 1.4f;
    [SerializeField, Tooltip("Distance to try to keep from the player while retreating.")] private float retreatDistance = 3f;

    [Header("AI - Dash")]
    [SerializeField, Tooltip("If true, enemy can dash briefly when retreating after a missed attack.")] private bool allowDash = false;
    [SerializeField, Tooltip("Seconds the dash lasts when triggered.")] private float dashDuration = 0.25f;
    [SerializeField, Tooltip("Speed multiplier applied while dashing.")] private float dashSpeedMultiplier = 2.5f;

    [Header("AI - Low HP Kiting")]
    [SerializeField, Tooltip("If true, enemy will kite away when its own health is low.")] private bool kiteWhenLowHP = true;
    [SerializeField, Range(0f, 1f), Tooltip("Health percent (0..1) at which kiting starts.")] private float lowHPThreshold = 0.3f;
    [SerializeField, Tooltip("Preferred distance to keep from player while kiting.")] private float kiteDistance = 4f;

    [Header("AI - Player HP Kiting")]
    [SerializeField, Tooltip("If true, enemy will kite when the player's health falls below the threshold.")] private bool kiteWhenPlayerLowHP = true;
    [SerializeField, Range(0f, 1f), Tooltip("Player health percent (0..1) below which the enemy will prefer kiting.")] private float playerLowHPThreshold = 0.5f;

    [Header("AI - Hit Kiting")]
    [SerializeField, Tooltip("If true, enemy will briefly kite after a successful hit.")] private bool kiteAfterHit = true;
    [SerializeField, Tooltip("Seconds to kite after a successful hit.")] private float kiteAfterHitDuration = 0.6f;
    [SerializeField, Tooltip("When a kite event (hit or retreat) is triggered, how long (seconds) the enemy will kite before re-evaluating and chasing again. Set to 0 to disable timed kite.")] private float kiteBurstDuration = 1.0f;
    // Internal: time until which the enemy should remain in timed-kite mode
    private float kiteUntilTime = -Mathf.Infinity;

    [Header("AI - Line of Sight")]
    [SerializeField, Tooltip("If true, enemy only chases/attacks when it has line of sight to the player.")] private bool useLineOfSight = true;
    [SerializeField, Tooltip("Layers that block line of sight (e.g., Walls/Ground).")]
    private LayerMask lineOfSightBlockingMask;

    [Header("AI - Aggro")]
    [SerializeField, Tooltip("How long (seconds) the enemy remembers the player after last seen.")] private float aggroDuration = 3f;

    [Header("Defense")]
    [SerializeField, Tooltip("Multiplier applied to incoming damage (1 = normal, 0.5 = half damage, 2 = double damage)")] private float damageTakenMultiplier = 0.75f; // default: take 25% less damage
    [SerializeField, Tooltip("Armor value (Dota-style). Higher armor reduces damage; negative armor increases damage.")] private float armor = 0f;
    [SerializeField, Tooltip("If true, this enemy can receive critical hits (player criticals will apply)")] private bool canBeCrit = true;

    // Simple public accessors for other systems
    public bool CanBeCrit => canBeCrit;
    public float DamageTakenMultiplier => damageTakenMultiplier;
    public float Armor => armor;

    [Header("Events")]
    [SerializeField, Tooltip("Invoke on the animation frame where the attack should hit.")] private UnityEvent onAttackHit;
    [SerializeField, Tooltip("Invoke when the attack animation ends.")] private UnityEvent onAttackEnd;

    [Header("Health")]
    [SerializeField, Tooltip("Maximum health for this enemy")]
    private int maxHealth = 100; // default standardized to 100 for all enemies
    [SerializeField, Tooltip("Optional VFX to spawn on death")] private GameObject deathVFX;
    [SerializeField, Tooltip("Optional SFX to play on death")] private AudioClip deathClip;
    [SerializeField, Tooltip("Death SFX volume"), Range(0f, 1f)] private float deathVolume = 1f;

    [Header("Audio")]
    [SerializeField, Tooltip("AudioSource for enemy SFX (optional)")] private AudioSource audioSource;
    [SerializeField, Tooltip("Audio clips for attack variants (index 0 = attack-1)")] private AudioClip[] attackClips;
    [SerializeField, Range(0f, 1f), Tooltip("Volume for attack SFX")] private float attackVolume = 1f;
    [SerializeField, Tooltip("If true, attack SFX will only be played via animation events (prevents duplicate sounds)")] private bool attackSfxViaAnimationEvents = false;
    [SerializeField, Tooltip("If true, death SFX will only be played via animation events (prevents duplicate sounds)")] private bool deathSfxViaAnimationEvents = false;
    [SerializeField, Tooltip("If true, destroy the GameObject when dead")] private bool destroyOnDeath = true;
    [SerializeField, Tooltip("Invoked when the enemy dies")] private UnityEvent onDeath;

    [Header("Death Animation")]
    [SerializeField, Tooltip("Animator trigger parameter used to start the death animation (optional)")] private string deathTrigger = "Dead";
    [SerializeField, Tooltip("Animator state name to wait for during death (optional)")] private string deathStateName = "dead";
    [SerializeField, Tooltip("If true, wait for the death animation to finish before cleaning up (otherwise cleanup happens immediately)")] private bool waitForDeathAnimation = true;
    [SerializeField, Tooltip("Animator state name for idle; used when forcing animations on spawn or recovery")] private string idleStateName = "idle";
    [SerializeField, Tooltip("If true, log animator state issues (entering 'dead' while not flagged dead)")] private bool logAnimatorStateIssues = true;

    [Header("HP Bar")]
    [SerializeField, Tooltip("Prefab for the floating HP bar (assign HPBar prefab)")] private HPBarSprite hpBarPrefab;
    [SerializeField, Tooltip("Local vertical offset above sprite top")] private float hpBarOffsetY = 0.08f;
    private HPBarSprite hpBarInstance;

    [Header("Death Physics")]
    [SerializeField, Tooltip("If true, make the corpse pass-through (turn colliders into triggers or disable them)")] private bool makeCorpsePassThrough = true;
    [SerializeField, Tooltip("If true, set colliders to isTrigger; if false, disable colliders entirely")] private bool setTriggerOnDeath = true;

    [Header("Reinforcements")]
    [SerializeField, Tooltip("If true, try to spawn reinforcements when this enemy dies.")] private bool spawnReinforcementsOnDeath = true;
    [SerializeField, Tooltip("Chance (0..1) to spawn reinforcements when this enemy dies.")] private float reinforcementChance = 0.15f;
    [SerializeField, Tooltip("How many reinforcements to spawn when triggered.")] private int reinforcementCount = 1;
    [SerializeField, Tooltip("Delay between spawning each reinforcement.")] private float reinforcementDelay = 0.15f;
    [SerializeField, Tooltip("Optional spawner to use. If left empty, no reinforcements will spawn.")] private ReinforcementSpawner reinforcementSpawner = null;
    [SerializeField, Tooltip("If true, logs reinforcement spawn decisions to the Console for debugging.")] private bool reinforcementDebug = false;

    [Header("Loot - Shard Drop")]
    [SerializeField, Tooltip("If true, spawn a shard pickup when this enemy dies (enable this on boss only).")]
    private bool dropShardOnDeath = false;
    [SerializeField, Tooltip("Prefab to spawn for the shard pickup (should have ShardPickup component).")]
    private GameObject shardPickupPrefab = null;
    [SerializeField, Tooltip("Extra offset from the enemy's right edge where the shard spawns. +X is world-right.")]
    private Vector2 shardDropOffset = new Vector2(0.35f, 0f);

    [Header("Stage 5 Finale")]
    [SerializeField, Tooltip("If true, this enemy is treated as the Stage 5 final boss for finale flow.")]
    private bool isStage5FinalBoss = false;
    [SerializeField, Tooltip("If true, mute configured BGM audio sources immediately when this enemy dies.")]
    private bool muteBgmOnDeath = true;
    [SerializeField, Tooltip("Seconds to fade out Stage 5 BGM after final boss death (0 = instant mute).")]
    private float bgmFadeOutDuration = 0.5f;
    [SerializeField, Tooltip("Optional BGM sources to mute. If empty, script mutes looping scene AudioSources.")]
    private AudioSource[] bgmSourcesToMute;

    [Header("Ranged")]
    [SerializeField, Tooltip("Projectile prefab to spawn for ranged attacks (assign Arrow Projectile prefab)")] private GameObject rangedProjectilePrefab = null;
    [SerializeField, Tooltip("Spawn transform (child) where projectiles originate")] private Transform projectileSpawnPoint = null;
    [SerializeField, Tooltip("If true, automatically fire the projectile when the attack animation ends")] private bool fireProjectileOnAttackEnd = false;
    [SerializeField, Tooltip("Forward offset to avoid colliding with the shooter")] private float projectileSpawnOffset = 0.12f;

    // runtime health
    private int currentHealth;
    private bool isDead = false;
    // spawn-time health override (used by spawner) - set to >0 to override max/current on Start or immediately via method
    private int spawnHPOverride = -1;
    private bool started = false;

    // Fallback settings (useful when animation events are not set up)
    [SerializeField, Tooltip("Delay (seconds after attack start) to auto-apply damage if animation event is missing (0 = disabled)")] private float attackHitDelay = 0.15f;
    [SerializeField, Tooltip("Automatically apply damage after AttackHitDelay when animation events are missing")] private bool autoApplyDamage = true;
    [SerializeField, Tooltip("If true, allow fallback damage to apply even if the Animator never entered the attack state (not recommended)")] private bool allowFallbackIfAnimNotStarted = false;
    [SerializeField, Tooltip("Use normalized clip time (0..1) to determine hit timing instead of raw seconds")] private bool useNormalizedHitTime = true;
    [SerializeField, Range(0f, 1f), Tooltip("Normalized time within attack clip to apply hit (0 = start, 1 = end). Used when Use Normalized Hit Time is enabled.")] private float attackHitNormalizedTime = 0.25f;

    // --- runtime state ---
    private Animator anim;
    private float lastAttackTime = -Mathf.Infinity;
    private bool isRecovering = false;
    private bool hasAttackedThisApproach = false;
    private bool hitAppliedThisAttack = false;
    // Index of the variant chosen for the current attack (1-based for variants, 0 = no variant)
    private int runtimeAttackIndex = 0;

    // Cached reference to the target's health component (if any)
    private PlayerHealth targetHealth = null;

    // runtime coroutine references for the attack watchdog and pending hit
    private Coroutine attackWatcherCoroutine = null;
    private Coroutine applyDamageCoroutine = null;

    // retreat / hit-kite state
    private float retreatUntilTime = -Mathf.Infinity;
    private float dashUntilTime = -Mathf.Infinity;
    private float kiteAfterHitUntilTime = -Mathf.Infinity;
    private float lastAggroTime = -Mathf.Infinity;

    // reinforcement spawn guard (avoid double spawn when death animation waits)
    private bool reinforcementSpawned = false;
    // shard drop guard (avoid duplicate drop from multiple cleanup paths)
    private bool shardDropSpawned = false;

    void Start()
    {
        anim = GetComponent<Animator>();
        startPosition = transform.position;

        // Apply spawn override if present, otherwise initialize health from max
        if (spawnHPOverride > 0)
        {
            maxHealth = spawnHPOverride;
            currentHealth = maxHealth;
            spawnHPOverride = -1; // consume
        }
        else
        {
            currentHealth = maxHealth;
        }
        started = true;

        if (target == null)
        {
            var found = GameObject.FindWithTag("Player");
            if (found != null) target = found.transform;
        }

        if (target != null)
            targetHealth = target.GetComponent<PlayerHealth>();

        // Auto-assign the attackTargetMask to the Player layer if the inspector left it unset
        if (attackTargetMask == 0)
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0)
                attackTargetMask = 1 << playerLayer;
        }

        // Auto-assign projectile spawn point if not assigned (looks for a child named 'ProjectileSpawn' or similar)
        if (projectileSpawnPoint == null)
        {
            Transform foundTransform = null;
            foundTransform = transform.Find("ProjectileSpawn") ?? transform.Find("ProjectileSpawnPoint");
            if (foundTransform == null)
            {
                var all = GetComponentsInChildren<Transform>(true);
                foreach (var t in all)
                {
                    var n = t.name.ToLower();
                    if (n.Contains("projectile") || n.Contains("projectilespawn") || n.Contains("spawnpoint") || n.Contains("spawn"))
                    {
                        foundTransform = t;
                        break;
                    }
                }
            }
            if (foundTransform != null)
            {
                projectileSpawnPoint = foundTransform;
                Debug.Log($"{name}: Auto-assigned projectileSpawnPoint = {projectileSpawnPoint.name}");
            }
            else
            {
                Debug.LogWarning($"{name}: projectileSpawnPoint is not assigned. Create a child named 'ProjectileSpawn' or assign it in the Inspector.");
            }
        }

        // Auto-assign a projectile prefab if missing by finding a child ArrowProjectile or a Resources asset named 'ArrowProjectile'
        if (rangedProjectilePrefab == null)
        {
            var childProj = GetComponentInChildren<ArrowProjectile>(true);
            if (childProj != null)
            {
                rangedProjectilePrefab = childProj.gameObject;
                Debug.Log($"{name}: Auto-assigned rangedProjectilePrefab from child '{rangedProjectilePrefab.name}'. Consider assigning a proper projectile prefab asset instead.");
            }
            else
            {
                var res = Resources.Load<GameObject>("ArrowProjectile");
                if (res != null)
                {
                    rangedProjectilePrefab = res;
                    Debug.Log($"{name}: Auto-assigned rangedProjectilePrefab from Resources/ArrowProjectile.");
                }
                else
                {
                    Debug.LogWarning($"{name}: rangedProjectilePrefab is not assigned. Assign your Arrow projectile prefab in the Inspector or place it in Resources/ArrowProjectile to auto-assign.");
                }
            }
        }

        // Create or reuse a floating HP bar instance (if configured)
        if (hpBarPrefab != null)
        {
            var sr = GetComponent<SpriteRenderer>();

            // If there's already an HPBarSprite in children (e.g., placed in prefab), reuse it
            if (hpBarInstance == null)
            {
                var existing = GetComponentInChildren<HPBarSprite>(true);
                if (existing != null)
                {
                    hpBarInstance = existing;
                    // Parent to this transform and preserve world position
                    hpBarInstance.transform.SetParent(transform, worldPositionStays: true);
                }
                else
                {
                    hpBarInstance = Instantiate(hpBarPrefab);
                    hpBarInstance.transform.SetParent(transform, worldPositionStays: true);
                }
            }

            // Position the hp bar above the sprite top in world space when possible
            if (sr != null)
            {
                Vector3 topWorld = new Vector3(transform.position.x, sr.bounds.max.y, transform.position.z);
                Vector3 worldPos = topWorld + Vector3.up * hpBarOffsetY; // hpBarOffsetY treated as world units above sprite top
                hpBarInstance.transform.position = worldPos;
            }
            else
            {
                // Fallback: set local position
                hpBarInstance.transform.localPosition = new Vector3(0f, hpBarOffsetY, 0f);
            }

            // Enforce sensible defaults to avoid huge UI scale issues or mis-rotation
            hpBarInstance.transform.localRotation = Quaternion.identity;
            if (hpBarInstance.transform.localScale == Vector3.one) // only override default unity scale
                hpBarInstance.transform.localScale = Vector3.one * 0.01f;

            // Use SetMaxHP helper so prefab implementations can react correctly
            hpBarInstance.SetMaxHP(maxHealth);
            hpBarInstance.SetHP(currentHealth);
        }

        // Apply any pending spawn HP override if set before Start() completed
        if (spawnHPOverride > 0)
        {
            maxHealth = spawnHPOverride;
            currentHealth = maxHealth;
            spawnHPOverride = -1;

            if (hpBarInstance != null)
            {
                hpBarInstance.SetMaxHP(maxHealth);
                hpBarInstance.SetHP(currentHealth);
            }
        }

        started = true;
    }

    void OnValidate()
    {
        const float eps = 0.0001f;
        if (!useBoxHitArea && attackHitRadius <= eps)
        {
            Debug.LogWarning($"{name}: attackHitRadius is zero and Use Box Hit Area is disabled — attacks may miss.");
        }

        if (useBoxHitArea && (attackHitBoxSize.x <= eps || attackHitBoxSize.y <= eps))
        {
            Debug.LogWarning($"{name}: attackHitBoxSize has zero dimension; set a positive size or disable Use Box Hit Area.");
        }

        if (attackVariantConfigs != null)
        {
            for (int i = 0; i < attackVariantConfigs.Length; i++)
            {
                var c = attackVariantConfigs[i];
                if (c.useBox && c.boxSize.sqrMagnitude <= eps)
                    Debug.LogWarning($"{name}: attackVariantConfigs[{i}] uses box but boxSize is zero — it will fall back at runtime.");
                if (!c.useBox && c.hitRadius <= eps)
                    Debug.LogWarning($"{name}: attackVariantConfigs[{i}] hitRadius is zero — it will fall back to default radius at runtime.");
            }
        }
    }

    /// <summary>
    /// Reset runtime state so the enemy behaves correctly immediately after being spawned by a spawner.
    /// </summary>
    public void ResetForSpawn()
    {
        // Clear death state
        isDead = false;

        // Reset death trigger if present
        if (anim != null && !string.IsNullOrEmpty(deathTrigger) && AnimatorHasParameter(deathTrigger))
        {
            try { anim.ResetTrigger(deathTrigger); } catch { }
        }

        // Stop any ongoing coroutines related to attacks
        if (attackWatcherCoroutine != null) { StopCoroutine(attackWatcherCoroutine); attackWatcherCoroutine = null; }
        if (applyDamageCoroutine != null) { StopCoroutine(applyDamageCoroutine); applyDamageCoroutine = null; }

        // Ensure dashing is disabled for freshly spawned clones
        allowDash = false;
        dashUntilTime = -Mathf.Infinity; // clear any pending dash timer


        // Reset combat state
        isRecovering = false;
        hasAttackedThisApproach = false;
        hitAppliedThisAttack = false;
        runtimeAttackIndex = 0;
        runtimeAttackStateName = null;
        lastAttackTime = -Mathf.Infinity;

        // Reset health if a spawn override was provided; otherwise ensure current isn't <= 0
        if (spawnHPOverride > 0)
        {
            maxHealth = spawnHPOverride;
            currentHealth = maxHealth;
            spawnHPOverride = -1;
        }
        else if (currentHealth <= 0)
        {
            currentHealth = Mathf.Max(1, maxHealth);
        }

        // Re-enable components
        enabled = true;
        var cols = GetComponentsInChildren<Collider2D>();
        foreach (var c in cols)
        {
            if (c == null) continue;
            c.enabled = true;
            // keep collider trigger state as-is; we don't force-isTrigger here
        }
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.simulated = true;

        // Reset HPBar
        if (hpBarInstance != null)
        {
            hpBarInstance.gameObject.SetActive(true);
            hpBarInstance.SetMaxHP(maxHealth);
            hpBarInstance.SetHP(currentHealth);
        }

        // Reset animator parameters to idle
        if (anim != null)
        {
            anim.enabled = true;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.updateMode = AnimatorUpdateMode.Normal;
            anim.speed = 1f;
            try { anim.ResetTrigger("Attack"); } catch { }
            try { anim.SetBool("isAttacking", false); anim.SetFloat("Speed", 0f); } catch { }
            try { anim.Play(idleStateName, 0, 0f); anim.Update(0f); } catch { }
        }
    }

    /// <summary>
    /// Called by external systems (spawners) to initialize an enemy on spawn.
    /// If called before Start(), the value will be applied at Start().
    /// </summary>
    public void InitializeSpawnHP(int hp)
    {
        hp = Mathf.Max(1, hp);
        spawnHPOverride = hp;
        if (started)
        {
            maxHealth = spawnHPOverride;
            currentHealth = maxHealth;
            spawnHPOverride = -1;

            if (hpBarInstance != null)
            {
                hpBarInstance.SetMaxHP(maxHealth);
                hpBarInstance.SetHP(currentHealth);
            }
        }
    }

    /// <summary>
    /// Forcefully reset animator parameters (bools/ints/floats/triggers) to neutral values and play idle.
    /// Useful to ensure spawned clones show correct idle/walk/attack cycles instead of dead frames.
    /// </summary>
    public void ForceResetAnimator()
    {
        if (anim == null) anim = GetComponent<Animator>();
        if (anim == null) return;

        anim.enabled = true;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        anim.updateMode = AnimatorUpdateMode.Normal;
        anim.speed = 1f;

        foreach (var p in anim.parameters)
        {
            try
            {
                switch (p.type)
                {
                    case AnimatorControllerParameterType.Bool: anim.SetBool(p.name, false); break;
                    case AnimatorControllerParameterType.Int: anim.SetInteger(p.name, 0); break;
                    case AnimatorControllerParameterType.Float: anim.SetFloat(p.name, 0f); break;
                    case AnimatorControllerParameterType.Trigger: anim.ResetTrigger(p.name); break;
                }
            }
            catch { }
        }

        try { anim.Play(idleStateName, 0, 0f); anim.Update(0f); } catch { }
    }

    // Public helpers for external systems
    public bool IsDead() => isDead;
    public string DeathStateName => deathStateName;
    public string IdleStateName => idleStateName;

    void Update()
    {
        if (isDead) return; // stop all behavior when dead

        if (lockMovementWhileAttacking && movementLockedByAttack)
        {
            SetSpeed(0f);
            return;
        }

        // Detect animator jumping into 'dead' state unexpectedly and correct it (helps spawned clones)
        if (anim != null && !isDead && !string.IsNullOrEmpty(deathStateName))
        {
            var state = anim.GetCurrentAnimatorStateInfo(0);
            if (state.IsName(deathStateName))
            {
                if (logAnimatorStateIssues) Debug.LogWarning($"Enemy '{name}' animator entered '{deathStateName}' while not dead - forcing '{idleStateName}' state.");
                try { anim.Play(idleStateName, 0, 0f); } catch { }
            }
        }

        if (target == null) return;

        // Ensure we have a cached reference to PlayerHealth (in case target was assigned in inspector/runtime)
        if (targetHealth == null && target != null)
            targetHealth = target.GetComponent<PlayerHealth>();

        float dist = Vector3.Distance(transform.position, target.position);

        bool hasLOS = HasLineOfSight();
        if (dist <= detectionRadius && hasLOS)
            lastAggroTime = Time.time;

        bool isAggro = Time.time - lastAggroTime <= aggroDuration;

        if (!isAggro)
        {
            ResetApproachState();
            if (returnToStart) ReturnHome();
            else SetIdle();
            return;
        }

        if (useLineOfSight && !hasLOS)
        {
            // Lost sight: pause or return home while aggro timer runs out
            if (returnToStart) ReturnHome();
            else SetIdle();
            return;
        }

        if (dist <= detectionRadius)
        {
            // keep guard; we reset when player leaves
            if (IsKiting())
                KiteFromTarget();
            else if (IsRetreating())
                RetreatFromTarget();
            else
                HandleApproachOrAttack(dist);
        }
        else
        {
            // Player left detection range
            // If we're currently attacking or recovering, allow the attack to complete
            if (isRecovering || (anim != null && anim.GetBool("isAttacking")))
            {
                // Stay in place and don't reset attack state; the animation events (EndAttack) will clear the attack
                SetSpeed(0f);
                return;
            }

            ResetApproachState();
            if (returnToStart) ReturnHome();
            else SetIdle();
        }
    }

    private void HandleApproachOrAttack(float distanceToTarget)
    {
        // If we're in the middle of an attack or recovery, don't cancel the attack animation
        if (isRecovering || (anim != null && anim.GetBool("isAttacking")))
        {
            SetSpeed(0f);
            return;
        }

        if (IsRetreating())
        {
            RetreatFromTarget();
            return;
        }

        if (distanceToTarget > attackRange)
        {
            ApproachTarget();
        }
        else
        {
            TryStartAttack();
        }
    }

    private void ApproachTarget()
    {
        if (isRecovering)
        {
            SetIdle();
            return;
        }

        transform.position = Vector3.MoveTowards(transform.position, target.position, moveSpeed * Time.deltaTime);
        SetSpeed(1f);
        if (useFacingFlip) FlipTowards(target.position);
        else FaceTowards(target.position);
        if (anim != null && anim.GetBool("isAttacking")) return;
        anim.SetBool("isAttacking", false);
    }

    private void TryStartAttack()
    {
        SetSpeed(0f);
        bool animInAttack = AnimIsInAttackState();

        if (Time.time < lastAttackTime + attackCooldown) return;
        if (isRecovering) return;
        if (animInAttack) return;
        if (singleAttackPerApproach && hasAttackedThisApproach) return;

        // Choose variant (if enabled)
        if (useAttackVariants && attackVariantCount > 1)
        {
            int chosen = Random.Range(1, attackVariantCount + 1); // 1-based
            runtimeAttackStateName = attackStateBaseName + chosen;
            runtimeAttackIndex = chosen; // remember which variant we chose
            if (anim != null) anim.SetInteger("attackIndex", chosen);
        }
        else
        {
            runtimeAttackStateName = attackStateName;
            runtimeAttackIndex = 0; // no variant selected
            if (anim != null) anim.SetInteger("attackIndex", 0);
        }

        // Face the current target immediately so the attack animation is oriented correctly
        if (target != null)
        {
            if (useFacingFlip) FlipTowards(target.position);
            else FaceTowards(target.position);
        }

        lastAttackTime = Time.time;
        anim.SetBool("isAttacking", true);
        movementLockedByAttack = true; // lock movement from the Attack trigger onward
        anim.SetTrigger("Attack");

#if UNITY_EDITOR
        // Debug helper: print effective hit area used by this attack variant for tuning
        {
            bool effUseBox = useBoxHitArea;
            float effRadius = attackHitRadius;
            Vector2 effBoxSize = attackHitBoxSize;
            Vector2 effOffset = attackHitOffset;

            if (runtimeAttackIndex > 0)
            {
                int idx = runtimeAttackIndex - 1;
                if (attackVariantConfigs != null && idx < attackVariantConfigs.Length)
                {
                    var cfg = attackVariantConfigs[idx];
                    if (cfg.hitOffset != Vector2.zero) effOffset = cfg.hitOffset;
                    if (cfg.hitRadius > 0f) effRadius = cfg.hitRadius;
                    if (cfg.useBox) effUseBox = true;
                    if (cfg.boxSize.sqrMagnitude > 0.0001f) effBoxSize = cfg.boxSize;
                }
            }

            if (effUseBox)
                Debug.Log($"Enemy '{name}' Attack {runtimeAttackIndex}: boxSize={effBoxSize} offset={effOffset}");
            else
                Debug.Log($"Enemy '{name}' Attack {runtimeAttackIndex}: radius={effRadius} offset={effOffset}");
        }
#endif


        // Determine which clip would play for this attack variant.
        AudioClip clipToPlay = null;
        if (runtimeAttackIndex > 0 && attackClips != null && runtimeAttackIndex <= attackClips.Length)
            clipToPlay = attackClips[runtimeAttackIndex - 1];
        else if (attackClips != null && attackClips.Length > 0)
            clipToPlay = attackClips[0];

        // If configured to let animation events control SFX, skip code playback to avoid double sounds.
        if (!attackSfxViaAnimationEvents)
        {
            // If this is variant 1, defer the SFX until the hit frame (OnAttackHit). Otherwise play immediately.
            if (clipToPlay != null && runtimeAttackIndex != 1)
            {
                if (audioSource != null)
                    audioSource.PlayOneShot(clipToPlay, attackVolume);
                else
                    AudioSource.PlayClipAtPoint(clipToPlay, transform.position, attackVolume);
                Debug.Log($"Enemy '{name}' played attack SFX (variant={(runtimeAttackIndex > 0 ? runtimeAttackIndex.ToString() : "default")}).");
            }
            else if (runtimeAttackIndex == 1)
            {
                Debug.Log($"Enemy '{name}' will play attack-1 SFX on hit (OnAttackHit).");
            }
        }
        else
        {
            // Developer opted to use animation events for attack SFX timing; log intent for debugging.
            Debug.Log($"Enemy '{name}' will use animation events for attack SFX (variant={(runtimeAttackIndex > 0 ? runtimeAttackIndex.ToString() : "default")}).");
        }

        if (singleAttackPerApproach) hasAttackedThisApproach = true;
        isRecovering = true;
        hitAppliedThisAttack = false;

        // Start recovery timer (movement pause) and a watchdog to force end the attack if needed
        StartCoroutine(AttackRecovery());

        if (attackWatcherCoroutine != null) StopCoroutine(attackWatcherCoroutine);
        attackWatcherCoroutine = StartCoroutine(AttackWatcher());

        // If enabled, apply damage after a short delay as a graceful fallback (useful when animation events are missing)
        if (autoApplyDamage)
        {
            if (applyDamageCoroutine != null) StopCoroutine(applyDamageCoroutine);
            applyDamageCoroutine = StartCoroutine(ApplyDamageAtHitTime());
        }
    }

    private string GetActiveAttackStateName() => string.IsNullOrEmpty(runtimeAttackStateName) ? attackStateName : runtimeAttackStateName;

    private bool AnimIsInAttackState()
    {
        if (anim == null) return false;
        return anim.GetCurrentAnimatorStateInfo(0).IsName(GetActiveAttackStateName());
    }

    private void ResetApproachState()
    {
        anim.SetBool("isAttacking", false);
        hasAttackedThisApproach = false;
    }

    private void SetIdle() => SetSpeed(0f);
    private void SetSpeed(float value)
    {
        if (anim != null) anim.SetFloat("Speed", value);
    }

    private void FlipTowards(Vector3 worldPos)
    {
        var localScale = transform.localScale;
        localScale.x = Mathf.Sign(worldPos.x - transform.position.x) * Mathf.Abs(localScale.x);
        transform.localScale = localScale;
    }

    private void FaceTowards(Vector3 worldPos)
    {
        var dir = worldPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);
    }

    // Called from Animation Event (attack start frame)
    public void OnAttackStart()
    {
        Debug.Log($"Enemy '{name}' OnAttackStart called (attack {runtimeAttackStateName ?? attackStateName}).");
        // Ensure we face the player at the start of the attack (useful if target moved behind during windup)
        if (target != null)
        {
            if (useFacingFlip) FlipTowards(target.position);
            else FaceTowards(target.position);
        }
        // Useful hook for SFX/VFX or debugging.
    }

    // Called from Animation Event (attack hit frame)
    public void OnAttackHit()
    {
        // If this attack used variant 1, play its SFX at the hit frame (so sound coincides with impact)
        if (runtimeAttackIndex == 1 && attackClips != null && attackClips.Length >= 1)
        {
            var clip = attackClips[0];
            if (clip != null)
            {
                if (!attackSfxViaAnimationEvents)
                {
                    if (audioSource != null) audioSource.PlayOneShot(clip, attackVolume);
                    else AudioSource.PlayClipAtPoint(clip, transform.position, attackVolume);
                    Debug.Log($"Enemy '{name}' played attack-1 SFX on hit.");
                }
                else
                {
                    Debug.Log($"Enemy '{name}' attack-1 SFX suppressed (played by animation event instead).");
                }
            }
        }

        ApplyDamage();
    }

    /// <summary>
    /// Animation event friendly: play the SFX for the currently selected attack variant (or default attack sound).
    /// </summary>
    public void PlayCurrentAttackSfx()
    {
        AudioClip clipToPlay = null;
        if (runtimeAttackIndex > 0 && attackClips != null && runtimeAttackIndex <= attackClips.Length)
            clipToPlay = attackClips[runtimeAttackIndex - 1];
        else if (attackClips != null && attackClips.Length > 0)
            clipToPlay = attackClips[0];

        PlaySfxClip(clipToPlay, attackVolume);
    }

    /// <summary>
    /// Animation event friendly: play attack SFX for a specific variant (1-based index).
    /// Example: add an Animation Event with integer parameter '1' to play attack-1 SFX.
    /// </summary>
    public void PlayAttackVariantSfx(int variantIndex)
    {
        if (attackClips == null || variantIndex <= 0 || variantIndex > attackClips.Length) return;
        PlaySfxClip(attackClips[variantIndex - 1], attackVolume);
    }

    /// <summary>
    /// Animation event friendly: play the configured death SFX at the current position.
    /// </summary>
    public void PlayDeathSfx()
    {
        PlaySfxClip(deathClip, deathVolume);
    }

    private void PlaySfxClip(AudioClip clip, float vol)
    {
        if (clip == null) return;
        if (audioSource != null) audioSource.PlayOneShot(clip, vol);
        else AudioSource.PlayClipAtPoint(clip, transform.position, vol);
    }

    // Called from Animation Event (attack end)
    public void EndAttack()
    {
        bool hitThisAttack = hitAppliedThisAttack;

        // Stop the watchdog (if running) — the animation properly ending should clear the state
        if (attackWatcherCoroutine != null) { StopCoroutine(attackWatcherCoroutine); attackWatcherCoroutine = null; }
        if (applyDamageCoroutine != null) { StopCoroutine(applyDamageCoroutine); applyDamageCoroutine = null; }

        anim.SetBool("isAttacking", false);
        movementLockedByAttack = false;
        isRecovering = false;
        hitAppliedThisAttack = false;
        runtimeAttackStateName = null;
        runtimeAttackIndex = 0; // clear chosen variant
        SetIdle();
        onAttackEnd?.Invoke();

        // Fire ranged projectile on attack end if configured
        if (fireProjectileOnAttackEnd)
        {
            Debug.Log($"{name}: EndAttack triggered. fireProjectileOnAttackEnd={fireProjectileOnAttackEnd}, rangedProjectilePrefab={(rangedProjectilePrefab != null ? rangedProjectilePrefab.name : "null")}, projectileSpawnPoint={(projectileSpawnPoint != null ? projectileSpawnPoint.name : "null")}");
            if (rangedProjectilePrefab != null) SpawnRangedProjectile();
            else Debug.LogWarning($"{name}: fireProjectileOnAttackEnd is set but rangedProjectilePrefab is not assigned.");
        }

        // Retreat only if the attack missed; if it hit, keep pressure and continue attacking.
        if (retreatAfterAttack && !hitThisAttack)
        {
            retreatUntilTime = Time.time + retreatDuration;
            if (allowDash) dashUntilTime = Time.time + dashDuration;

            // Start running (retreat) immediately so the enemy looks like it runs after
            // finishing its attack animation. Do NOT change facing here so the enemy keeps
            // its previous facing direction.
            SetSpeed(1f);
        }
    }

    /// <summary>
    /// Spawn and fire the configured ranged projectile from the spawn point. Safe to call from Animation Events or code.
    /// </summary>
    public void SpawnRangedProjectile()
    {
        Debug.Log($"{name}: SpawnRangedProjectile called. prefab={(rangedProjectilePrefab != null ? rangedProjectilePrefab.name : "null")}, spawnPoint={(projectileSpawnPoint != null ? projectileSpawnPoint.name : "null")}, target={(target != null ? target.name : "null")}");
        if (rangedProjectilePrefab == null)
        {
            Debug.LogWarning($"{name}: No rangedProjectilePrefab assigned.");
            return;
        }

        Vector2 spawnPos = projectileSpawnPoint != null ? (Vector2)projectileSpawnPoint.position : (Vector2)transform.position;
        Vector2 dir;
        if (target != null)
            dir = ((Vector2)target.position - spawnPos).normalized;
        else
            dir = transform.right * Mathf.Sign(transform.localScale.x);

        // move spawn point slightly forward to avoid hitting the shooter
        spawnPos += dir * projectileSpawnOffset;

        // Try to spawn from a pool if available (more efficient)
        if (ArrowProjectilePool.Instance != null && ArrowProjectilePool.Instance.projectilePrefab != null)
        {
            var proj = ArrowProjectilePool.Instance.Spawn(spawnPos, dir, "Enemy");
            if (proj != null)
            {
                Debug.Log($"{name}: Spawned pooled projectile {proj.name} at {spawnPos} dir={dir}");
            }
            else
            {
                Debug.LogWarning($"{name}: Failed to spawn pooled projectile (pool empty).");
            }
        }
        else
        {
            var go = Instantiate(rangedProjectilePrefab, spawnPos, Quaternion.identity);
            Debug.Log($"{name}: Projectile instantiated: {go.name} at {spawnPos} dir={dir}");
            var proj = go.GetComponent<ArrowProjectile>();
            if (proj != null)
            {
                proj.ownerTag = "Enemy";
                proj.Fire(dir);
            }
            else
            {
                Debug.LogWarning($"{name}: Spawned projectile missing ArrowProjectile component.");
            }
        }
    }

    private IEnumerator AttackRecovery()
    {
        yield return new WaitForSeconds(attackRecovery);
        isRecovering = false;
        SetIdle();
    }

    private bool IsRetreating() => retreatAfterAttack && Time.time < retreatUntilTime;

    private bool IsDashing() => allowDash && Time.time < dashUntilTime;

    private bool IsKiting()
    {
        // Timed kite window takes priority: allows a short burst of kiting after events (hit/retreat)
        if (kiteBurstDuration > 0f && Time.time < kiteUntilTime) return true;

        // Legacy: kite briefly after a successful hit (kept for compatibility)
        if (Time.time < kiteAfterHitUntilTime) return true;

        // Kite if this enemy is low HP (persistent until hp changes)
        if (kiteWhenLowHP && maxHealth > 0)
        {
            float hpPercent = (float)currentHealth / maxHealth;
            if (hpPercent <= lowHPThreshold) return true;
        }

        // Kite if the player's HP is low and this behavior is enabled (persistent)
        if (kiteWhenPlayerLowHP && targetHealth != null && targetHealth.maxHP > 0)
        {
            float pHpPercent = (float)targetHealth.GetHP() / targetHealth.maxHP;
            if (pHpPercent <= playerLowHPThreshold) return true;
        }

        return false;
    }

    private void RetreatFromTarget()
    {
        if (target == null) return;

        Vector3 away = (transform.position - target.position).normalized;
        if (away.sqrMagnitude < 0.001f)
            away = -transform.right; // fallback

        Vector3 desired = target.position + away * retreatDistance;
        float speed = moveSpeed * (IsDashing() ? dashSpeedMultiplier : 1f);
        transform.position = Vector3.MoveTowards(transform.position, desired, speed * Time.deltaTime);
        SetSpeed(1f);
    }

    private void KiteFromTarget()
    {
        if (target == null) return;

        float dist = Vector3.Distance(transform.position, target.position);

        // If within striking range, attempt to attack opportunistically
        if (dist <= attackRange)
        {
            SetSpeed(0f);
            TryStartAttack();
            return;
        }

        if (dist < kiteDistance)
        {
            // Move away to keep distance
            RetreatFromTarget();
        }
        else
        {
            // If already far enough, idle or circle lightly (idle for now)
            SetIdle();
        }
    }

    private bool HasLineOfSight()
    {
        if (!useLineOfSight) return true;
        if (target == null) return false;

        Vector2 origin = transform.position;
        Vector2 dest = target.position;
        var hit = Physics2D.Linecast(origin, dest, lineOfSightBlockingMask);
        return hit.collider == null;
    }

    private IEnumerator AttackWatcher()
    {
        // Simple watchdog: wait the max duration then force-end the attack if it hasn't ended.
        if (maxAttackDuration <= 0f) { attackWatcherCoroutine = null; yield break; }

        float endTime = Time.time + maxAttackDuration;
        while (Time.time < endTime) yield return null;

        attackWatcherCoroutine = null;

        // If we're still attacking or recovering, force end it
        if (isRecovering || (anim != null && anim.GetBool("isAttacking")))
            EndAttack();
    }

    private IEnumerator ApplyDamageAtHitTime()
    {
        // Wait for the attack animation to start (short timeout). If it doesn't start, fall back to the configured delay.
        float timeout = Mathf.Max(0.05f, maxAttackDuration > 0f ? maxAttackDuration : 1f);
        float waited = 0f;

        while (!AnimIsInAttackState() && waited < timeout)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        bool animationStarted = AnimIsInAttackState();

        if (!animationStarted && !allowFallbackIfAnimNotStarted)
        {
            Debug.Log($"Enemy '{name}' auto-damage aborted: attack animation never started within timeout.");
            yield break; // do not apply fallback damage if the animation never started
        }

        if (animationStarted && useNormalizedHitTime)
        {
            // Try to compute remaining time until the desired normalized hit time
            AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);
            var clips = anim.GetCurrentAnimatorClipInfo(0);
            float clipLength = (clips.Length > 0 && clips[0].clip != null) ? clips[0].clip.length : 0f;

            if (clipLength > 0f)
            {
                float currentNormalized = state.normalizedTime % 1f;
                float remainingNormalized = attackHitNormalizedTime - currentNormalized;
                if (remainingNormalized < 0f) remainingNormalized += 1f;
                float delay = remainingNormalized * clipLength;
                if (delay > 0f) yield return new WaitForSeconds(delay);
            }
            else if (attackHitDelay > 0f)
            {
                // If we couldn't find a clip length, fall back to the raw delay
                yield return new WaitForSeconds(attackHitDelay);
            }
        }
        else if (attackHitDelay > 0f)
        {
            // Fallback: simple fixed delay
            yield return new WaitForSeconds(attackHitDelay);
        }

        // Before applying damage, ensure the player is still within the attack hit area (allows dodging)
        if (target == null) yield break;
        if (attackHitsOutOfRange)
        {
            if (!hitAppliedThisAttack) ApplyDamage();
        }
        else
        {
            if (IsTargetInHitArea(target))
            {
                if (!hitAppliedThisAttack) ApplyDamage();
            }
        }
    }

    private bool IsTargetInHitArea(Transform t)
    {
        if (t == null) return false;

        // Determine effective hit area based on variant config if present
        Vector2 localOffset = attackHitOffset;
        float effectiveRadius = attackHitRadius;
        bool effectiveUseBox = useBoxHitArea;
        Vector2 effectiveBoxSize = attackHitBoxSize;

        if (runtimeAttackIndex > 0)
        {
            int idx = runtimeAttackIndex - 1;
            if (attackVariantConfigs != null && idx < attackVariantConfigs.Length)
            {
                var cfg = attackVariantConfigs[idx];
                if (cfg.hitOffset != Vector2.zero) localOffset = cfg.hitOffset;
                if (cfg.hitRadius > 0f) effectiveRadius = cfg.hitRadius;
                if (cfg.useBox) effectiveUseBox = true;
                if (cfg.boxSize.sqrMagnitude > 0.0001f) effectiveBoxSize = cfg.boxSize;
            }
        }

        // Sanity fallback: ensure we have non-zero extents so hits are possible
        const float eps = 0.0001f;
        if (!effectiveUseBox && effectiveRadius <= eps)
        {
            // fall back to default attackHitRadius or a safe value
            effectiveRadius = Mathf.Max(attackHitRadius, 0.5f);
            Debug.LogWarning($"Enemy '{name}' effective hit radius was zero — falling back to {effectiveRadius}.");
        }

        if (effectiveUseBox && (effectiveBoxSize.x <= eps || effectiveBoxSize.y <= eps))
        {
            // If box size is invalid, try to derive a sensible size from radius or defaults
            float fallbackX = (effectiveRadius > eps) ? Mathf.Max(effectiveRadius * 2f, 0.5f) : Mathf.Max(attackHitBoxSize.x, 1.6f);
            float fallbackY = Mathf.Max(effectiveBoxSize.y, Mathf.Max(0.4f, effectiveRadius));
            if (fallbackX <= eps) fallbackX = 1.6f;
            if (fallbackY <= eps) fallbackY = 0.6f;
            effectiveBoxSize = new Vector2(fallbackX, fallbackY);
            Debug.LogWarning($"Enemy '{name}' effective box size was zero — falling back to {effectiveBoxSize}.");
        }

        if (useFacingFlip) localOffset.x *= Mathf.Sign(transform.localScale.x);
        Vector2 origin = (Vector2)transform.position + localOffset;
        Vector2 toTarget = (t.position - (Vector3)origin);

        if (!attackHitsOutOfRange)
        {
            if (effectiveUseBox)
            {
                Vector2 forward = useFacingFlip ? (Vector2)transform.right * Mathf.Sign(transform.localScale.x) : ((t.position - transform.position).normalized);
                Vector2 boxCenter = origin + forward.normalized * (effectiveBoxSize.x * 0.5f);
                Vector2 rel = (Vector2)t.position - boxCenter;
                Vector2 fNorm = forward.normalized;
                Vector2 perp = new Vector2(-fNorm.y, fNorm.x);
                float projF = Vector2.Dot(rel, fNorm);
                float projS = Vector2.Dot(rel, perp);
                if (Mathf.Abs(projF) > effectiveBoxSize.x * 0.5f || Mathf.Abs(projS) > effectiveBoxSize.y * 0.5f)
                    return false;
            }
            else
            {
                if (toTarget.sqrMagnitude > effectiveRadius * effectiveRadius)
                    return false;
            }
        }

        if (attackTargetMask != 0)
        {
            Collider2D[] hits;
            if (effectiveUseBox)
            {
                Vector2 forward = useFacingFlip ? (Vector2)transform.right * Mathf.Sign(transform.localScale.x) : ((t.position - transform.position).normalized);
                Vector2 boxCenter = origin + forward.normalized * (effectiveBoxSize.x * 0.5f);
                float angle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;
                hits = Physics2D.OverlapBoxAll(boxCenter, effectiveBoxSize, angle, attackTargetMask);
            }
            else
            {
                hits = Physics2D.OverlapCircleAll(origin, effectiveRadius, attackTargetMask);
            }

            if (hits == null || hits.Length == 0) return false;
            bool found = false;
            foreach (var h in hits)
            {
                if (h == null) continue;
                if (h.transform == t || h.transform.IsChildOf(t) || t.IsChildOf(h.transform)) { found = true; break; }
            }
            if (!found) return false;
        }

        Vector2 forwardDir = useFacingFlip ? (Vector2)transform.right * Mathf.Sign(transform.localScale.x) : ((t.position - transform.position).normalized);
        if (Vector2.Dot(toTarget.normalized, forwardDir.normalized) < 0.2f) return false;

        return true;
    }

    private void ApplyDamage()
    {
        if (hitAppliedThisAttack) return;

        // If the player is currently dashing, don't apply damage (allows dash pass-through to dodge attacks)
        if (target != null)
        {
            var p = target.GetComponent<Player>();
            if (p != null && p.IsDashing)
            {
                Debug.Log($"Enemy '{name}' attempted to hit player but player is dashing; damage skipped.");
                return;
            }
        }

        // Ensure the target is actually inside the configured hit area (more precise than a distance-from-center test)
        if (!attackHitsOutOfRange && target != null)
        {
            if (!IsTargetInHitArea(target))
            {
                Debug.Log($"Enemy '{name}' attempted to hit player but player left the attack hit area; damage skipped.");
                return;
            }
        }

        hitAppliedThisAttack = true;

        if (targetHealth != null)
        {
            targetHealth.TakeDamage(damageAmount);
        }
        else if (target != null)
        {
            var ph = target.GetComponent<PlayerHealth>();
            if (ph != null) ph.TakeDamage(damageAmount);
        }

        Debug.Log($"Enemy '{name}' applied {damageAmount} damage to target '{(target != null ? target.name : "null")}' (attack {runtimeAttackStateName ?? attackStateName}).");

        onAttackHit?.Invoke();

        // If configured, briefly kite after a successful hit to create a hit-and-run feel
        if (kiteAfterHit)
            kiteAfterHitUntilTime = Time.time + kiteAfterHitDuration;
    }

    // Health API - can be called from other scripts or UnityEvents
    // Internal helper that applies final integer damage (already adjusted by multiplier if necessary)
    private void ApplyFinalDamage(int finalAmount, bool isCritical = false, float percent = -1f)
    {
        finalAmount = Mathf.Max(1, finalAmount);
        Debug.Log($"Enemy '{name}' final dmg={finalAmount}{(percent >= 0f ? $" ({percent * 100f}% of max)" : "")}{(isCritical ? " CRITICAL" : "")} before HP={currentHealth} (armor={armor}, mult={damageTakenMultiplier})");

        currentHealth -= finalAmount;
        Debug.Log($"Enemy '{name}' HP after hit: {currentHealth}/{maxHealth}");

        if (hpBarInstance != null)
        {
            hpBarInstance.SetHP(currentHealth);
            if (currentHealth <= 0)
                hpBarInstance.gameObject.SetActive(false);
        }

        if (anim != null) anim.SetTrigger("Hit");

        if (currentHealth <= 0) Die();
    }

    public void TakeDamage(int amount)
    {
        if (currentHealth <= 0 || isDead) return; // already dead

        // Apply damage taken multiplier (defense/resistance) + armor reduction
        float armorMult = GetArmorDamageMultiplier();
        int finalAmount = Mathf.Max(0, Mathf.RoundToInt(amount * damageTakenMultiplier * armorMult));
        finalAmount = Mathf.Max(1, finalAmount); // ensure at least 1 damage

        ApplyFinalDamage(finalAmount, false, -1f);
    }

    /// <summary>
    /// Apply damage as a percentage of this enemy's max health (0..1). Applies damageTakenMultiplier and supports criticals.
    /// </summary>
    public void TakePercentDamage(float percent, bool isCritical = false)
    {
        percent = Mathf.Clamp01(percent);
        // Compute base amount from percent and maxHealth (float)
        float baseAmount = percent * maxHealth;
        // Apply multiplier and round to int once to avoid double-multiplying and rounding bias
        float armorMult = GetArmorDamageMultiplier();
        int finalAmount = Mathf.Max(1, Mathf.RoundToInt(baseAmount * damageTakenMultiplier * armorMult));

        if (isCritical)
            Debug.Log($"Enemy '{name}' took CRITICAL {finalAmount} damage ({percent * 100f}% of max after mult {damageTakenMultiplier}).");

        ApplyFinalDamage(finalAmount, isCritical, percent);
    }

    // Dota-style armor reduction: damage multiplier = 1 - (0.06 * armor) / (1 + 0.06 * |armor|)
    private float GetArmorDamageMultiplier()
    {
        float a = armor;
        float reduction = (0.06f * a) / (1f + 0.06f * Mathf.Abs(a));
        return 1f - reduction;
    }

    [ContextMenu("Debug Damage 1")]
    public void DebugDamage1() => TakeDamage(1);

    [ContextMenu("Debug Kill")]
    public void DebugKill() => TakeDamage(9999);

    public int GetCurrentHealth() => currentHealth;

    /// <summary>
    /// Returns current health as a fraction of max health (0..1).
    /// Useful for external systems that need percent-based triggers.
    /// </summary>
    public float GetHealthPercent()
    {
        if (maxHealth <= 0) return 0f;
        return Mathf.Clamp01(currentHealth / (float)maxHealth);
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        HandleStage5FinalBossDeath();

        // stop ongoing coroutines and timers
        if (attackWatcherCoroutine != null) { StopCoroutine(attackWatcherCoroutine); attackWatcherCoroutine = null; }
        if (applyDamageCoroutine != null) { StopCoroutine(applyDamageCoroutine); applyDamageCoroutine = null; }

        onDeath?.Invoke();

        // Try to spawn reinforcements immediately on death so it still happens even if
        // the death animation waits or never exits the death state.
        TrySpawnReinforcements("Die()");

        // Make corpse pass-through immediately if requested so player can walk through while death animation plays
        if (makeCorpsePassThrough)
        {
            // Turn all 2D colliders on this object (and children) into triggers or disable them
            var cols = GetComponentsInChildren<Collider2D>();
            foreach (var c in cols)
            {
                if (c == null) continue;
                if (setTriggerOnDeath) c.isTrigger = true;
                else c.enabled = false;
            }

            // Stop physics on any Rigidbodies so the corpse doesn't interact with the world
            var rb = GetComponent<Rigidbody2D>();
            if (rb != null) rb.simulated = false;
            var childRbs = GetComponentsInChildren<Rigidbody2D>();
            foreach (var r in childRbs) if (r != null) r.simulated = false;
        }

        // Trigger death animation if the animator has the configured trigger, otherwise try to directly transition to the death state
        bool startedDeathAnim = false;
        if (anim != null)
        {
            if (!string.IsNullOrEmpty(deathTrigger) && AnimatorHasParameter(deathTrigger))
            {
                anim.SetTrigger(deathTrigger);
                startedDeathAnim = true;
            }
            else if (!string.IsNullOrEmpty(deathStateName))
            {
                // attempt to crossfade directly to the death state as a fallback
                try
                {
                    anim.CrossFade(deathStateName, 0f);
                    startedDeathAnim = true;
                }
                catch
                {
                }
            }

            if (!startedDeathAnim)
            {
            }
        }

        if (waitForDeathAnimation && anim != null && !string.IsNullOrEmpty(deathStateName) && startedDeathAnim)
        {
            StartCoroutine(WaitForDeathAnimationAndCleanup());
        }
        else
        {
            // Immediately cleanup (spawn effects, play sfx, destroy/disable)
            DoDeathCleanup();
        }
    }

    private void HandleStage5FinalBossDeath()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        bool isStage5Scene = sceneName.Equals("Floor5");
        bool isEligibleFinalBoss = isStage5FinalBoss || (isStage5Scene && dropShardOnDeath);
        if (!isEligibleFinalBoss) return;

        FinalSequenceState.MarkStage5BossDefeated();

        if (muteBgmOnDeath)
            MuteStageMusicNow();
    }

    private void MuteStageMusicNow()
    {
        var sources = GetStageBgmSources();
        foreach (var src in sources)
        {
            if (src == null) continue;

            if (bgmFadeOutDuration > 0f)
            {
                var helper = src.GetComponent<AudioSourceFadeHelper>();
                if (helper == null)
                    helper = src.gameObject.AddComponent<AudioSourceFadeHelper>();
                helper.FadeOutAndStop(src, bgmFadeOutDuration);
            }
            else
            {
                src.volume = 0f;
                src.mute = true;
                if (src.isPlaying) src.Stop();
            }
        }
    }

    private AudioSource[] GetStageBgmSources()
    {
        bool usedExplicitSources = bgmSourcesToMute != null && bgmSourcesToMute.Length > 0;
        if (usedExplicitSources)
            return bgmSourcesToMute;

        var allSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        var looping = new System.Collections.Generic.List<AudioSource>();
        foreach (var src in allSources)
        {
            if (src == null) continue;
            if (!src.loop) continue;
            looping.Add(src);
        }

        return looping.ToArray();
    }

    private void DoDeathCleanup()
    {
        // hide or destroy hp bar instance so it doesn't linger
        if (hpBarInstance != null)
            hpBarInstance.gameObject.SetActive(false);

        if (deathVFX != null)
            Instantiate(deathVFX, transform.position, Quaternion.identity);

        if (deathClip != null && !deathSfxViaAnimationEvents)
            AudioSource.PlayClipAtPoint(deathClip, transform.position, deathVolume);

        // Spawn shard drop once on death (boss use-case).
        TryDropShardOnDeath("DoDeathCleanup()");

        // Try to spawn reinforcements (if configured and a spawner is assigned)
        TrySpawnReinforcements("DoDeathCleanup()");

        if (destroyOnDeath)
            Destroy(gameObject);
        else
        {
            // disable behavior if not destroying
            enabled = false;
            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
            if (GetComponent<Rigidbody2D>() is Rigidbody2D rb) rb.simulated = false;
        }
    }

    private void TrySpawnReinforcements(string context)
    {
        if (reinforcementSpawned) return;
        if (!spawnReinforcementsOnDeath)
        {
            if (reinforcementDebug) Debug.Log($"Enemy '{name}' no reinforcements: spawnReinforcementsOnDeath=false ({context}).");
            return;
        }
        if (reinforcementSpawner == null)
        {
            if (reinforcementDebug) Debug.LogWarning($"Enemy '{name}' no reinforcements: reinforcementSpawner is null ({context}).");
            return;
        }

        float chance01 = NormalizeChance01(reinforcementChance);
        if (chance01 <= 0f)
        {
            if (reinforcementDebug) Debug.Log($"Enemy '{name}' no reinforcements: chance=0 ({context}).");
            return;
        }

        if (Random.value > chance01)
        {
            if (reinforcementDebug) Debug.Log($"Enemy '{name}' reinforcements skipped by chance ({chance01 * 100f:0.#}%) ({context}).");
            return;
        }

        reinforcementSpawned = true;
        if (reinforcementDebug) Debug.Log($"Enemy '{name}' spawning reinforcements x{reinforcementCount} ({context}).");
        // pass 1f so the spawner won't re-roll chance (we already did it here)
        reinforcementSpawner.TrySpawnAt(transform.position, reinforcementCount, 1f, reinforcementDelay);
    }

    private void TryDropShardOnDeath(string context)
    {
        if (shardDropSpawned) return;
        if (!dropShardOnDeath) return;

        if (shardPickupPrefab == null)
        {
            Debug.LogWarning($"Enemy '{name}' shard drop skipped: shardPickupPrefab is null ({context}).");
            return;
        }

        float rightExtent = 0f;
        var renderers = GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in renderers)
        {
            if (sr == null) continue;
            rightExtent = Mathf.Max(rightExtent, sr.bounds.max.x - transform.position.x);
        }

        Vector3 spawnPos = transform.position + new Vector3(rightExtent + Mathf.Abs(shardDropOffset.x), shardDropOffset.y, 0f);
        Instantiate(shardPickupPrefab, spawnPos, Quaternion.identity);
        shardDropSpawned = true;
    }

    private float NormalizeChance01(float chance)
    {
        if (chance > 1f) chance /= 100f; // allow using 0..100 in inspector
        return Mathf.Clamp01(chance);
    }

    private IEnumerator WaitForDeathAnimationAndCleanup()
    {
        float timeout = 3f; // safety timeout
        float elapsed = 0f;

        // wait until animator enters the death state (or timeout)
        while (anim != null && !anim.GetCurrentAnimatorStateInfo(0).IsName(deathStateName) && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // if we didn't see the state, just cleanup
        if (anim == null || elapsed >= timeout)
        {
            DoDeathCleanup();
            yield break;
        }

        // wait while in death state
        while (anim != null && anim.GetCurrentAnimatorStateInfo(0).IsName(deathStateName))
            yield return null;

        DoDeathCleanup();
    }

    private bool AnimatorHasParameter(string name)
    {
        if (anim == null) return false;
        foreach (var p in anim.parameters)
            if (p.name == name) return true;
        return false;
    }

    private void ReturnHome()
    {
        float distToHome = Vector3.Distance(transform.position, startPosition);
        if (distToHome > homeThreshold)
        {
            transform.position = Vector3.MoveTowards(transform.position, startPosition, moveSpeed * Time.deltaTime);
            SetSpeed(1f);
            if (useFacingFlip) FlipTowards(startPosition);
            else FaceTowards(startPosition);
        }
        else
        {
            SetIdle();
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Draw the attack hit area (using the configured local offset + radius or box). If not playing, draw both sides to show the zone when using facing flip.
        Gizmos.color = Color.magenta;
        if (!Application.isPlaying && useFacingFlip)
        {
            if (useBoxHitArea)
            {
                // left and right boxes
                Vector2 o1 = (Vector2)transform.position + attackHitOffset;
                Vector2 o2 = (Vector2)transform.position + new Vector2(-attackHitOffset.x, attackHitOffset.y);
                // draw box at o1 (aligned with world axes for preview)
                Gizmos.matrix = Matrix4x4.TRS((Vector3)o1, Quaternion.identity, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero + new Vector3(attackHitBoxSize.x * 0.5f, 0f, 0f), new Vector3(attackHitBoxSize.x, attackHitBoxSize.y, 0f));
                Gizmos.matrix = Matrix4x4.identity;
                Gizmos.matrix = Matrix4x4.TRS((Vector3)o2, Quaternion.identity, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero + new Vector3(-attackHitBoxSize.x * 0.5f, 0f, 0f), new Vector3(attackHitBoxSize.x, attackHitBoxSize.y, 0f));
                Gizmos.matrix = Matrix4x4.identity;
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position + (Vector3)attackHitOffset, attackHitRadius);
                Gizmos.DrawWireSphere(transform.position + (Vector3)new Vector2(-attackHitOffset.x, attackHitOffset.y), attackHitRadius);
            }
        }
        else
        {
            // Use effective settings if runtime variant config is active
            Vector2 offset = attackHitOffset;
            float radius = attackHitRadius;
            bool drawBox = useBoxHitArea;
            Vector2 boxSize = attackHitBoxSize;

            if (Application.isPlaying && runtimeAttackIndex > 0)
            {
                int idx = runtimeAttackIndex - 1;
                if (attackVariantConfigs != null && idx < attackVariantConfigs.Length)
                {
                    var cfg = attackVariantConfigs[idx];
                    if (cfg.hitOffset != Vector2.zero) offset = cfg.hitOffset;
                    if (cfg.hitRadius > 0f) radius = cfg.hitRadius;
                    if (cfg.useBox) drawBox = true;
                    if (cfg.boxSize.sqrMagnitude > 0.0001f) boxSize = cfg.boxSize;
                }
            }

            if (useFacingFlip) offset.x *= Mathf.Sign(transform.localScale.x);

            if (drawBox)
            {
                Vector2 forward = useFacingFlip ? (Vector2)transform.right * Mathf.Sign(transform.localScale.x) : Vector2.right;
                Vector2 boxCenter = (Vector2)transform.position + offset + forward.normalized * (boxSize.x * 0.5f);
                float angle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;
                Gizmos.matrix = Matrix4x4.TRS((Vector3)boxCenter, Quaternion.Euler(0f, 0f, angle), Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(boxSize.x, boxSize.y, 0f));
                Gizmos.matrix = Matrix4x4.identity;
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position + (Vector3)offset, radius);
            }
        }
    }
}