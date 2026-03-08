using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class RadialVisionOverlay : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Camera sceneCamera;

    [Header("Visuals")]
    public int textureSize = 512;        // 512 is a good default
    [Range(0.5f, 20f)] public float radius = 3.5f;            // world-space radius in units
    public float feather = 1f;           // feather width in world units
    [Range(0f, 1f)] public float noiseStrength = 0.08f;
    public float noiseScale = 6f;
    [Tooltip("Multiplier applied to camera diagonal to ensure overlay covers entire view.")]
    public float overlayScaleMultiplier = 1.6f;

    [Header("Coverage")]
    public float coverageMargin = 2f; // how much larger than the camera view the overlay should be (2 = 200%)
    public bool forceOpaqueOutside = true; // ensures alpha outside the rim is fully opaque

    SpriteRenderer sr;
    Texture2D tex;
    Sprite sprite;

    float neededDiameter = 0f;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sceneCamera == null) sceneCamera = Camera.main;

        // compute how large the overlay must be to fully cover the camera view (with margin)
        if (sceneCamera != null && sceneCamera.orthographic)
        {
            float worldHeight = sceneCamera.orthographicSize * 2f;
            float worldWidth = worldHeight * sceneCamera.aspect;
            // use diagonal (hypotenuse) so corners are covered, then multiply by overlayScaleMultiplier
            float diagonal = Mathf.Sqrt(worldWidth * worldWidth + worldHeight * worldHeight);
            neededDiameter = diagonal * overlayScaleMultiplier;
        }
        else
        {
            // fallback for perspective or missing camera - use a large value and margin
            neededDiameter = Mathf.Max(40f, radius * 10f) * overlayScaleMultiplier;
        }

        // scale the sprite so it covers the camera view (apply optional extra coverageMargin)
        float defaultSpriteDiameter = 2f; // our generated sprite is 2 world units wide by default
        float globalScale = (neededDiameter * Mathf.Max(1f, coverageMargin)) / defaultSpriteDiameter;
        transform.localScale = Vector3.one * globalScale;

        GenerateTexture();
        ApplySprite();
    }

    void LateUpdate()
    {
        if (player == null) return;
        // follow player position (keep overlay Z the same)
        transform.position = new Vector3(player.position.x, player.position.y, transform.position.z);
    }

    void GenerateTexture()
    {
        tex = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        float half = textureSize * 0.5f;
        float invHalf = 1f / half;

        // convert the world-space radius/feather to normalized texture space (0..1)
        float worldHalf = Mathf.Max(0.0001f, neededDiameter * 0.5f);
        float normalizedRadius = Mathf.Clamp01(radius / worldHalf);
        float normalizedFeather = Mathf.Clamp01(feather / worldHalf);

        float inner = Mathf.Clamp01(normalizedRadius - normalizedFeather);
        float outer = Mathf.Clamp01(normalizedRadius + normalizedFeather);

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float dx = (x - half) * invHalf;
                float dy = (y - half) * invHalf;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float a;
                if (dist <= inner)
                {
                    a = 0f; // fully transparent inside
                }
                else if (dist >= outer)
                {
                    a = 1f; // fully opaque outside (no noise here)
                }
                else
                {
                    float t = (dist - inner) / Mathf.Max(0.0001f, (outer - inner));
                    a = Mathf.SmoothStep(0f, 1f, t);

                    // add perlin noise only inside the rim region and reduce its impact toward edges
                    float nx = (x / (float)textureSize) * noiseScale;
                    float ny = (y / (float)textureSize) * noiseScale;
                    float n = (Mathf.PerlinNoise(nx, ny) - 0.5f) * 2f * noiseStrength;
                    // rim factor peaks near the middle of the rim and is 0 at inner/outer
                    float rimFactor = 1f - Mathf.Abs(0.5f - t) * 2f;
                    rimFactor = Mathf.Clamp01(rimFactor);
                    a = Mathf.Clamp01(a + n * rimFactor * 0.6f);
                }

                if (forceOpaqueOutside && dist >= outer) a = 1f; // enforce fully black outside

                Color col = new Color(0f, 0f, 0f, a); // black with computed alpha
                tex.SetPixel(x, y, col);
            }
        }

        tex.Apply();
    }

    void ApplySprite()
    {
        // Create a sprite whose default diameter in world units is 2 units (for easy scaling)
        float pixelsPerUnit = textureSize / 2f;
        sprite = Sprite.Create(tex, new Rect(0, 0, textureSize, textureSize), Vector2.one * 0.5f, pixelsPerUnit);
        sr.sprite = sprite;
        sr.color = Color.black;
        // Make sure overlay covers world: set order/sorting in inspector (Overlay layer, high order)
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // regenerate preview texture in editor when values change
        // use a delayed call to avoid 'SendMessage cannot be called during OnValidate' errors
        if (!Application.isPlaying)
        {
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            UnityEditor.EditorApplication.delayCall += () => { if (this != null) Regenerate(); };
        }
    }
#endif

    // Optional helper to regenerate at runtime if you change settings in play mode
    [ContextMenu("Regenerate Texture")]
    void Regenerate()
    {
        if (tex != null) DestroyImmediate(tex);
        // recompute diameter & scale (in case camera or settings changed)
        if (sceneCamera != null && sceneCamera.orthographic)
        {
            float worldHeight = sceneCamera.orthographicSize * 2f;
            float worldWidth = worldHeight * sceneCamera.aspect;
            float diagonal = Mathf.Sqrt(worldWidth * worldWidth + worldHeight * worldHeight);
            neededDiameter = diagonal * overlayScaleMultiplier;
        }
        else
        {
            neededDiameter = Mathf.Max(40f, radius * 10f) * overlayScaleMultiplier;
        }
        float defaultSpriteDiameter = 2f;
        float globalScale = (neededDiameter * Mathf.Max(1f, coverageMargin)) / defaultSpriteDiameter;
        transform.localScale = Vector3.one * globalScale;

        GenerateTexture();
        ApplySprite();
    }
}