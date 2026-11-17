using System.Reflection;
using UnityEngine;
using UnityEditor;
using KrumbleHut.Drummi.Dungeons;

public static class TestBallRespawnDelay
{
    // Play-mode: force the ball onto the AwaitingRespawn path and report the
    // scheduled respawn time vs now (should be +0.15s virtual, not next beat).
    public static string Execute()
    {
        if (!Application.isPlaying) return "ERROR: not in play mode";
        var ball = Object.FindFirstObjectByType<FlickBallController>(FindObjectsInactive.Include);
        if (ball == null) return "ERROR: no FlickBallController";
        if (GameClock.Instance == null) return "ERROR: no GameClock";

        double before = GameClock.Instance.GameTime;
        typeof(FlickBallController)
            .GetMethod("ScheduleRespawn", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(ball, null);
        var f = typeof(FlickBallController).GetField("_respawnAtVirtual", BindingFlags.NonPublic | BindingFlags.Instance);
        double at = (double)f.GetValue(ball);
        return $"now={before:F4}, respawnAt={at:F4}, delta={(at - before):F4}s";
    }
}
