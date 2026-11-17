using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>PLAY-MODE: simulates a pause-menu Home/Restart — sets HomeFlowController.SkipIntro and reloads Home.</summary>
public static class ReloadHomeSkipIntro
{
    public static void Execute()
    {
        if (!Application.isPlaying) { Debug.Log("[ReloadHomeSkipIntro] not in play mode"); return; }
        KrumbleHut.Drummi.UI.HomeFlowController.SkipIntro = true;
        SceneManager.LoadScene("Home");
        Debug.Log("[ReloadHomeSkipIntro] SkipIntro=true, reloading Home");
    }
}
