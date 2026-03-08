using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShardUI : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private Image shardIcon;      // assign shard sprite here
    [SerializeField] private Text countText;       // "x 0"
    [SerializeField] private TextMeshProUGUI tmpCountText; // optional TMP

    [Header("Animation")]
    [SerializeField] private bool animateOnAdd = true;
    [SerializeField] private float pulseScale = 1.15f;
    [SerializeField] private float pulseDuration = 0.12f;

    int lastCount;

    void Start()
    {
        if (countText == null && tmpCountText == null)
        {
            countText = GetComponentInChildren<Text>();
            tmpCountText = GetComponentInChildren<TextMeshProUGUI>();
        }

        // If neither text is present the UI can't function — disable to avoid null access.
        if (countText == null && tmpCountText == null)
        {
            enabled = false;
            return;
        }

        if (ShardManager.Instance != null)
        {
            ShardManager.Instance.OnShardCountChanged += OnCountChanged;
            OnCountChanged(ShardManager.Instance.ShardCount);
        }
    }

    void OnDestroy()
    {
        if (ShardManager.Instance != null) ShardManager.Instance.OnShardCountChanged -= OnCountChanged;
    }

    void OnCountChanged(int count)
    {
        if (countText != null) countText.text = "x " + count;
        if (tmpCountText != null) tmpCountText.text = "x " + count;
        if (animateOnAdd && count > lastCount) StartCoroutine(Pulse());
        lastCount = count;
    }

    IEnumerator Pulse()
    {
        var orig = transform.localScale;
        var target = orig * pulseScale;
        float t = 0f;
        while (t < pulseDuration) { transform.localScale = Vector3.Lerp(orig, target, t / pulseDuration); t += Time.unscaledDeltaTime; yield return null; }
        t = 0f;
        while (t < pulseDuration) { transform.localScale = Vector3.Lerp(target, orig, t / pulseDuration); t += Time.unscaledDeltaTime; yield return null; }
        transform.localScale = orig;
    }
}