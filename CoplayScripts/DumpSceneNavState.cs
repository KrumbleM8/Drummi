using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using KrumbleHut.Drummi.FlashCards;

/// <summary>
/// Play-mode helper: dumps cross-scene navigation state — active scene, menu
/// page, AudioManager instance count + music playing, globe focus index.
/// </summary>
public static class DumpSceneNavState
{
    public static void Execute()
    {
        if (!Application.isPlaying) { Debug.Log("[NavState] Not in play mode."); return; }
        var sb = new StringBuilder();
        sb.AppendLine("scene=" + SceneManager.GetActiveScene().name);

        var menu = Object.FindFirstObjectByType<UIMenuManager>(FindObjectsInactive.Include);
        sb.AppendLine("menuPage=" + (menu != null ? menu.GetCurrentPageName() : "<no UIMenuManager>"));

        var audios = Object.FindObjectsByType<AudioManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        bool playing = AudioManager.instance != null && AudioManager.instance.speakers != null
                       && AudioManager.instance.speakers.Length > 0 && AudioManager.instance.speakers[0] != null
                       && AudioManager.instance.speakers[0].isPlaying;
        sb.AppendLine("audioManagers=" + audios.Length + " musicPlaying=" + playing
                      + " instanceNull=" + (AudioManager.instance == null));

        var spinner = Object.FindFirstObjectByType<PlanetSpinner>(FindObjectsInactive.Include);
        sb.AppendLine("spinner=" + (spinner != null ? ("focusedIndex=" + spinner.FocusedIndex + " angle=" + spinner.CurrentAngle.ToString("F1")) : "<none>"));

        var flow = Object.FindFirstObjectByType<FlashCardsMenuController>(FindObjectsInactive.Include);
        if (flow != null)
        {
            var fi = typeof(FlashCardsMenuController).GetField("_index", BindingFlags.Instance | BindingFlags.NonPublic);
            sb.AppendLine("menuController._index=" + (fi != null ? fi.GetValue(flow) : "?")
                          + " PendingFocusIndex=" + FlashCardsMenuController.PendingFocusIndex);
        }

        var gm = GameManager.instance;
        sb.AppendLine("activeMode=" + (gm != null && gm.ActiveMode != null ? gm.ActiveMode.ModeId : "<none>"));
        Debug.Log("[NavState]\n" + sb);
    }
}
