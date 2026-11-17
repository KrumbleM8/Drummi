using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// Injects synthetic touch gestures into the Input System while in play mode so the
/// real EnhancedTouch → FlickInputReader path is exercised without a human dragging.
/// Gesture 1: long upward drag (Valid). Gesture 2: long rightward drag (WrongDirection).
/// Gesture 3: tiny upward drag (TooShort).
/// </summary>
public static class SimulateFlicks
{
    private struct Gesture
    {
        public Vector2 Start;
        public Vector2 End;
        public Gesture(Vector2 s, Vector2 e) { Start = s; End = e; }
    }

    private static readonly Gesture[] Gestures =
    {
        new Gesture(new Vector2(540f, 200f), new Vector2(540f, 800f)),  // Valid (straight up)
        new Gesture(new Vector2(300f, 500f), new Vector2(900f, 500f)),  // WrongDirection (right)
        new Gesture(new Vector2(540f, 300f), new Vector2(545f, 320f)),  // TooShort
    };

    private const int MoveFrames = 6;
    private const int GapFrames = 40;

    private static Touchscreen _touchscreen;
    private static int _gestureIndex;
    private static int _frameInGesture;

    public static void Execute()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("[SimFlick] Not in play mode — aborting.");
            return;
        }

        _touchscreen = Touchscreen.current;
        if (_touchscreen == null)
        {
            _touchscreen = InputSystem.AddDevice<Touchscreen>();
            Debug.Log("[SimFlick] Added synthetic Touchscreen device.");
        }

        _gestureIndex = 0;
        _frameInGesture = 0;
        EditorApplication.update += Pump;
        Debug.Log("[SimFlick] Starting gesture injection.");
    }

    private static void Pump()
    {
        if (!EditorApplication.isPlaying || _gestureIndex >= Gestures.Length)
        {
            EditorApplication.update -= Pump;
            Debug.Log("[SimFlick] Gesture injection complete.");
            return;
        }

        Gesture g = Gestures[_gestureIndex];
        int f = _frameInGesture;

        if (f == 0)
        {
            Queue(UnityEngine.InputSystem.TouchPhase.Began, g.Start);
        }
        else if (f <= MoveFrames)
        {
            Vector2 pos = Vector2.Lerp(g.Start, g.End, (float)f / MoveFrames);
            Queue(UnityEngine.InputSystem.TouchPhase.Moved, pos);
        }
        else if (f == MoveFrames + 1)
        {
            Queue(UnityEngine.InputSystem.TouchPhase.Ended, g.End);
        }
        else if (f >= MoveFrames + 1 + GapFrames)
        {
            _gestureIndex++;
            _frameInGesture = 0;
            return;
        }

        _frameInGesture++;
    }

    private static void Queue(UnityEngine.InputSystem.TouchPhase phase, Vector2 position)
    {
        InputSystem.QueueStateEvent(_touchscreen, new TouchState
        {
            touchId = 7,
            phase = phase,
            position = position,
        });
    }
}
