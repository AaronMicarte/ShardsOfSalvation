using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class ButtonSound : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    public AudioClip hoverSfx;
    public AudioClip clickSfx;
    [Range(0f, 1f)] public float volume = 0.8f;
    public AudioSource audioSource; // optional: assign an AudioSource

    void Start()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverSfx == null) return;
        if (audioSource != null) audioSource.PlayOneShot(hoverSfx, volume);
        else AudioSource.PlayClipAtPoint(hoverSfx, Camera.main != null ? Camera.main.transform.position : Vector3.zero, volume);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (clickSfx == null) return;
        if (audioSource != null) audioSource.PlayOneShot(clickSfx, volume);
        else AudioSource.PlayClipAtPoint(clickSfx, Camera.main != null ? Camera.main.transform.position : Vector3.zero, volume);
    }
}
