using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using KrumbleHut.Drummi.Dungeons;

public static class DiagnoseFlickInput
{
    public static void Execute()
    {
        var mouse = Mouse.current;
        Debug.Log($"[FlickDiag] Mouse.current={(mouse == null ? "NULL" : $"present enabled={mouse.enabled}")}");

        var touchscreens = new List<string>();
        foreach (var d in InputSystem.devices)
            touchscreens.Add($"{d.GetType().Name}('{d.name}' enabled={d.enabled})");
        Debug.Log($"[FlickDiag] Devices: {string.Join(", ", touchscreens)}");

        var dpt = Object.FindObjectOfType<DrumPadTouch>(true);
        Debug.Log($"[FlickDiag] DrumPadTouch enabled={dpt != null && dpt.enabled}");

        var reader = Object.FindObjectOfType<FlickInputReader>(true);
        Debug.Log($"[FlickDiag] FlickInputReader EncounterActive={(reader != null ? reader.EncounterActive.ToString() : "NULL")}");

        // UI raycast at screen center — what would eat a flick press?
        if (EventSystem.current != null)
        {
            var ped = new PointerEventData(EventSystem.current)
            {
                position = new Vector2(Screen.width / 2f, Screen.height / 2f)
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(ped, hits);
            if (hits.Count == 0) Debug.Log("[FlickDiag] UI raycast at screen center: NO hits");
            else foreach (var h in hits)
                Debug.Log($"[FlickDiag] UI raycast hit: '{GetPath(h.gameObject.transform)}' (depth={h.depth})");
        }
        else Debug.Log("[FlickDiag] No EventSystem");
    }

    private static string GetPath(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }
}
