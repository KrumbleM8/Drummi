using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using KrumbleHut.Drummi.UI;

/// <summary>Play-mode probe: logs every OverlayUIController's homeSceneName, then
/// invokes GoToHomeScene on the active one and reports which scene loaded.</summary>
public static class ProbeHomeDefault
{
    public static void Execute()
    {
        var all = Object.FindObjectsOfType<OverlayUIController>(true);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("activeScene=" + SceneManager.GetActiveScene().name + " playing=" + Application.isPlaying + " count=" + all.Length);
        foreach (var c in all)
        {
            var f = typeof(OverlayUIController).GetField("homeSceneName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            sb.AppendLine(" - " + c.gameObject.name + " active=" + c.gameObject.activeInHierarchy + " homeSceneName='" + f.GetValue(c) + "'");
        }
        Debug.Log("[ProbeHomeDefault]\n" + sb);
    }

    public static void ExecuteGoHome()
    {
        var c = Object.FindObjectOfType<OverlayUIController>();
        if (c == null) { Debug.Log("[ProbeHomeDefault] no OverlayUIController"); return; }
        Debug.Log("[ProbeHomeDefault] calling GoToHomeScene on " + c.gameObject.name + " from " + SceneManager.GetActiveScene().name);
        c.GoToHomeScene();
    }

    public static void ExecuteWhere()
    {
        Debug.Log("[ProbeHomeDefault] now in scene " + SceneManager.GetActiveScene().name);
    }
}
