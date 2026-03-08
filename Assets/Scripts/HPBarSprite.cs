using UnityEngine;
using UnityEngine.UI;

public class HPBarSprite : MonoBehaviour
{
    public SpriteRenderer spriteRenderer;
    public Image uiImage;
    public Sprite[] frames;
    public int maxHP = 8;

    /// <summary>
    /// Set maximum HP used when computing percent-based fills.
    /// </summary>
    public void SetMaxHP(int max)
    {
        maxHP = Mathf.Max(1, max);
    }

    /// <summary>
    /// Update the visual HP bar. Supports two modes:
    /// - Sprite frames (frames.Length > 0): choose a frame based on percent
    /// - UI Image (uiImage != null and no frames): set fillAmount for continuous display
    /// </summary>
    public void SetHP(int currentHP)
    {
        float percent = 0f;
        if (maxHP > 0) percent = Mathf.Clamp01((float)currentHP / maxHP);

        // If a UI Image is assigned and no sprite frames are provided, use fillAmount for smooth display
        if (uiImage != null && (frames == null || frames.Length == 0))
        {
            uiImage.type = Image.Type.Filled;
            uiImage.fillMethod = Image.FillMethod.Horizontal;
            uiImage.fillAmount = percent;
            return;
        }

        // Otherwise fallback to sprite frame lookup (coarse steps)
        if (frames == null || frames.Length == 0) return;

        int index = Mathf.RoundToInt((frames.Length - 1) * (1f - percent));
        index = Mathf.Clamp(index, 0, frames.Length - 1);

        Sprite s = frames[index];
        if (spriteRenderer != null) spriteRenderer.sprite = s;
        if (uiImage != null) uiImage.sprite = s;
    }
}
