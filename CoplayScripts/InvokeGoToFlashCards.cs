using UnityEngine;
using KrumbleHut.Drummi.UI;

public static class InvokeGoToFlashCards
{
    public static string Execute()
    {
        if (!Application.isPlaying) return "ERROR: not in play mode";
        var flow = Object.FindAnyObjectByType<HomeFlowController>();
        if (flow == null) return "ERROR: HomeFlowController not found";
        flow.GoToFlashCardsScene();
        return "Invoked GoToFlashCardsScene";
    }
}
