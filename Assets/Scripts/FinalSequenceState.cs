using UnityEngine;

public static class FinalSequenceState
{
    public static bool Stage5BossDefeated { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetState()
    {
        Stage5BossDefeated = false;
    }

    public static void MarkStage5BossDefeated()
    {
        Stage5BossDefeated = true;
    }
}
