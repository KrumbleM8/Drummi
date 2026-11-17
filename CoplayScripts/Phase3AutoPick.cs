using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using KrumbleHut.Drummi.Dungeons;

/// <summary>
/// PLAY-MODE ONLY: watches for the effect-pick overlay and auto-clicks the first
/// active pick Button so automated floor runs never stall on the pick phase.
/// Hooks EditorApplication.update; call Detach to remove.
/// </summary>
public static class Phase3AutoPick
{
    private static bool   _attached;
    private static double _lastClick;

    public static string Execute()
    {
        if (_attached) return "Already attached.";
        _attached = true;
        EditorApplication.update += Tick;
        return "Phase3AutoPick attached.";
    }

    public static string Detach()
    {
        if (_attached) EditorApplication.update -= Tick;
        _attached = false;
        return "Detached.";
    }

    private static void Tick()
    {
        if (!Application.isPlaying) return;
        if (EditorApplication.timeSinceStartup - _lastClick < 1.0) return;

        var overlay = Object.FindFirstObjectByType<EffectPickOverlayController>();
        if (overlay == null || !overlay.gameObject.activeInHierarchy) return;

        var buttons = overlay.GetComponentsInChildren<Button>(false);
        foreach (var b in buttons)
        {
            if (b != null && b.gameObject.activeInHierarchy && b.interactable)
            {
                Debug.Log("[Phase3AutoPick] Clicking pick button '" + b.gameObject.name + "'");
                b.onClick.Invoke();
                _lastClick = EditorApplication.timeSinceStartup;
                return;
            }
        }
    }
}
