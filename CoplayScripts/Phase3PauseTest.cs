using UnityEditor;
using UnityEngine;
using KrumbleHut.Drummi.Dungeons;

/// <summary>
/// PLAY-MODE ONLY: verification (c) — pause/resume during a FlashCard reveal bar
/// AND during a perform bar. Hooks EditorApplication.update; waits for the next
/// FlashCard encounter, pauses ~2 real seconds inside the reveal bar, resumes,
/// then pauses ~2 real seconds inside the perform bar, resumes, then detaches.
/// All state transitions are logged with virtual-time evidence.
/// </summary>
public static class Phase3PauseTest
{
    private enum Phase { WaitReveal, PausedInReveal, WaitPerform, PausedInPerform, Done }

    private static Phase  _phase;
    private static double _resumeAtRealTime;
    private static bool   _attached;

    public static string Execute()
    {
        if (!Application.isPlaying) return "FAIL: not in play mode.";
        if (_attached) return "Already attached.";
        _phase    = Phase.WaitReveal;
        _attached = true;
        EditorApplication.update += Tick;
        return "Phase3PauseTest attached — will pause in next reveal bar, then next perform bar.";
    }

    public static string Detach()
    {
        if (_attached) EditorApplication.update -= Tick;
        _attached = false;
        return "Detached.";
    }

    private static void Tick()
    {
        if (!Application.isPlaying) { Detach(); return; }

        var fc = Object.FindFirstObjectByType<FlashCardController>();
        var ph = Object.FindFirstObjectByType<PauseHandler>();
        if (fc == null || ph == null || GameClock.Instance == null) return;

        double now = Application.isPlaying && !GameClock.Instance.IsPaused
            ? GameClock.Instance.GameTime : double.NaN;
        var t = fc.CurrentTiming;

        switch (_phase)
        {
            case Phase.WaitReveal:
                if (fc.IsRunning && !GameClock.Instance.IsPaused
                    && now > t.CallStartTime + 1.0 && now < t.ResponseStartTime - 1.0)
                {
                    Debug.Log($"[PauseTest] PAUSING in REVEAL bar | vt={now:F6} " +
                              $"reveal=[{t.CallStartTime:F6},{t.ResponseStartTime:F6})");
                    ph.TogglePause();
                    _resumeAtRealTime = EditorApplication.timeSinceStartup + 2.0;
                    _phase = Phase.PausedInReveal;
                }
                break;

            case Phase.PausedInReveal:
                if (EditorApplication.timeSinceStartup >= _resumeAtRealTime)
                {
                    ph.TogglePause();
                    Debug.Log($"[PauseTest] RESUMED from reveal pause | vt={GameClock.Instance.GameTime:F6}");
                    _phase = Phase.WaitPerform;
                }
                break;

            case Phase.WaitPerform:
                if (fc.IsRunning && !GameClock.Instance.IsPaused
                    && now > t.ResponseStartTime + 1.0 && now < t.EvaluationTime - 1.0)
                {
                    Debug.Log($"[PauseTest] PAUSING in PERFORM bar | vt={now:F6} " +
                              $"perform=[{t.ResponseStartTime:F6},{t.EvaluationTime:F6})");
                    ph.TogglePause();
                    _resumeAtRealTime = EditorApplication.timeSinceStartup + 2.0;
                    _phase = Phase.PausedInPerform;
                }
                break;

            case Phase.PausedInPerform:
                if (EditorApplication.timeSinceStartup >= _resumeAtRealTime)
                {
                    ph.TogglePause();
                    Debug.Log($"[PauseTest] RESUMED from perform pause | vt={GameClock.Instance.GameTime:F6}");
                    _phase = Phase.Done;
                    Debug.Log("[PauseTest] DONE — detaching.");
                    Detach();
                }
                break;
        }
    }
}
