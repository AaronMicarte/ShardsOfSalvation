using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;
using System.Collections;

[DisallowMultipleComponent]
public class DeathUIController : MonoBehaviour
{
    [Header("UI")]
    public GameObject deathPanel; // assign the root panel (inactive by default)
    public TextMeshProUGUI titleText; // "You are dead"
    public TextMeshProUGUI detailText; // optional details
    public Button retryButton;
    public Button mainMenuButton;

    [Header("Behavior")]
    public float fadeDuration = 0.35f;

    CanvasGroup panelCanvasGroup;

    void Awake()
    {
        if (deathPanel != null)
        {
            panelCanvasGroup = deathPanel.GetComponent<CanvasGroup>();
            if (panelCanvasGroup == null) panelCanvasGroup = deathPanel.AddComponent<CanvasGroup>();
            deathPanel.SetActive(false);
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.interactable = false;
            panelCanvasGroup.blocksRaycasts = false;
        }

        if (retryButton != null) retryButton.onClick.AddListener(OnRetryPressed);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenuPressed);
    }

    public void ShowDeath(string reason = null)
    {
        if (deathPanel == null) return;

        int livesRemaining = Player.GetRetryLivesRemaining();
        if (retryButton != null)
            retryButton.interactable = livesRemaining > 0;

        if (!string.IsNullOrEmpty(reason) && detailText != null) detailText.text = reason;
        else if (detailText != null)
            detailText.text = $"Retries left: {livesRemaining}/{Player.GetMaxRetryLivesPerRun()}";

        deathPanel.SetActive(true);
        StartCoroutine(FadeInPanel());
    }

    IEnumerator FadeInPanel()
    {
        if (panelCanvasGroup == null) yield break;
        float t = 0f;
        panelCanvasGroup.interactable = true;
        panelCanvasGroup.blocksRaycasts = true;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            panelCanvasGroup.alpha = Mathf.Clamp01(t / fadeDuration);
            yield return null;
        }
        panelCanvasGroup.alpha = 1f;
    }

    void OnRetryPressed()
    {
        if (Player.GetRetryLivesRemaining() <= 0)
        {
            if (detailText != null)
                detailText.text = $"No retries left. Return to Main Menu to refresh to {Player.GetMaxRetryLivesPerRun()} retries.";
            if (retryButton != null) retryButton.interactable = false;
            return;
        }

        // Retry from stage checkpoint: discard buffs gained after entering this stage.
        Player.RestoreDropBuffStacksFromStageCheckpoint();
        // Reset saved HP so the player starts full on retry
        PlayerHealth.ResetSavedHP();
        // reload current active scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void OnMainMenuPressed()
    {
        // Returning to menu starts a fresh run (no saved buffs).
        Player.ResetSavedDropBuffStacks();
        Player.ResetRetryLives();
        // go to main menu scene
        SceneManager.LoadScene("MainMenu");
    }
}