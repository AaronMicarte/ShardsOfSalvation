using UnityEngine;
using TMPro;

public class DamagePopup : MonoBehaviour
{
    [SerializeField] private TextMeshPro text;
    [SerializeField] private float lifetime = 0.8f;
    [SerializeField] private Vector3 movePerSecond = new Vector3(0f, 1.2f, 0f);
    [SerializeField] private float fadeDuration = 0.3f;

    private float spawnTime;
    private Color startColor = Color.white;

    void Awake()
    {
        if (text == null) text = GetComponentInChildren<TextMeshPro>();
        spawnTime = Time.time;
        if (text != null) startColor = text.color;
    }

    public void Setup(string message, Color color)
    {
        if (text == null) return;
        text.text = message;
        startColor = color;
        text.color = color;
    }

    void Update()
    {
        transform.position += movePerSecond * Time.deltaTime;

        float age = Time.time - spawnTime;
        if (fadeDuration > 0f && age >= Mathf.Max(0f, lifetime - fadeDuration) && text != null)
        {
            float t = Mathf.Clamp01((age - (lifetime - fadeDuration)) / fadeDuration);
            Color c = text.color;
            c.a = Mathf.Lerp(startColor.a, 0f, t);
            text.color = c;
        }

        if (age >= lifetime)
            Destroy(gameObject);
    }
}
