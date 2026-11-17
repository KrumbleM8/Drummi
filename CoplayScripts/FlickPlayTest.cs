using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using KrumbleHut.Drummi.Dungeons;
using KrumbleHut.Drummi.Flick;

public static class FlickPlayTest
{
    static GameObject FindAny(string name) =>
        Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
              .FirstOrDefault(t => t.name == name)?.gameObject;

    public static string StartRun()
    {
        var btn = FindAny("PlayButton")?.GetComponent<Button>();
        if (btn == null) return "PlayButton not found";
        btn.onClick.Invoke();
        return "PlayButton clicked";
    }

    public static string ClickAgain()
    {
        var btn = FindAny("AgainButton")?.GetComponent<Button>();
        if (btn == null) return "AgainButton not found";
        btn.onClick.Invoke();
        return "AgainButton clicked";
    }

    public static string TogglePause()
    {
        GameManager.instance.TogglePause();
        return $"Toggled pause. GameClock.IsPaused={GameClock.Instance.IsPaused}";
    }

    public static string Status()
    {
        var mode   = Object.FindFirstObjectByType<FlickModeController>();
        var health = Object.FindFirstObjectByType<DungeonHealth>();
        var ball   = Object.FindFirstObjectByType<FlickBallController>();
        var mgr    = Object.FindFirstObjectByType<FlickBeatManager>();
        var ballState = ball.GetType().GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ball);
        var mgrState  = mgr.GetType().GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(mgr);
        return $"score={mode.Score} perfects={mode.TotalPerfectHits} hp={health.CurrentHealth} " +
               $"ball={ballState}@{ball.transform.position} mgr={mgrState} " +
               $"paused={GameClock.Instance.IsPaused} vt={GameClock.Instance.GameTime:F3}";
    }

    public static string MusicStatus()
    {
        var am = AudioManager.instance;
        var src = am.speakers[0];
        return $"music clip={(src.clip != null ? src.clip.name : "none")} playing={src.isPlaying} " +
               $"time={src.time:F2} loop={src.loop} selectedSongIndex={am.selectedSongIndex}";
    }

    // Simulates quitting mid-Held: forces the reader into a tracked mouse drag and the
    // ball into Held, then depletes HP so game-over teardown runs from that state.
    public static string ForceHeldThenGameOver()
    {
        var reader = Object.FindFirstObjectByType<FlickInputReader>();
        var ball   = Object.FindFirstObjectByType<FlickBallController>();
        var health = Object.FindFirstObjectByType<DungeonHealth>();

        reader.GetType().GetField("_tracking",      BindingFlags.NonPublic | BindingFlags.Instance).SetValue(reader, true);
        reader.GetType().GetField("_mouseDragging", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(reader, true);
        var stateField = ball.GetType().GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
        var heldValue = System.Enum.Parse(stateField.FieldType, "Held");
        stateField.SetValue(ball, heldValue);
        ball.transform.position = new Vector3(1.7f, 0.4f, 0f); // mid-screen, away from home

        health.TakeMissDamage(100); // hard deplete → OnHealthDepleted → CompletMode
        return "Forced Held + depleted HP. " + Status();
    }
}
