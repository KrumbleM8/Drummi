using System.Collections.Generic;
using System.Reflection;
using KrumbleHut.Drummi.Pulse;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Play-mode test driver for the Pulse scene: polls on EditorApplication.update
/// and invokes PulseModeController.HandleTap() a few ms after each pending grid
/// beat, up to LastBeat, then stops.
///   Execute       — tap every beat 0..47 on the grid (through all five stages)
///   ExecuteSloppy — tap beats 0..63 but SKIP {20,22} (2 misses in bar 5 → drop
///                   one stage) and {40,42,44,46} (2 misses in bars 10 and 11 →
///                   revert to stage 0), then recover
///   ExecuteStop   — stop tapping now
/// </summary>
public static class PulseTapDriver
{
    private static long _lastBeat;
    private static HashSet<long> _skip = new();
    private static double _offset;
    private static bool _active;
    private static PulseModeController _mode;
    private static FieldInfo _origin, _interval, _nextBeat;
    private static MethodInfo _handleTap;

    public static void Execute()       => Start(47, new HashSet<long>());
    public static void ExecuteSloppy() => Start(63, new HashSet<long> { 20, 22, 40, 42, 44, 46 });
    public static void ExecuteStop()   { Stop(); Debug.Log("[TapDriver] stopped by request"); }

    private static void Start(long lastBeat, HashSet<long> skip)
    {
        if (!EditorApplication.isPlaying) { Debug.Log("[TapDriver] ABORT: not in play mode"); return; }
        _mode = Object.FindFirstObjectByType<PulseModeController>();
        if (_mode == null) { Debug.Log("[TapDriver] ABORT: no PulseModeController"); return; }
        var t = typeof(PulseModeController);
        const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
        _origin    = t.GetField("_origin", F);
        _interval  = t.GetField("_interval", F);
        _nextBeat  = t.GetField("_nextBeat", F);
        _handleTap = t.GetMethod("HandleTap", F);
        _offset = 0.012; _lastBeat = lastBeat; _skip = skip;
        Stop();
        EditorApplication.update += Tick;
        _active = true;
        Debug.Log($"[TapDriver] started: beats 0..{lastBeat}, skipping [{string.Join(",", skip)}]");
    }

    private static void Stop()
    {
        if (_active) EditorApplication.update -= Tick;
        _active = false;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || _mode == null || GameClock.Instance == null) { Stop(); return; }
        if (GameClock.Instance.IsPaused) return; // frozen GameTime would otherwise re-fire every frame
        double interval = (double)_interval.GetValue(_mode);
        if (interval <= 0) return;
        long beat = (long)_nextBeat.GetValue(_mode);
        if (beat > _lastBeat) { Stop(); Debug.Log("[TapDriver] done"); return; }
        if (_skip.Contains(beat)) return; // let the mode register the miss and advance
        double due = (double)_origin.GetValue(_mode) + beat * interval + _offset;
        double now = GameClock.Instance.GameTime;
        if (now < due || now > due + interval * 0.2) return;
        _handleTap.Invoke(_mode, null);
    }
}
