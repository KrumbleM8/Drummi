using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using KrumbleHut.Drummi.Dungeons;

/// <summary>
/// Phase 3 loop test. Relative to the next bar boundary B:
///   Bar A (B+0):  aimed flicks slots 0,2; mis-aimed (+40° off target, still up-cone) slot 4; slot 6 skipped
///                 → expect Perfect x2, BadAim x1, missed x2 → fizzleCount 3
///   Bar B (B+4):  ONE aimed flick released 250ms after slot 0 → AMENDMENT PROOF:
///                 slot 0 consumed as Fizzled/OutOfWindow, Missed = slots 2,4,6 only → fizzleCount 4 (old rule: 5)
///   Bar C (B+8):  aimed on all four slots → whiff; lock-on flights + beat respawns visible
/// Aim directions are steered each tick toward the target's live screen position.
/// </summary>
public static class SimulateFlickPhase3
{
    private enum Mode { Aimed, MisAimed, }

    private struct Planned
    {
        public double ReleaseVt;
        public Mode Mode;
        public Planned(double vt, Mode m) { ReleaseVt = vt; Mode = m; }
    }

    private static readonly List<Planned> _plan = new List<Planned>();
    private static InputSettings.EditorInputBehaviorInPlayMode _savedInputBehavior;
    private static Touchscreen _touchscreen;
    private static FlickTargetSweep _sweep;
    private static Camera _cam;
    private static int _index;
    private static int _phase;
    private static int _touchId;
    private static readonly Vector2 Start = new Vector2(540f, 200f);
    private const float DragLen = 600f;

    public static void Execute()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[SimP3] Not in play mode."); return; }
        if (TimingCoordinator.Instance == null) { Debug.LogError("[SimP3] No TimingCoordinator."); return; }
        _sweep = Object.FindFirstObjectByType<FlickTargetSweep>();
        _cam = Camera.main;
        if (_sweep == null || _cam == null) { Debug.LogError("[SimP3] Missing sweep or camera."); return; }

        // Game-view focus routing is flaky for editor-queued events — bypass it entirely
        // for the duration of the injection, then restore the project setting.
        _savedInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode =
            InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        Debug.Log($"[SimP3] editorInputBehaviorInPlayMode: {_savedInputBehavior} → AllDeviceInputAlwaysGoesToGameView (restored on completion)");

        double b = TimingCoordinator.Instance.NextBar.BarStartTime;

        _plan.Clear();
        // Bar A
        _plan.Add(new Planned(b + 0.0, Mode.Aimed));
        _plan.Add(new Planned(b + 1.0, Mode.Aimed));
        _plan.Add(new Planned(b + 2.0, Mode.MisAimed));
        // Bar B — amendment proof: 250ms late on slot 0, aimed.
        _plan.Add(new Planned(b + 4.25, Mode.Aimed));
        // Bar C
        _plan.Add(new Planned(b + 8.0, Mode.Aimed));
        _plan.Add(new Planned(b + 9.0, Mode.Aimed));
        _plan.Add(new Planned(b + 10.0, Mode.Aimed));
        _plan.Add(new Planned(b + 11.0, Mode.Aimed));

        _touchscreen = Touchscreen.current;
        if (_touchscreen == null) _touchscreen = InputSystem.AddDevice<Touchscreen>();

        _index = 0;
        _phase = 0;
        _touchId = 300;
        EditorApplication.update += Pump;
        Debug.Log($"[SimP3] Plan armed — B={b:F4}, {_plan.Count} gestures.");
    }

    private static Vector2 CurrentDir(Mode mode)
    {
        Vector2 targetScreen = _cam.WorldToScreenPoint(_sweep.PositionAt(GameClock.Instance.GameTime));
        Vector2 aimed = (targetScreen - Start).normalized;
        if (mode == Mode.Aimed) return aimed;

        // MisAimed: rotate 40° off the required direction, picking the rotation that
        // stays closest to straight up so the drag remains a Valid (up-cone) flick.
        Vector2 cw = Rotate(aimed, -40f);
        Vector2 ccw = Rotate(aimed, 40f);
        return Vector2.Angle(cw, Vector2.up) < Vector2.Angle(ccw, Vector2.up) ? cw : ccw;
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    private static void Pump()
    {
        if (!EditorApplication.isPlaying || _index >= _plan.Count)
        {
            EditorApplication.update -= Pump;
            InputSystem.settings.editorInputBehaviorInPlayMode = _savedInputBehavior;
            Debug.Log($"[SimP3] Injection complete. Input behavior restored to {_savedInputBehavior}.");
            return;
        }
        if (GameClock.Instance == null) return;

        double now = GameClock.Instance.GameTime;
        Planned p = _plan[_index];

        if (_phase == 0)
        {
            if (now >= p.ReleaseVt - 0.25)
            {
                Queue(UnityEngine.InputSystem.TouchPhase.Began, Start);
                _phase = 1;
            }
        }
        else
        {
            Vector2 dir = CurrentDir(p.Mode);
            if (now >= p.ReleaseVt)
            {
                Queue(UnityEngine.InputSystem.TouchPhase.Ended, Start + dir * DragLen);
                _index++;
                _phase = 0;
                _touchId++;
            }
            else
            {
                float t = Mathf.Clamp01((float)((now - (p.ReleaseVt - 0.25)) / 0.25));
                Queue(UnityEngine.InputSystem.TouchPhase.Moved, Start + dir * DragLen * t);
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
