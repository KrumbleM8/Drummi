using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>PLAY-MODE: simulates returning from Bongo — sets PendingFocusIndex=0 and reloads Home.</summary>
public static class ReloadHomePendingFocus
{
    public static void Execute()
    {
        if (!Application.isPlaying) { Debug.Log("[ReloadHomePendingFocus] not in play mode"); return; }
        KrumbleHut.Drummi.FlashCards.FlashCardsMenuController.PendingFocusIndex = 0;
        SceneManager.LoadScene("Home");
        Debug.Log("[ReloadHomePendingFocus] PendingFocusIndex=0, reloading Home");
    }
}
