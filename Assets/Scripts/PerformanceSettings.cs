using UnityEngine;

// Keeps CPU and GPU use low. Runs once at startup; no scene setup needed.
// The game is 2D UI, so 60 fps looks smooth. Without a cap, VSync on a 144 Hz
// screen makes Unity draw 144 frames a second, which more than doubles the work
// and takes CPU time away from the Python hand tracker.
public static class PerformanceSettings
{
    public const int TargetFps = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Apply()
    {
        QualitySettings.vSyncCount = 0;          // VSync would follow the monitor (144 Hz)
        Application.targetFrameRate = TargetFps;

        QualitySettings.antiAliasing = 0;        // sprites and UI do not need MSAA
        QualitySettings.shadows = ShadowQuality.Disable;
        QualitySettings.realtimeReflectionProbes = false;
        QualitySettings.softParticles = false;
        // The game has no rigidbodies or colliders: skip the physics steps (50 a second).
        Physics.simulationMode = SimulationMode.Script;
        Physics2D.simulationMode = SimulationMode2D.Script;
    }
}
