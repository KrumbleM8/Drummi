using UnityEngine;
using UnityEngine.UI;
using KrumbleHut.Drummi.FlashCards;

/// <summary>PLAY-MODE: taps the Classic sign's button (distant tap → FocusOn(1); menu must be interactive).</summary>
public static class TapClassicSign
{
    public static void Execute()
    {
        if (!Application.isPlaying) { Debug.Log("[TapClassicSign] not in play mode"); return; }
        foreach (var node in Object.FindObjectsByType<MenuSignNode>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (node.Definition == null || node.Definition.displayName != "Classic") continue;
            var button = node.GetComponentInChildren<Button>(true);
            button.onClick.Invoke();
            Debug.Log("[TapClassicSign] tapped " + node.name);
            return;
        }
        Debug.Log("[TapClassicSign] Classic sign not found");
    }
}
