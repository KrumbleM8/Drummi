using UnityEngine;
using UnityEngine.UI;

/// <summary>Play-mode: clicks Pulse scene buttons by name. Execute=Again, ExecuteHome, ExecutePause.</summary>
public static class ClickPulseButton
{
    public static string Execute()      => Click("AgainButton");
    public static string ExecuteHome()  => Click("HomeButton");
    public static string ExecutePause() => Click("PauseButton");

    private static string Click(string name)
    {
        if (!Application.isPlaying) return "ERROR: not in play mode";
        foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (b.gameObject.name == name) { b.onClick.Invoke(); return $"Clicked {name} (active={b.gameObject.activeInHierarchy})"; }
        return $"ERROR: {name} not found";
    }
}
