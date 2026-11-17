using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using KrumbleHut.Drummi.Dungeons;

/// <summary>
/// Deterministic verification of the manual-aiming redesign — invokes
/// FlickBeatManager.HandleFlickReleased directly (bypasses flaky editor touch routing)
/// with precisely timed FlickRelease payloads. Relative to next bar boundary B:
///   Bar A: B+0.0 aimed (Perfect slot0) | B+1.0 mis-aimed 40° (BadAim consumes slot2)
///          | B+2.25 aimed, 250ms late (OutOfWindow consumes slot4) | B+3.0 aimed
///          (Perfect slot6) | B+3.3 aimed (ExtraRelease)
///          → Consumed all 4 (2 clean + 2 fizzled), Missed none, fizzleCount 3.
///   Bar B: all four slots aimed on-beat → clean whiff.
/// </summary>
public static class InvokeFlickReleases
{
    private struct Planned
    {
        public double ReleaseVt;
        public bool MisAimed;
        public Planned(double vt, bool misAimed) { ReleaseVt = vt; MisAimed = misAimed; }
    }

    private static readonly List<Planned> _plan = new List<Planned>();
    private static FlickBeatManager _manager;
    private static FlickTargetSweep _sweep;
    private static Camera _cam;
    private static MethodInfo _handler;
    private static int _index;
    private static readonly Vector2 Origin = new Vector2(540f, 200f);

    public static void Execute()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[InvokeF] Not in play mode."); return; }
        _manager = Object.FindFirstObjectByType<FlickBeatManager>();
        _sweep = Object.FindFirstObjectByType<FlickTargetSweep>();
        _cam = Camera.main;
        if (_manager == null || _sweep == null || _cam == null || TimingCoordinator.Instance == null)
        {
            Debug.LogError("[InvokeF] Missing manager/sweep/camera/coordinator.");
            return;
        }

        _handler = typeof(FlickBeatManager).GetMethod("HandleFlickReleased",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (_handler == null) { Debug.LogError("[InvokeF] HandleFlickReleased not found."); return; }

        double b = TimingCoordinator.Instance.NextBar.BarStartTime;

        _plan.Clear();
        // Bar A — one of each outcome.
        _plan.Add(new Planned(b + 0.0, false));  // Perfect slot 0
        _plan.Add(new Planned(b + 1.0, true));   // BadAim consumes slot 2
        _plan.Add(new Planned(b + 2.25, false)); // OutOfWindow consumes slot 4
        _plan.Add(new Planned(b + 3.0, false));  // Perfect slot 6
        _plan.Add(new Planned(b + 3.3, false));  // ExtraRelease
        // Bar B — clean whiff.
        _plan.Add(new Planned(b + 4.0, false));
        _plan.Add(new Planned(b + 5.0, false));
        _plan.Add(new Planned(b + 6.0, false));
        _plan.Add(new Planned(b + 7.0, false));

        _index = 0;
        EditorApplication.update += Pump;
        Debug.Log($"[InvokeF] Plan armed — B={b:F4}, {_plan.Count} releases.");
    }

    private static void Pump()
    {
        if (!EditorApplication.isPlaying || _index >= _plan.Count)
        {
            EditorApplication.update -= Pump;
            Debug.Log("[InvokeF] Invocation complete.");
            return;
        }
        if (GameClock.Instance == null) return;

        Planned p = _plan[_index];
        if (GameClock.Instance.GameTime < p.ReleaseVt) return;

        // Aimed = drag straight at the target's current position; mis-aimed = 40° off
        // (far outside the ~1.2wu hitbox at throw range).
        Vector2 targetScreen = _cam.WorldToScreenPoint(_sweep.PositionAt(GameClock.Instance.GameTime));
        Vector2 aimed = (targetScreen - Origin).normalized;
        Vector2 dir = p.MisAimed ? Rotate(aimed, 40f) : aimed;

        var release = new FlickRelease(p.ReleaseVt, dir * 600f, Origin, FlickClassification.Valid);
        _handler.Invoke(_manager, new object[] { release });
        _index++;
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }
}
