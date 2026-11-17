using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// PHASE 4.1c play-mode navigation driver (verify d — mode switching residue).
///   ExecuteHome / ExecuteDungeon / ExecuteFlashCards — invoke the ACTIVE scene's
///   UIMenuManager.GoTo*Scene (the real button path: cover transition → LoadScene).
///   ExecuteWhere — report active scene + singleton health.
/// </summary>
public static class SceneNavDriver
{
    public static string ExecuteHome()       => Go(m => m.GoToHomeScene(),       "Home");
    public static string ExecuteDungeon()    => Go(m => m.GoToDungeonScene(),    "Dungeon");
    public static string ExecuteFlashCards() => Go(m => m.GoToFlashCardsScene(), "FlashCards");

    public static string ExecuteWhere()
    {
        if (!Application.isPlaying) return "FAIL: not in play mode";
        var scene = SceneManager.GetActiveScene().name;
        string clock = GameClock.Instance  != null ? $"GameClock ok (t={GameClock.Instance.GameTime:F2}, paused={GameClock.Instance.IsPaused})" : "GameClock MISSING";
        string am    = AudioManager.instance != null ? "AudioManager ok" : "AudioManager MISSING";
        string gm    = GameManager.instance  != null ? $"GameManager ok (mode={(GameManager.instance.ActiveMode != null ? GameManager.instance.ActiveMode.ModeId : "none")})" : "GameManager MISSING";
        int panels   = Object.FindObjectsByType<KrumbleHut.Drummi.Dungeons.FlashCardView>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        return $"scene={scene} | {clock} | {am} | {gm} | FlashCardViews={panels} | timeScale={Time.timeScale}";
    }

    private static string Go(System.Action<UIMenuManager> nav, string label)
    {
        if (!Application.isPlaying) return "FAIL: not in play mode";
        var menu = Object.FindFirstObjectByType<UIMenuManager>(FindObjectsInactive.Include);
        if (menu == null) return $"FAIL: no UIMenuManager in active scene ({SceneManager.GetActiveScene().name})";
        nav(menu);
        return $"OK: navigating to {label} from {SceneManager.GetActiveScene().name}";
    }
}
