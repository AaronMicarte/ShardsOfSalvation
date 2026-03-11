using Unity.Cinemachine;
using UnityEngine;

[RequireComponent(typeof(CinemachineCamera))]
[RequireComponent(typeof(CinemachinePositionComposer))]
public class CinemachineFacingOffset : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform player;

    [Header("Offset")]
    [SerializeField] private float offsetX = 3f;
    [SerializeField] private float offsetY = 0f;
    [SerializeField] private float offsetZ = 0f;

    [Header("Smoothing")]
    [SerializeField, Tooltip("How long it takes the X offset to settle into the new facing direction")]
    private float smoothTime = 0.18f;
    [SerializeField, Tooltip("Optional cap on how fast the offset can move while smoothing. Set to 0 for unlimited")]
    private float maxSpeed = 0f;

    [Header("Facing Detection")]
    [SerializeField, Tooltip("Minimum absolute localScale.x to consider a valid facing direction")]
    private float facingEpsilon = 0.01f;

    private CinemachineCamera cmCamera;
    private CinemachinePositionComposer composer;
    private float currentOffsetX;
    private float offsetXVelocity;

    private void Awake()
    {
        cmCamera = GetComponent<CinemachineCamera>();
        composer = GetComponent<CinemachinePositionComposer>();

        if (player == null && cmCamera != null)
            player = cmCamera.Follow;

        if (player == null)
        {
            var tagged = GameObject.FindWithTag("Player");
            if (tagged != null)
                player = tagged.transform;
        }

        if (composer != null)
            currentOffsetX = composer.TargetOffset.x;
    }

    private void LateUpdate()
    {
        if (composer == null)
            return;

        if (player == null)
        {
            if (cmCamera != null && cmCamera.Follow != null)
                player = cmCamera.Follow;
            else
                return;
        }

        float sx = player.localScale.x;
        if (Mathf.Abs(sx) < facingEpsilon)
            return;

        float sign = sx > 0f ? 1f : -1f;
        float targetOffsetX = Mathf.Abs(offsetX) * sign;
        float speedLimit = maxSpeed > 0f ? maxSpeed : Mathf.Infinity;

        currentOffsetX = Mathf.SmoothDamp(
            currentOffsetX,
            targetOffsetX,
            ref offsetXVelocity,
            Mathf.Max(0.001f, smoothTime),
            speedLimit,
            Time.deltaTime);

        Vector3 newOffset = new Vector3(currentOffsetX, offsetY, offsetZ);

        if (composer.TargetOffset != newOffset)
            composer.TargetOffset = newOffset;
    }
}
