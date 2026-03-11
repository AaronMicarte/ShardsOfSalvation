using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private float offsetX = 3.0f;
    private bool disabledForCinemachine;

    // Use this for initialization
    void Start()
    {
        // If this camera is using Cinemachine, this legacy follow script should not run.
        disabledForCinemachine = GetComponent("CinemachineBrain") != null;
        if (disabledForCinemachine)
        {
            enabled = false;
            return;
        }

        if (player == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj == null)
                playerObj = GameObject.Find("Player");

            if (playerObj != null)
                player = playerObj.transform;
        }
    }

    // Use LateUpdate for camera movement to ensure the player has moved for the frame
    void LateUpdate()
    {
        if (player == null) return;

        transform.position = new Vector3(player.position.x + offsetX, transform.position.y, transform.position.z);
    }
}
