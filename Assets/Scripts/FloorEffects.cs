using UnityEngine;

public class FloorEffects : MonoBehaviour
{
    [Header("Gravity")]
    [SerializeField] bool enableGravityDistortion = true;

    [Header("Controls")]
    [SerializeField] bool enableControlsInversion = true;

    private GravityDistortion activeGravityDistortion;
    private ControlsInversion activeControlsInversion;

    private Player ResolvePlayer()
    {
        // Prefer tagged object, but handle common setup mistakes where a child is tagged instead of the root.
        var tagged = GameObject.FindGameObjectWithTag("Player");
        if (tagged != null)
        {
            var fromTagged = tagged.GetComponent<Player>()
                           ?? tagged.GetComponentInParent<Player>()
                           ?? tagged.GetComponentInChildren<Player>();
            if (fromTagged != null) return fromTagged;
        }

        // Fallback: find any active Player component in the scene.
        var players = FindObjectsByType<Player>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (players != null && players.Length > 0)
            return players[0];

        return null;
    }

    void Start()
    {
        var player = ResolvePlayer();
        if (player == null)
        {
            Debug.LogWarning("FloorEffects: Could not resolve a Player in scene. Effects were not started.");
            return;
        }

        if (enableGravityDistortion)
        {
            activeGravityDistortion = player.GetComponent<GravityDistortion>()
                                  ?? player.GetComponentInChildren<GravityDistortion>()
                                  ?? player.GetComponentInParent<GravityDistortion>();
            activeGravityDistortion?.StartDistortion();
        }

        if (enableControlsInversion)
        {
            activeControlsInversion = player.GetComponent<ControlsInversion>()
                                 ?? player.GetComponentInChildren<ControlsInversion>()
                                 ?? player.GetComponentInParent<ControlsInversion>();
            activeControlsInversion?.StartInversion();
        }
    }

    void OnDisable()
    {
        // Stop effects safely when this object is disabled (covers editor stop & runtime)
        if (enableGravityDistortion)
        {
            if (activeGravityDistortion == null)
            {
                var player = ResolvePlayer();
                activeGravityDistortion = player != null
                    ? (player.GetComponent<GravityDistortion>()
                    ?? player.GetComponentInChildren<GravityDistortion>()
                    ?? player.GetComponentInParent<GravityDistortion>())
                    : null;
            }
            activeGravityDistortion?.StopDistortion();
        }

        if (enableControlsInversion)
        {
            if (activeControlsInversion == null)
            {
                var player = ResolvePlayer();
                activeControlsInversion = player != null
                    ? (player.GetComponent<ControlsInversion>()
                    ?? player.GetComponentInChildren<ControlsInversion>()
                    ?? player.GetComponentInParent<ControlsInversion>())
                    : null;
            }
            activeControlsInversion?.StopInversion();
        }
    }

    void OnDestroy()
    {
        // Keep OnDestroy too in case the object is destroyed
        OnDisable();
    }
}