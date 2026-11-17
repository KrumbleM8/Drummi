using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Play-mode helper: invokes a results-page button by GameObject name.
///   Execute()     → "AgainButton" (restart last variant via the real button path)
///   ExecuteMenu() → "MenuButton"  (back to the globe menu)
/// </summary>
public static class ClickResultsButton
{
    public static void Execute()     => Click("AgainButton");
    public static void ExecuteMenu() => Click("MenuButton");

    private static void Click(string name)
    {
        if (!Application.isPlaying) { Debug.Log("[ClickResults] Not in play mode."); return; }
        foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (b.name != name) continue;
            b.onClick.Invoke();
            Debug.Log("[ClickResults] Invoked " + name + " (active=" + b.gameObject.activeInHierarchy + ", listeners=" + b.onClick.GetPersistentEventCount() + ")");
            return;
        }
        Debug.Log("[ClickResults] No button named " + name);
    }
}
