using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using KrumbleHut.Drummi.UI;

/// <summary>
/// Play-mode drivers for Bongo.unity (entered from the FlashCards globe):
///   TapAlbum()      — SongSelectController.OnAlbumTapped(first AlbumItem)
///   TapSong()       — OnSongTapped(0)  (call twice: preview, then popup)
///   Finalize()      — FinalizeSong() (launch run)
///   ForceComplete() — reflection-invoke BongoModeController.HandleSongComplete → results
///   ClickBack()     — SongSelect/MainMenuButton.onClick (→ FlashCardsMenu)
///   OverlayHome()   — OverlayUIController.GoToHomeScene() (settings-panel Main Menu)
///   Execute()       — prints SongSelectController state
/// </summary>
public static class BongoSceneDriver
{
    public static void Execute()
    {
        var ctrl = Object.FindFirstObjectByType<SongSelectController>(FindObjectsInactive.Include);
        Debug.Log("[BongoDriver] SongSelectController=" + (ctrl != null ? ctrl.State.ToString() : "<none>"));
    }

    public static void TapAlbum()
    {
        var ctrl = Object.FindFirstObjectByType<SongSelectController>(FindObjectsInactive.Include);
        var album = Object.FindFirstObjectByType<AlbumItem>();
        if (ctrl == null || album == null) { Debug.Log("[BongoDriver] ctrl=" + (ctrl != null) + " album=" + (album != null)); return; }
        ctrl.OnAlbumTapped(album);
        Debug.Log("[BongoDriver] OnAlbumTapped(" + album.name + ") → state=" + ctrl.State);
    }

    public static void TapSong()
    {
        var ctrl = Object.FindFirstObjectByType<SongSelectController>(FindObjectsInactive.Include);
        if (ctrl == null) { Debug.Log("[BongoDriver] no SongSelectController"); return; }
        ctrl.OnSongTapped(0);
        Debug.Log("[BongoDriver] OnSongTapped(0) → state=" + ctrl.State);
    }

    public static void Finalize()
    {
        var ctrl = Object.FindFirstObjectByType<SongSelectController>(FindObjectsInactive.Include);
        if (ctrl == null) { Debug.Log("[BongoDriver] no SongSelectController"); return; }
        ctrl.FinalizeSong();
        Debug.Log("[BongoDriver] FinalizeSong → state=" + ctrl.State + " activeMode="
                  + (GameManager.instance != null && GameManager.instance.ActiveMode != null ? GameManager.instance.ActiveMode.ModeId : "<none>"));
    }

    public static void ForceComplete()
    {
        var bongo = Object.FindFirstObjectByType<BongoModeController>(FindObjectsInactive.Include);
        if (bongo == null) { Debug.Log("[BongoDriver] no BongoModeController"); return; }
        var mi = typeof(BongoModeController).GetMethod("HandleSongComplete", BindingFlags.Instance | BindingFlags.NonPublic);
        if (mi == null) { Debug.Log("[BongoDriver] HandleSongComplete not found"); return; }
        mi.Invoke(bongo, null);
        Debug.Log("[BongoDriver] HandleSongComplete invoked (score=" + bongo.Score + " key=" + bongo.HighScoreKey + ")");
    }

    public static void ClickBack()
    {
        foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (b.name != "MainMenuButton") continue;
            Transform p = b.transform.parent; bool under = false;
            while (p != null) { if (p.name == "SongSelect") { under = true; break; } p = p.parent; }
            if (!under) continue;
            b.onClick.Invoke();
            Debug.Log("[BongoDriver] SongSelect/MainMenuButton invoked (listeners=" + b.onClick.GetPersistentEventCount() + ")");
            return;
        }
        Debug.Log("[BongoDriver] SongSelect/MainMenuButton not found");
    }

    public static void ClickResultsBack()
    {
        foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (b.name != "Back") continue;
            Transform p = b.transform.parent; bool under = false;
            while (p != null) { if (p.name == "ResultsScreen") { under = true; break; } p = p.parent; }
            if (!under) continue;
            b.onClick.Invoke();
            Debug.Log("[BongoDriver] ResultsScreen/Back invoked");
            return;
        }
        Debug.Log("[BongoDriver] ResultsScreen/Back not found");
    }

    public static void DumpResults()
    {
        var screen = Object.FindFirstObjectByType<ScoreScreen>(FindObjectsInactive.Include);
        if (screen == null) { Debug.Log("[BongoDriver] no ScoreScreen"); return; }
        var menu = Object.FindFirstObjectByType<UIMenuManager>(FindObjectsInactive.Include);
        Debug.Log("[BongoDriver] results page=" + (menu != null ? menu.GetCurrentPageName() : "?")
                  + " active=" + screen.gameObject.activeInHierarchy
                  + " final='" + (screen.finalScoreText != null ? screen.finalScoreText.text : "-") + "'"
                  + " high='" + (screen.highScoreText != null ? screen.highScoreText.text : "-") + "'");
    }

    public static void OverlayHome()
    {
        var overlay = Object.FindFirstObjectByType<OverlayUIController>(FindObjectsInactive.Include);
        if (overlay == null) { Debug.Log("[BongoDriver] no OverlayUIController"); return; }
        overlay.GoToHomeScene();
        Debug.Log("[BongoDriver] OverlayUIController.GoToHomeScene() invoked");
    }
}
