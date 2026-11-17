using System.Reflection;
using System.Text;
using KrumbleHut.Drummi.Dungeons;
using KrumbleHut.Drummi.FlashCards;
using UnityEngine;

/// <summary>
/// PHASE 4.1c play-mode drivers for the FlashCards scene.
///   Execute            — start endless run WITH the synthetic tap harness
///                        (cards mostly clear → exercises the escalation ladder).
///   ExecuteStrikeTest  — start endless run with NO taps (every card Fails → 3
///                        strikes → results).
///   ExecuteReplay      — simulate the Again button (ResetGameValues + StartFromMenu).
///   ExecuteTogglePause — toggle pause via GameManager.
///   ExecuteStatus      — dump run state (HUD texts + controller/mode flags).
/// </summary>
public static class FlashCardsEndlessDrivers
{
    public static string Execute()
    {
        if (!Application.isPlaying) return "FAIL: not in play mode";
        var (mode, controller, reader, err) = Find();
        if (err != null) return err;

        // Attach the tap harness: inactive-host → AddComponent → wire → activate
        // (OnEnable subscribes only when refs are present).
        var existing = Object.FindFirstObjectByType<FlashCardTestRunner>(FindObjectsInactive.Include);
        if (existing == null)
        {
            var host = new GameObject("FlashCardTestRunner (runtime)");
            host.SetActive(false);
            var runner = host.AddComponent<FlashCardTestRunner>();
            SetPrivate(runner, "controller",  controller);
            SetPrivate(runner, "inputReader", reader);
            host.SetActive(true);
        }
        else
        {
            existing.enabled = true;
        }

        mode.StartFromMenu();
        return "OK: endless run started WITH tap harness";
    }

    public static string ExecuteStrikeTest()
    {
        if (!Application.isPlaying) return "FAIL: not in play mode";
        var (mode, _, _, err) = Find();
        if (err != null) return err;

        var runner = Object.FindFirstObjectByType<FlashCardTestRunner>(FindObjectsInactive.Include);
        if (runner != null) runner.enabled = false;

        mode.StartFromMenu();
        return "OK: endless run started with NO taps (expect 3 strikes in 3 cards)";
    }

    public static string ExecuteReplay()
    {
        if (!Application.isPlaying) return "FAIL: not in play mode";
        var (mode, _, _, err) = Find();
        if (err != null) return err;
        GameManager.instance.ResetGameValues();
        mode.StartFromMenu();
        return "OK: replay (ResetGameValues + StartFromMenu)";
    }

    public static string ExecuteTogglePause()
    {
        if (!Application.isPlaying) return "FAIL: not in play mode";
        GameManager.instance.TogglePause();
        return $"OK: toggled pause → IsPaused={GameClock.Instance.IsPaused} time={GameClock.Instance.GameTime:F3}";
    }

    public static string ExecuteStatus()
    {
        if (!Application.isPlaying) return "FAIL: not in play mode";
        var (mode, controller, _, err) = Find();
        if (err != null) return err;

        var sb = new StringBuilder();
        sb.Append($"controller.IsRunning={controller.IsRunning}  ");
        sb.Append($"score={mode.Score}  perfects={mode.TotalPerfectHits}  ");
        foreach (var name in new[] { "_runActive", "_cardsCleared", "_strikes", "_streak", "_bestStreak", "_appliedStep" })
            sb.Append($"{name}={GetPrivate(mode, name)}  ");
        sb.Append($"paused={GameClock.Instance.IsPaused}  t={GameClock.Instance.GameTime:F2}");
        return sb.ToString();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static (FlashCardsModeController, FlashCardController, DungeonInputReader, string) Find()
    {
        var mode       = Object.FindFirstObjectByType<FlashCardsModeController>(FindObjectsInactive.Include);
        var controller = Object.FindFirstObjectByType<FlashCardController>(FindObjectsInactive.Include);
        var reader     = Object.FindFirstObjectByType<DungeonInputReader>(FindObjectsInactive.Include);
        if (mode == null || controller == null || reader == null)
            return (null, null, null, "FAIL: FlashCards systems not found — is the FlashCards scene loaded?");
        return (mode, controller, reader, null);
    }

    private static void SetPrivate(object target, string field, object value)
        => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(target, value);

    private static object GetPrivate(object target, string field)
        => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(target);
}
