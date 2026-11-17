using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public static class DiagnoseTouch
{
    private static int _ticks;
    private static Touchscreen _ts;

    public static void Execute()
    {
        foreach (var d in InputSystem.devices)
            Debug.Log($"[DiagTouch] Device: {d.name} id={d.deviceId} enabled={d.enabled} added={d.added} native={d.native}");

        _ts = Touchscreen.current;
        Debug.Log($"[DiagTouch] Touchscreen.current = {(_ts == null ? "NULL" : _ts.name + " id=" + _ts.deviceId + " enabled=" + _ts.enabled)}");
        if (_ts == null) return;

        InputSystem.QueueStateEvent(_ts, new TouchState { touchId = 500, phase = UnityEngine.InputSystem.TouchPhase.Began, position = new Vector2(540f, 200f) });
        _ticks = 0;
        EditorApplication.update += Pump;
    }

    private static void Pump()
    {
        _ticks++;
        if (_ticks <= 10)
            Debug.Log($"[DiagTouch] tick {_ticks}: activeTouches={Touch.activeTouches.Count}");
        if (_ticks == 5)
            InputSystem.QueueStateEvent(_ts, new TouchState { touchId = 500, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = new Vector2(540f, 800f) });
        if (_ticks > 10)
            EditorApplication.update -= Pump;
    }
}
