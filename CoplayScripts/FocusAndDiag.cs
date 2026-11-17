using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class FocusAndDiag
{
    private static int _ticks;
    private static Touchscreen _ts;

    public static void Execute()
    {
        var gameViewType = System.Type.GetType("UnityEditor.GameView,UnityEditor");
        var gv = EditorWindow.GetWindow(gameViewType);
        gv.Focus();
        Debug.Log($"[FocusDiag] Focused game view. focusedWindow={(EditorWindow.focusedWindow != null ? EditorWindow.focusedWindow.titleContent.text : "null")}");

        _ts = Touchscreen.current;
        if (_ts == null) { Debug.LogError("[FocusDiag] No touchscreen."); return; }

        _ticks = 0;
        EditorApplication.update += Pump;
    }

    private static void Pump()
    {
        _ticks++;
        if (_ticks == 3)
            InputSystem.QueueStateEvent(_ts, new TouchState { touchId = 700, phase = UnityEngine.InputSystem.TouchPhase.Began, position = new Vector2(300f, 200f) });
        if (_ticks == 20)
            InputSystem.QueueStateEvent(_ts, new TouchState { touchId = 700, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = new Vector2(300f, 500f) });
        if (_ticks > 25)
        {
            EditorApplication.update -= Pump;
            Debug.Log($"[FocusDiag] Done. focusedWindow={(EditorWindow.focusedWindow != null ? EditorWindow.focusedWindow.titleContent.text : "null")}");
        }
    }
}
