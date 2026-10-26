using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

/// <summary>EDIT-MODE, Home.unity open. Points HomeIntroDirector.menuMusic at MenuMusic's MenuBeatClock, sets the title volume fraction, saves.</summary>
public static class WireMenuMusicSwell
{
    public static string Execute()
    {
        if (EditorApplication.isPlaying) return "In play mode - abort.";
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Home") return "Active scene is " + scene.name + " - open Home first.";

        var director = Object.FindFirstObjectByType<HomeIntroDirector>(FindObjectsInactive.Include);
        var clock    = Object.FindFirstObjectByType<MenuBeatClock>(FindObjectsInactive.Include);
        if (director == null) return "HomeIntroDirector not found";
        if (clock == null) return "MenuBeatClock not found";

        var so = new SerializedObject(director);
        so.FindProperty("menuMusic").objectReferenceValue = clock;
        so.FindProperty("musicTitleVolume").floatValue    = 0.35f;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(director);

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveOpenScenes();
        string result = $"menuMusic -> {clock.gameObject.name}, musicTitleVolume 0.35, saved={saved}";
        Debug.Log("[WireMenuMusicSwell] " + result);
        return result;
    }
}
