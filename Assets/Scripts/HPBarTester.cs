using UnityEngine;
using UnityEngine.InputSystem;

public class HPBarTester : MonoBehaviour
{
    public PlayerHealth playerHealth;

    private void Update()
    {
        if (playerHealth == null) return;

        if (Keyboard.current.hKey.wasPressedThisFrame)
        {
            playerHealth.TakeDamage(1);
        }

        if (Keyboard.current.jKey.wasPressedThisFrame)
        {
            playerHealth.Heal(1);
        }

        if (Keyboard.current.lKey.wasPressedThisFrame)
        {
            int dmg = Random.Range(1, 3);
            playerHealth.TakeDamage(dmg);
        }
    }
}
