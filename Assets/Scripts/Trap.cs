using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider2D))]
public class Trap : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField, Tooltip("Damage applied each tick")] private int damagePerTick = 1;
    [SerializeField, Tooltip("Damage applied by one-shot (single) hits")] private int oneShotDamage = 2;
    [SerializeField, Tooltip("Seconds between damage ticks (0 = instant on enter)")] private float tickInterval = 0.5f;
    [SerializeField, Tooltip("If true, apply damage once on enter and do not repeat")] private bool oneShotOnEnter = false;
    [SerializeField, Tooltip("Delay before applying one-shot damage on enter (seconds)")] private float oneShotDelay = 0f;
    [SerializeField, Tooltip("Delay before starting ticks (seconds)")] private float initialDelay = 0f;
    [SerializeField, Tooltip("If true, trap bypasses player's dash invulnerability and will damage even while dashing")] private bool bypassDashInvulnerability = false;

    [Header("Targeting")]
    [SerializeField, Tooltip("Who gets damaged (use Player layer)")] private LayerMask targetMask;

    [Header("Events")]
    public UnityEvent onEnter;
    public UnityEvent onTick;
    public UnityEvent onExit;
    public UnityEvent onOneShot;

    [Header("Audio")]
    [SerializeField, Tooltip("Optional AudioSource to play trap SFX (recommended). If empty, PlayClipAtPoint will be used.")] private AudioSource sfxSource;
    [SerializeField, Tooltip("Sound played when an object enters the trap")] private AudioClip enterClip;
    [SerializeField, Tooltip("Sound played each damage tick or when Pulse() is called")] private AudioClip tickClip;
    [SerializeField, Tooltip("Sound played when a one-shot damage is applied")] private AudioClip oneShotClip;
    [SerializeField, Tooltip("Sound played when an object exits the trap")] private AudioClip exitClip;
    [SerializeField, Tooltip("Sound played when StartDamageForAllInside() is invoked")] private AudioClip startDamageClip;
    [SerializeField, Tooltip("Sound played when StopDamageForAll() is invoked")] private AudioClip stopDamageClip;
    [SerializeField, Range(0f, 1f), Tooltip("Volume for trap SFX")] private float sfxVolume = 1f;
    [SerializeField, Tooltip("Pitch variance (+/- range) to add slight variation to repeated SFX")] private float pitchVariance = 0.05f;

    // Track coroutines per target to support multiple colliders entering
    private readonly Dictionary<GameObject, Coroutine> activeDamageRoutines = new Dictionary<GameObject, Coroutine>();
    // Track one-shot coroutines separately so we can time single-hit traps
    private readonly Dictionary<GameObject, Coroutine> activeOneShotRoutines = new Dictionary<GameObject, Coroutine>();

    void Reset()
    {
        // Try to default to Player layer if present in project
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0) targetMask = 1 << playerLayer;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsTarget(other.gameObject)) return;
        var go = other.gameObject;
        onEnter?.Invoke();
        PlayEnterSfx();

        if (oneShotOnEnter)
        {
            if (oneShotDelay <= 0f)
            {
                ApplyOneShotTo(go);
            }
            else
            {
                if (!activeOneShotRoutines.ContainsKey(go))
                    activeOneShotRoutines[go] = StartCoroutine(OneShotRoutine(go));
            }

            return;
        }

        // Avoid stacking multiple coroutines for same object
        if (!activeDamageRoutines.ContainsKey(go))
            activeDamageRoutines[go] = StartCoroutine(DamageRoutine(go));
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!IsTarget(other.gameObject)) return;
        var go = other.gameObject;
        StopDamageRoutine(go);
        onExit?.Invoke();
        PlayExitSfx();
    }

    private bool IsTarget(GameObject go)
    {
        return ((targetMask.value & (1 << go.layer)) != 0);
    }

    private IEnumerator DamageRoutine(GameObject target)
    {
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);
        while (true)
        {
            ApplyDamageTo(target);
            PlayTickSfx();
            onTick?.Invoke();
            if (tickInterval <= 0f) yield break; // safety
            yield return new WaitForSeconds(tickInterval);
        }
    }

    private void ApplyDamageTo(GameObject target)
    {
        // Prefer PlayerHealth if present (your project already has this)
        var ph = target.GetComponentInParent<PlayerHealth>();
        if (ph != null)
        {
            ph.TakeDamage(damagePerTick, bypassDashInvulnerability);
            return;
        }

        // Optionally support a generic interface later (IDamageable) to generalize
        var otherHealth = target.GetComponentInParent<MonoBehaviour>(); // placeholder
        // if (otherHealth is IDamageable d) d.TakeDamage(damagePerTick);
    }

    private IEnumerator OneShotRoutine(GameObject target)
    {
        yield return new WaitForSeconds(oneShotDelay);
        ApplyOneShotTo(target);
        // cleanup entry
        StopOneShotRoutine(target);
    }

    private void ApplyOneShotTo(GameObject target)
    {
        var ph = target.GetComponentInParent<PlayerHealth>();
        if (ph != null)
        {
            ph.TakeDamage(oneShotDamage, bypassDashInvulnerability);
            PlayOneShotSfx();
            onOneShot?.Invoke();
            return;
        }

        // future: IDamageable support
    }

    private void StopOneShotRoutine(GameObject target)
    {
        if (activeOneShotRoutines.TryGetValue(target, out var c))
        {
            StopCoroutine(c);
            activeOneShotRoutines.Remove(target);
        }
    }

    /// <summary>
    /// Public API to apply a one-shot damage to all targets inside (use from animation events to time an attack)
    /// </summary>
    public void OneShot()
    {
        var results = new Collider2D[10];
        int count = Physics2D.OverlapCollider(GetComponent<Collider2D>(),
            new ContactFilter2D() { layerMask = targetMask, useLayerMask = true }, results);

        for (int i = 0; i < count; i++)
        {
            var hit = results[i];
            if (hit != null) ApplyOneShotTo(hit.gameObject);
        }

        onOneShot?.Invoke();
    }

    private void StopDamageRoutine(GameObject target)
    {
        if (activeDamageRoutines.TryGetValue(target, out var c))
        {
            StopCoroutine(c);
            activeDamageRoutines.Remove(target);
        }
    }

    /// <summary>
    /// Public API so external timers / animations can trigger damage on all targets currently inside.
    /// Call this from an animation event for exact timing.
    /// </summary>
    public void Pulse()
    {
        var results = new Collider2D[10];
        int count = Physics2D.OverlapCollider(GetComponent<Collider2D>(),
            new ContactFilter2D() { layerMask = targetMask, useLayerMask = true }, results);

        for (int i = 0; i < count; i++)
        {
            var hit = results[i];
            if (hit != null) { ApplyDamageTo(hit.gameObject); PlayTickSfx(); }
        }

        // Invoke event so animation-based pulses can trigger VFX/SFX too
        onTick?.Invoke();
    }

    /// <summary>
    /// Start continuous damage for all targets currently inside the trap area.
    /// Useful for enabling the trap from scripts or animations.
    /// </summary>
    public void StartDamageForAllInside()
    {
        var results = new Collider2D[10];
        int count = Physics2D.OverlapCollider(GetComponent<Collider2D>(),
            new ContactFilter2D() { layerMask = targetMask, useLayerMask = true }, results);

        for (int i = 0; i < count; i++)
        {
            var hit = results[i];
            if (hit == null) continue;
            var go = hit.gameObject;
            if (!activeDamageRoutines.ContainsKey(go))
                activeDamageRoutines[go] = StartCoroutine(DamageRoutine(go));
        }
    }

    /// <summary>
    /// Stop continuous damage for all currently-damaging targets.
    /// </summary>
    public void StopDamageForAll()
    {
        foreach (var kv in new List<KeyValuePair<GameObject, Coroutine>>(activeDamageRoutines))
            StopDamageRoutine(kv.Key);
    }

    /// <summary>
    /// Start continuous damage for a specific target (if it matches the trap's targetMask).
    /// </summary>
    public void StartDamage(GameObject target)
    {
        if (!IsTarget(target)) return;
        if (!activeDamageRoutines.ContainsKey(target))
            activeDamageRoutines[target] = StartCoroutine(DamageRoutine(target));
    }

    /// <summary>
    /// Stop continuous damage for a specific target.
    /// </summary>
    public void StopDamage(GameObject target)
    {
        StopDamageRoutine(target);
    }

    /// <summary>
    /// Returns true if the trap is currently applying continuous damage to the specified target.
    /// </summary>
    public bool IsDamaging(GameObject target)
    {
        return activeDamageRoutines.ContainsKey(target);
    }
    // --- Audio / animation event helpers ---
    private void PlayClip(AudioClip clip)
    {
        if (clip == null) return;
        if (sfxSource != null)
        {
            sfxSource.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
            sfxSource.PlayOneShot(clip, sfxVolume);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clip, transform.position, sfxVolume);
        }
    }

    /// <summary>Animation event friendly: play enter SFX</summary>
    public void PlayEnterSfx() => PlayClip(enterClip);
    /// <summary>Animation event friendly: play tick SFX</summary>
    public void PlayTickSfx() => PlayClip(tickClip);
    /// <summary>Animation event friendly: play one-shot SFX</summary>
    public void PlayOneShotSfx() => PlayClip(oneShotClip);
    /// <summary>Animation event friendly: play exit SFX</summary>
    public void PlayExitSfx() => PlayClip(exitClip);
    /// <summary>Animation event friendly: play start-damage SFX</summary>
    public void PlayStartDamageSfx() => PlayClip(startDamageClip);
    /// <summary>Animation event friendly: play stop-damage SFX</summary>
    public void PlayStopDamageSfx() => PlayClip(stopDamageClip);
    void OnDisable()
    {
        // cleanup any running coroutines
        foreach (var kv in new List<KeyValuePair<GameObject, Coroutine>>(activeDamageRoutines))
            StopDamageRoutine(kv.Key);

        foreach (var kv in new List<KeyValuePair<GameObject, Coroutine>>(activeOneShotRoutines))
            StopOneShotRoutine(kv.Key);
    }
}