using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// Queues synthetic Mouse state events over successive frames to simulate one upward
/// drag-release flick: press at 30% height, drag to 60% over ~8 frames, release.
/// Verifies the FlickInputReader mouse path end-to-end in a dungeon flick room.
/// </summary>
public static class SimulateMouseFlick
{
    private static EditorApplication.CallbackFunction _tick;
    private static int _frame;

    public static void Execute()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[MouseSim] Enter play mode first."); return; }
        if (Mouse.current == null) { Debug.LogError("[MouseSim] No mouse device."); return; }

        if (_tick != null) EditorApplication.update -= _tick;
        _frame = 0;
        _tick = Tick;
        EditorApplication.update += _tick;
        Debug.Log("[MouseSim] Synthetic flick started");
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) { Stop(); return; }

        const int dragFrames = 8;
        float w = Screen.width, h = Screen.height;
        float t = Mathf.Clamp01(_frame / (float)dragFrames);
        var pos = new Vector2(w * 0.5f, Mathf.Lerp(h * 0.3f, h * 0.6f, t));

        bool pressed = _frame <= dragFrames; // released on the frame after the drag ends
        InputSystem.QueueStateEvent(Mouse.current, new MouseState
        {
            position = pos,
            buttons  = (ushort)(pressed ? 1 : 0),
        });

        _frame++;
        if (_frame > dragFrames + 2) { Stop(); Debug.Log("[MouseSim] Synthetic flick complete"); }
    }

    private static void Stop()
    {
        if (_tick != null) EditorApplication.update -= _tick;
        _tick = null;
    }
}
