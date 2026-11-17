using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

public static class StartFlashCardsRun
{
    public static void Execute()
    {
        if (!Application.isPlaying) { Debug.LogError("[StartRun] Not in play mode"); return; }
        var controllers = Object.FindObjectsByType<FlashCardsModeController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (controllers.Length == 0) { Debug.LogError("[StartRun] No FlashCardsModeController found"); return; }
        controllers[0].StartFromMenu();
        Debug.Log("[StartRun] Run started");
    }
}
