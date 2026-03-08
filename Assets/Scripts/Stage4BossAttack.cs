using System.Collections;
using UnityEngine;

/// <summary>
/// Stage-4 boss attacks.  Attack1: disable player's dash for a duration and reduce movement speed.
/// Call `Attack1()` from the boss AI / animator event to apply the debuff to the player.
/// </summary>
public class Stage4BossAttack : MonoBehaviour
{
    [Tooltip("Seconds the player's movement is slowed and blind chance applied")]
    public float disableDuration = 5f;
    [Tooltip("Chance (0..1) that player's attacks will miss while blinded")]
    public float blindChance = 0.5f;

    [Tooltip("Player moveSpeed value to set while disabled (world units)")]
    public float reducedMoveSpeed = 0.5f;

    [Tooltip("Optional AudioClip to play when the debuff is applied")]
    public AudioClip debuffSound;

    private AudioSource audioSource;

    // --- Auto-wire to Animator (optional) -------------------------------------------------
    [Header("Auto-Wire (Animator)")]
    [Tooltip("When true, this component watches the local Animator and invokes Attack1 when the configured state/time is reached.")]
    public bool autoWireToAnimator = true;
    [Tooltip("Animator state name to watch (use 'attack-1' or 'attack-2' from the Boss4 controller)")]
    public string animatorAttackStateName = "attack-1";
    [Tooltip("If > 0, Attack1 triggers when normalizedTime >= this value. 0 = trigger at state entry")]
    [Range(0f, 1f)] public float triggerNormalizedTime = 0f;

    private Animator animator;
    private bool triggeredInState = false;
    private int animatorLayer = 0;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (autoWireToAnimator)
        {
            animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
            if (animator == null)
                Debug.LogWarning($"{name}: autoWireToAnimator is enabled but no Animator was found on the GameObject.");
        }
    }

    private void Update()
    {
        if (!autoWireToAnimator || animator == null) return;

        var st = animator.GetCurrentAnimatorStateInfo(animatorLayer);

        // robust state matching: accept exact name, 'Base Layer.' prefix, or shortNameHash match
        int targetHash = Animator.StringToHash(animatorAttackStateName);
        bool isNameMatch = st.IsName(animatorAttackStateName)
                           || st.IsName("Base Layer." + animatorAttackStateName)
                           || st.shortNameHash == targetHash;

        if (isNameMatch)
        {
            if (!triggeredInState)
            {
                if (triggerNormalizedTime <= 0f)
                {
                    Attack1();
                    triggeredInState = true;
                }
                else
                {
                    // use modulo 1 in case the animation loops
                    float t = st.normalizedTime % 1f;
                    if (t >= triggerNormalizedTime)
                    {
                        Attack1();
                        triggeredInState = true;
                    }
                }
            }
        }
        else
        {
            // reset when leaving the state so we can trigger again next time
            triggeredInState = false;
        }
    }

    /// <summary>
    /// Attack1: temporarily prevents the player from dashing and reduces move speed for <see cref="disableDuration"/> seconds.
    /// Uses the Player timed API so the effect is enforced even if other systems try to re-enable dash.
    /// </summary>
    public void Attack1()
    {
        Debug.Log($"{name}: Attack1 — applying dash-disable + slow to player for {disableDuration}s (speed -> {reducedMoveSpeed}).");

        var player = FindFirstObjectByType<Player>();
        if (player == null)
        {
            var playerGO = GameObject.FindWithTag("Player");
            if (playerGO != null) player = playerGO.GetComponent<Player>();
        }

        if (player == null)
        {
            Debug.LogWarning($"{name}: Player component not present on the Player GameObject.");
            return;
        }

        // apply a timed blind (chance to miss) and temporary slow
        player.ApplyBlindForSeconds(disableDuration, blindChance);
        player.SetMoveSpeedForSeconds(reducedMoveSpeed, disableDuration);
        Debug.Log($"{name}: Attack1 applied to '{player.name}'. Blind chance {blindChance * 100f:0.#}% for {disableDuration}s. Effective speed now: {player.GetCurrentMoveSpeed():F2}");

        if (debuffSound != null)
        {
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource != null) audioSource.PlayOneShot(debuffSound);
        }
    }


    [ContextMenu("Test Attack1")]
    private void TestAttack1() => Attack1();
}