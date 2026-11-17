using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

public static class RestartSnakeRun
{
    // Play-mode helper: relaunches the last variant from the results page,
    // exactly like the Again button (RestartFromResults).
    public static void Execute()
    {
        if (!Application.isPlaying)
        {
            Debug.Log("[RestartSnakeRun] Not in play mode.");
            return;
        }
        var mode = Object.FindFirstObjectByType<FlashCardsModeController>(FindObjectsInactive.Include);
        if (mode == null)
        {
            Debug.Log("[RestartSnakeRun] No FlashCardsModeController found.");
            return;
        }
        mode.RestartFromResults();
        Debug.Log("[RestartSnakeRun] RestartFromResults invoked.");
    }
}
