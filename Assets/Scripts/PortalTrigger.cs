using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

public class PortalTrigger : MonoBehaviour
{
    [SerializeField] string sceneToLoad = "Floor2";
    [Tooltip("Tag used to find remaining enemies (defaults to 'Enemy')")]
    [SerializeField] string enemyTag = "Enemy";

    [Header("UI (optional)")]
    [Tooltip("Assign a TextMeshProUGUI to show messages to the player when trying to enter the portal while enemies remain.")]
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private float messageDuration = 2f;

    [Header("Filtering (optional)")]
    [Tooltip("If assigned, only enemies inside this Collider2D will be considered (useful for per-stage checks)")]
    [SerializeField] private Collider2D areaCollider;

    private Coroutine messageCoroutine;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (AreEnemiesRemaining())
        {
            ShowMessage("Can't enter portal — enemies remain");
            return;
        }

        // Optional: Debug.Log("Player hit portal");
        SceneManager.LoadScene(sceneToLoad);
    }

    private bool AreEnemiesRemaining()
    {
        return CountRemainingEnemiesInArea() > 0;
    }

    private int CountRemainingEnemiesInArea()
    {
        var enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        if (enemies == null || enemies.Length == 0) return 0;

        int count = 0;
        foreach (var e in enemies)
        {
            if (e == null || !e.activeInHierarchy) continue;

            // If an area collider is assigned, skip enemies outside it
            if (areaCollider != null)
            {
                if (!areaCollider.bounds.Contains(e.transform.position))
                    continue;
            }

            // Check Enemy.IsDead() if available
            var enemyComp = e.GetComponent<Enemy>();
            if (enemyComp != null)
            {
                if (!enemyComp.IsDead()) count++;
                continue;
            }

            // No Enemy component: treat active object as alive
            count++;
        }

        return count;
    }

    private void ShowMessage(string text)
    {
        EnsureMessageText();
        if (messageText == null)
        {
            Debug.LogWarning($"PortalTrigger: No message Text assigned and none found in scene. Message '{text}' skipped.");
            return;
        }
        if (messageCoroutine != null) StopCoroutine(messageCoroutine);
        messageCoroutine = StartCoroutine(ShowMessageRoutine(text));
    }

    private IEnumerator ShowMessageRoutine(string text)
    {
        messageText.text = text;
        messageText.gameObject.SetActive(true);
        yield return new WaitForSeconds(messageDuration);
        messageText.gameObject.SetActive(false);
        messageCoroutine = null;
    }

    private void EnsureMessageText()
    {
        if (messageText != null) return;
        var all = FindObjectsOfType<TMPro.TextMeshProUGUI>(includeInactive: true);
        foreach (var t in all)
        {
            var n = t.name.ToLower();
            if (n.Contains("message") || n.Contains("status") || n.Contains("hud") || n.Contains("screen") || n.Contains("notice"))
            {
                messageText = t;
                Debug.Log($"PortalTrigger: auto-assigned messageText -> {t.name}");
                return;
            }
        }
    }
}