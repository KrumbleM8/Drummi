using System.Text;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using KrumbleHut.Drummi.UI;

/// <summary>
/// Opens Bongo.unity and rewires it as a sub-scene of the FlashCards globe:
///   - UIMenuManager.startingPageName  Main -> Song (skip the main menu)
///   - SongSelect/MainMenuButton        ShowPageRight("Main") -> OverlayUIController.GoToFlashCardsScene
///   - OverlayUtilCanvas instance        homeSceneName -> FlashCardsMenu (settings-panel Main Menu button)
/// Saves Bongo.unity, then reopens FlashCardsMenu.unity.
/// </summary>
public static class ConfigureBongoReturnToFlashCards
{
    public static void Execute()
    {
        var sb = new StringBuilder();
        if (EditorApplication.isPlaying) { Debug.Log("[BongoReturn] In play mode - abort."); return; }

        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Bongo.unity", OpenSceneMode.Single);

        // 1. Start on the Song page.
        var menuManager = Object.FindFirstObjectByType<UIMenuManager>(FindObjectsInactive.Include);
        if (menuManager == null) { Debug.Log("[BongoReturn] UIMenuManager not found."); return; }
        var mso = new SerializedObject(menuManager);
        var startProp = mso.FindProperty("startingPageName");
        sb.AppendLine("startingPageName " + startProp.stringValue + " -> Song");
        startProp.stringValue = "Song";
        mso.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(menuManager);

        // 2. SongSelect/MainMenuButton -> back to FlashCardsMenu.
        var overlay = Object.FindFirstObjectByType<OverlayUIController>(FindObjectsInactive.Include);
        if (overlay == null) { Debug.Log("[BongoReturn] OverlayUIController not found."); return; }

        Button backButton = null;
        foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (b.name != "MainMenuButton") continue;
            Transform p = b.transform.parent;
            bool underSongSelect = false;
            while (p != null) { if (p.name == "SongSelect") { underSongSelect = true; break; } p = p.parent; }
            if (underSongSelect) { backButton = b; break; }
        }
        if (backButton == null) { Debug.Log("[BongoReturn] SongSelect/MainMenuButton not found."); return; }

        int before = backButton.onClick.GetPersistentEventCount();
        for (int i = before - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(backButton.onClick, i);
        UnityEventTools.AddVoidPersistentListener(backButton.onClick, overlay.GoToFlashCardsScene);
        EditorUtility.SetDirty(backButton);
        sb.AppendLine("SongSelect/MainMenuButton listeners " + before + " -> 1 (OverlayUIController.GoToFlashCardsScene)");

        // 3. Overlay settings-panel Main Menu -> FlashCardsMenu (prefab-instance override).
        var oso = new SerializedObject(overlay);
        var homeProp = oso.FindProperty("homeSceneName");
        if (homeProp == null) { Debug.Log("[BongoReturn] homeSceneName property not found - recompile first."); return; }
        sb.AppendLine("homeSceneName '" + homeProp.stringValue + "' -> FlashCardsMenu");
        homeProp.stringValue = "FlashCardsMenu";
        oso.ApplyModifiedPropertiesWithoutUndo();
        if (PrefabUtility.IsPartOfPrefabInstance(overlay))
            PrefabUtility.RecordPrefabInstancePropertyModifications(overlay);
        EditorUtility.SetDirty(overlay);

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("Bongo.unity SaveOpenScenes=" + saved);

        EditorSceneManager.OpenScene("Assets/Scenes/FlashCardsMenu.unity", OpenSceneMode.Single);
        sb.AppendLine("Reopened FlashCardsMenu.unity");
        Debug.Log("[BongoReturn]\n" + sb);
    }
}
