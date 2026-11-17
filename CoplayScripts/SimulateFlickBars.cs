using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// Play-mode injector for the Phase 2 loop test. Schedules beat-timed flick gestures
/// (via GameClock virtual time) relative to the NEXT bar boundary B:
///   Bar A (B+0):  valid on slots 0,2; wrong-direction near slot 6; slot 4 skipped → 3 fizzles
///   Bar B (B+4):  valid on all four slots → whiff
///   Bar C (B+8):  valid on all four + one extra release → 1 ExtraRelease fizzle
///   Bar D (B+12): valid released 250ms late on slot 0 → OutOfWindow; slots 2/4/6 missed
/// Each gesture: Began 0.25s before target, Moved each frame, Ended once GameTime >= target.
/// </summary>
public static class SimulateFlickBars
{
    private struct Planned
    {
        public double ReleaseVt;
        public bool Horizontal;
        public Planned(double vt, bool horizontal) { ReleaseVt = vt; Horizontal = horizontal; }
    }

    private static readonly List<Planned> _plan = new List<Planned>();
    private static Touchscreen _touchscreen;
    private static int _index;
    private static int _phase; // 0 = waiting for began time, 1 = dragging
    private static int _touchId;

    public static void Execute()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[SimBars] Not in play mode."); return; }
        var coord = Object.FindFirstObjectByType<TimingCoordinator>();
        if (coord == null || TimingCoordinator.Instance == null)
        {
            Debug.LogError("[SimBars] TimingCoordinator missing/uninitialized.");
            return;
        }

        // Give the Game view focus: with the default PointersAndKeyboardsRespectGameViewFocus
        // input setting, touchscreen events queued while the Game view is unfocused are
        // consumed by the EDITOR input update and never reach the player loop.
        var gameViewType = System.Type.GetType("UnityEditor.GameView,UnityEditor");
        if (gameViewType != null) EditorWindow.GetWindow(gameViewType).Focus();

        double b = TimingCoordinator.Instance.NextBar.BarStartTime;

        _plan.Clear();
        // Bar A: slots 0,2 valid; slot 6 wrong-direction; slot 4 skipped.
        _plan.Add(new Planned(b + 0.0, false));
        _plan.Add(new Planned(b + 1.0, false));
        _plan.Add(new Planned(b + 3.0, true));
        // Bar B: all four slots valid.
        _plan.Add(new Planned(b + 4.0, false));
        _plan.Add(new Planned(b + 5.0, false));
        _plan.Add(new Planned(b + 6.0, false));
        _plan.Add(new Planned(b + 7.0, false));
        // Bar C: all four valid + one extra.
        _plan.Add(new Planned(b + 8.0, false));
        _plan.Add(new Planned(b + 9.0, false));
        _plan.Add(new Planned(b + 10.0, false));
        _plan.Add(new Planned(b + 11.0, false));
        _plan.Add(new Planned(b + 11.3, false));
        // Bar D: slot 0 released 250ms late → OutOfWindow.
        _plan.Add(new Planned(b + 12.25, false));

        _touchscreen = Touchscreen.current;
        if (_touchscreen == null) _touchscreen = InputSystem.AddDevice<Touchscreen>();

        _index = 0;
        _phase = 0;
        _touchId = 100;
        EditorApplication.update += Pump;
        Debug.Log($"[SimBars] Plan armed — base bar start B={b:F4} vt, {_plan.Count} gestures.");
    }

    private static void Pump()
    {
        if (!EditorApplication.isPlaying || _index >= _plan.Count)
        {
            EditorApplication.update -= Pump;
            Debug.Log("[SimBars] Injection complete.");
            return;
        }
        if (GameClock.Instance == null) return;

        double now = GameClock.Instance.GameTime;
        Planned p = _plan[_index];

        Vector2 start = p.Horizontal ? new Vector2(300f, 500f) : new Vector2(540f, 200f);
        Vector2 end   = p.Horizontal ? new Vector2(900f, 500f) : new Vector2(540f, 800f);

        if (_phase == 0)
        {
            if (now >= p.ReleaseVt - 0.25)
            {
                Queue(UnityEngine.InputSystem.TouchPhase.Began, start);
                _phase = 1;
            }
        }
        else
        {
            if (now >= p.ReleaseVt)
            {
                Queue(UnityEngine.InputSystem.TouchPhase.Ended, end);
                _index++;
                _phase = 0;
                _touchId++;
            }
            else
            {
                float t = Mathf.Clamp01((float)((now - (p.ReleaseVt - 0.25)) / 0.25));
                Queue(UnityEngine.InputSystem.TouchPhase.Moved, Vector2.Lerp(start, end, t));
            }
        }
    }

    private static void Queue(UnityEngine.InputSystem.TouchPhase phase, Vector2 position)
    {
        InputSystem.QueueStateEvent(_touchscreen, new TouchState
        {
            touchId = _touchId,
            phase = phase,
            position = position,
        });
    }
}
