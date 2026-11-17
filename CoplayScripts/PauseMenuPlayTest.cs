using System.Text;
using UnityEngine;
using KrumbleHut.Drummi.UI;

public static class PauseMenuPlayTest
{
    /// <summary>Play-mode: open the settings/pause panel via the overlay controller.</summary>
    public static void OpenPause()
    {
        var controller = Object.FindFirstObjectByType<OverlayUIController>(FindObjectsInactive.Include);
        if (controller == null) { Debug.LogError("[PauseMenuPlayTest] No OverlayUIController in scene"); return; }
        controller.OpenSettings();
        Debug.Log($"[PauseMenuPlayTest] OpenSettings called. GameClock paused: {GameClock.Instance?.IsPaused}, timeScale: {Time.timeScale}");
    }

    /// <summary>Play-mode: press the Home button.</summary>
    public static void PressHome()
    {
        var controller = Object.FindFirstObjectByType<OverlayUIController>(FindObjectsInactive.Include);
        if (controller == null) { Debug.LogError("[PauseMenuPlayTest] No OverlayUIController in scene"); return; }
        controller.GoToHomeScene();
        Debug.Log("[PauseMenuPlayTest] GoToHomeScene called.");
    }

    /// <summary>Play-mode: press the Restart button.</summary>
    public static void PressRestart()
    {
        var controller = Object.FindFirstObjectByType<OverlayUIController>(FindObjectsInactive.Include);
        if (controller == null) { Debug.LogError("[PauseMenuPlayTest] No OverlayUIController in scene"); return; }
        controller.RestartScene();
        Debug.Log("[PauseMenuPlayTest] RestartScene called.");
    }

    /// <summary>Play-mode: report state of the current scene (New_Home skip verification etc).</summary>
    public static void Report()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Active scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
        sb.AppendLine($"timeScale: {Time.timeScale}, GameClock paused: {GameClock.Instance?.IsPaused}");

        var curtain = Object.FindFirstObjectByType<CurtainController>(FindObjectsInactive.Include);
        sb.AppendLine($"Curtain: {(curtain == null ? "none" : curtain.gameObject.activeInHierarchy ? "ACTIVE" : "inactive")}");

        var bars = Object.FindFirstObjectByType<BarsMenuController>(FindObjectsInactive.Include);
        sb.AppendLine($"BarsMenu: {(bars == null ? "none" : bars.gameObject.activeInHierarchy ? "ACTIVE" : "inactive")}");

        var flow = Object.FindFirstObjectByType<HomeFlowController>(FindObjectsInactive.Include);
        sb.AppendLine($"HomeFlowController: {(flow == null ? "none" : "present")}");

        Debug.Log("[PauseMenuPlayTest] Report:\n" + sb);
    }
}
