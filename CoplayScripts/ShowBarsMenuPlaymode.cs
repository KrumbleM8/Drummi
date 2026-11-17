using UnityEngine;
using UnityEditor;
using KrumbleHut.Drummi.UI;

public static class ShowBarsMenuPlaymode
{
    // Play-mode helper: skip waiting for the intro and show the bars menu now.
    public static string Execute()
    {
        if (!Application.isPlaying) return "ERROR: not in play mode";
        var menu = Object.FindFirstObjectByType<BarsMenuController>(FindObjectsInactive.Include);
        if (menu == null) return "ERROR: no BarsMenuController";
        menu.PlayEntrance();
        return "BarsMenu entrance played";
    }
}
