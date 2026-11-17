using UnityEngine;
using KrumbleHut.Drummi.Flick;

public static class StartFlickRun
{
    public static void Execute()
    {
        var mode = Object.FindObjectOfType<FlickModeController>(true);
        if (mode == null) { Debug.LogError("[StartFlickRun] No FlickModeController in scene"); return; }
        mode.StartFromMenu();
        Debug.Log("[StartFlickRun] StartFromMenu invoked");
    }
}
