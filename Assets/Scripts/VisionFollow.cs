using UnityEngine;

public class VisionFollow : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Transform visionHoleMask;
    [SerializeField] private float smoothSpeed = 15f; // 0 = instant

    void LateUpdate()
    {
        if (player == null || visionHoleMask == null) return;
        Vector3 target = new Vector3(player.position.x, player.position.y, visionHoleMask.position.z);
        visionHoleMask.position = smoothSpeed <= 0f
            ? target
            : Vector3.Lerp(visionHoleMask.position, target, Time.deltaTime * smoothSpeed);
    }
}