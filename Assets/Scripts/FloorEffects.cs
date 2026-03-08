using UnityEngine;

public class FloorEffects : MonoBehaviour
{
    [Header("Gravity")]
    [SerializeField] bool enableGravityDistortion = true;

    [Header("Controls")]
    [SerializeField] bool enableControlsInversion = true;

    void Start()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (enableGravityDistortion)
        {
            var gd = player?.GetComponent<GravityDistortion>();
            gd?.StartDistortion();
        }

        if (enableControlsInversion)
        {
            var ci = player?.GetComponent<ControlsInversion>();
            if (ci != null) ci.StartInversion();
        }
    }

    void OnDisable()
    {
        // Stop effects safely when this object is disabled (covers editor stop & runtime)
        var player = GameObject.FindGameObjectWithTag("Player");
        if (enableGravityDistortion)
        {
            var gd = player?.GetComponent<GravityDistortion>();
            gd?.StopDistortion();
        }

        if (enableControlsInversion)
        {
            var ci = player?.GetComponent<ControlsInversion>();
            ci?.StopInversion();
        }
    }

    void OnDestroy()
    {
        // Keep OnDestroy too in case the object is destroyed
        OnDisable();
    }
}