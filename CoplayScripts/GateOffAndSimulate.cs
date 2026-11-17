using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using KrumbleHut.Drummi.Dungeons;

public static class GateOffAndSimulate
{
    private const int MoveFrames = 6;
    private static Touchscreen _touchscreen;
    private static int _frame;

    public static void Execute()
    {
        var reader = Object.FindFirstObjectByType<FlickInputReader>();
        if (reader == null)
        {
            Debug.LogError("[SimFlick] No FlickInputReader found.");
            return;
        }
        reader.EncounterActive = false;
        Debug.Log("[SimFlick] EncounterActive set to FALSE — injecting one upward flick, expecting silent drop.");

        _touchscreen = Touchscreen.current;
        if (_touchscreen == null) _touchscreen = InputSystem.AddDevice<Touchscreen>();

        _frame = 0;
        EditorApplication.update += Pump;
    }

    private static void Pump()
    {
        if (!EditorApplication.isPlaying || _frame > MoveFrames + 1)
        {
            EditorApplication.update -= Pump;
            Debug.Log("[SimFlick] Gated gesture injection complete.");
            return;
        }

        Vector2 start = new Vector2(540f, 200f);
        Vector2 end = new Vector2(540f, 800f);

        if (_frame == 0)
            Queue(UnityEngine.InputSystem.TouchPhase.Began, start);
        else if (_frame <= MoveFrames)
            Queue(UnityEngine.InputSystem.TouchPhase.Moved, Vector2.Lerp(start, end, (float)_frame / MoveFrames));
        else
            Queue(UnityEngine.InputSystem.TouchPhase.Ended, end);

        _frame++;
    }

    private static void Queue(UnityEngine.InputSystem.TouchPhase phase, Vector2 position)
    {
        InputSystem.QueueStateEvent(_touchscreen, new TouchState
        {
            touchId = 9,
            phase = phase,
            position = position,
        });
    }
}
