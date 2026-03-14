using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class HPBarTester : MonoBehaviour
{
    public PlayerHealth playerHealth;

    private void Update()
    {
        if (playerHealth == null) return;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
        {
            playerHealth.TakeDamage(1);
        }

        if (Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame)
        {
            playerHealth.Heal(1);
        }

        if (Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame)
        {
            int dmg = Random.Range(1, 3);
            playerHealth.TakeDamage(dmg);
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.H))
        {
            playerHealth.TakeDamage(1);
        }

        if (Input.GetKeyDown(KeyCode.J))
        {
            playerHealth.Heal(1);
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            int dmg = Random.Range(1, 3);
            playerHealth.TakeDamage(dmg);
        }
#endif
    }
}
