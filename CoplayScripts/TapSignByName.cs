using UnityEngine;
using UnityEngine.UI;
using KrumbleHut.Drummi.FlashCards;

/// <summary>
/// Play-mode helper: taps the sign whose definition displayName matches.
/// First call spins it into focus; a second call (after the spin settles)
/// launches. Entry points: Execute() = Classic, ExecutePlay() = Play (Snake),
/// ExecuteBongo() = Bongo.
/// </summary>
public static class TapSignByName
{
    public static void Execute()      => Tap("Classic");
    public static void ExecutePlay()  => Tap("Play");
    public static void ExecuteBongo() => Tap("Bongo");
    public static void ExecutePulse() => Tap("Pulse");

    private static void Tap(string targetName)
    {
        if (!Application.isPlaying) { Debug.Log("[TapSignByName] Not in play mode."); return; }

        foreach (var node in Object.FindObjectsByType<MenuSignNode>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (node.Definition == null || node.Definition.displayName != targetName) continue;
            var button = node.GetComponentInChildren<Button>(true);
            if (button == null) { Debug.Log("[TapSignByName] Sign found but no Button child."); return; }
            button.onClick.Invoke();
            Debug.Log("[TapSignByName] Tapped '" + targetName + "' sign: " + node.name + " locked=" + node.IsLocked
                      + " active=" + node.gameObject.activeInHierarchy);
            return;
        }
        Debug.Log("[TapSignByName] No sign with displayName '" + targetName + "'.");
    }
}
