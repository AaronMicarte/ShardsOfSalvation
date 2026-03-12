using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BuffPickup : MonoBehaviour
{
    [Header("Buff")]
    [SerializeField] private BuffDropType buffType = BuffDropType.Damage;
    [SerializeField, Tooltip("How many stacks this pickup grants")]
    private int stackAmount = 1;

    [Header("Feedback")]
    [SerializeField] private GameObject pickupVFX;
    [SerializeField] private AudioClip pickupClip;
    [SerializeField, Range(0f, 1f)] private float pickupVolume = 1f;

    [Header("Spawn Motion")]
    [SerializeField] private bool playSpawnMotion = true;
    [SerializeField] private float spawnPopHeight = 0.6f;
    [SerializeField] private float spawnPopDuration = 0.12f;
    [SerializeField] private float spawnDropDuration = 0.18f;
    [SerializeField] private float pickupEnableDelay = 0.08f;

    [Header("Idle Float")]
    [SerializeField] private bool idleFloat = true;
    [SerializeField] private float floatAmplitude = 0.06f;
    [SerializeField] private float floatFrequency = 1.8f;

    private bool canBePicked = true;
    private bool floatActive = false;
    private float floatTimer = 0f;
    private Vector3 settledPosition;

    private void Awake()
    {
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void OnEnable()
    {
        settledPosition = transform.position;
        canBePicked = true;
        floatActive = false;

        if (playSpawnMotion)
            StartCoroutine(SpawnMotionRoutine());
        else
            floatActive = idleFloat;
    }

    private void Update()
    {
        if (!floatActive) return;

        floatTimer += Time.deltaTime;
        float yOffset = Mathf.Sin(floatTimer * Mathf.PI * 2f * floatFrequency) * floatAmplitude;
        transform.position = settledPosition + Vector3.up * yOffset;
    }

    public void Configure(BuffDropType type, int stacks)
    {
        buffType = type;
        stackAmount = Mathf.Max(1, stacks);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!canBePicked) return;
        if (other == null) return;

        var player = other.GetComponent<Player>() ?? other.GetComponentInParent<Player>();
        if (player == null) return;

        bool applied = player.TryApplyDropBuff(buffType, stackAmount);
        if (!applied)
            return;

        if (pickupVFX != null)
            Instantiate(pickupVFX, transform.position, Quaternion.identity);

        if (pickupClip != null)
            AudioSource.PlayClipAtPoint(pickupClip, transform.position, pickupVolume);

        Destroy(gameObject);
    }

    private System.Collections.IEnumerator SpawnMotionRoutine()
    {
        canBePicked = false;
        floatActive = false;
        floatTimer = 0f;

        settledPosition = transform.position;
        Vector3 start = settledPosition;
        Vector3 apex = start + Vector3.up * spawnPopHeight;

        float upDuration = Mathf.Max(0.01f, spawnPopDuration);
        float downDuration = Mathf.Max(0.01f, spawnDropDuration);

        float t = 0f;
        while (t < upDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / upDuration);
            float eased = 1f - Mathf.Pow(1f - k, 2f);
            transform.position = Vector3.LerpUnclamped(start, apex, eased);
            yield return null;
        }

        t = 0f;
        while (t < downDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / downDuration);
            float eased = k * k;
            transform.position = Vector3.LerpUnclamped(apex, start, eased);
            yield return null;
        }

        transform.position = settledPosition;
        floatActive = idleFloat;

        if (pickupEnableDelay > 0f)
            yield return new WaitForSeconds(pickupEnableDelay);

        canBePicked = true;
    }
}
