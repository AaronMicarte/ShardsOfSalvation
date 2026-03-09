using UnityEngine;
using System.Collections;

/// <summary>
/// Lightweight helper that fades an AudioSource to silence, then stops it.
/// Lives on the same GameObject as the AudioSource so fade continues even if other objects are destroyed.
/// </summary>
public class AudioSourceFadeHelper : MonoBehaviour
{
    private Coroutine fadeRoutine;

    public void FadeOutAndStop(AudioSource source, float duration)
    {
        if (source == null) return;

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(FadeRoutine(source, duration));
    }

    private IEnumerator FadeRoutine(AudioSource source, float duration)
    {
        if (source == null)
        {
            fadeRoutine = null;
            yield break;
        }

        float safeDuration = Mathf.Max(0f, duration);
        if (safeDuration <= 0f)
        {
            source.volume = 0f;
            source.mute = true;
            if (source.isPlaying) source.Stop();
            fadeRoutine = null;
            Destroy(this);
            yield break;
        }

        float startVolume = source.volume;
        float elapsed = 0f;

        while (source != null && elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            source.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        if (source != null)
        {
            source.volume = 0f;
            source.mute = true;
            if (source.isPlaying) source.Stop();
        }

        fadeRoutine = null;
        Destroy(this);
    }
}
