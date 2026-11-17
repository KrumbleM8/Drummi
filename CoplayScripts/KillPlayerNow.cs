using UnityEngine;

/// <summary>PLAY-MODE ONLY: depletes DungeonHealth to trigger the death path mid-cycle.</summary>
public static class KillPlayerNow
{
    public static string Execute()
    {
        if (!Application.isPlaying) return "FAIL: not in play mode.";
        var health = Object.FindFirstObjectByType<DungeonHealth>(FindObjectsInactive.Include);
        if (health == null) return "FAIL: DungeonHealth not found.";
        health.TakeDrainDamage(9999f);
        return $"Drain applied. CurrentHealth={health.CurrentHealth}";
    }
}
