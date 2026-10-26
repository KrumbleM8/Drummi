using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

/// <summary>EDIT-MODE: mark PlanetIdleMotion + HomeIntroDirector dirty (so newly added serialized fields are written out) and save Home.</summary>
public static class SaveHomeScene
{
    public static string Execute()
    {
        if (EditorApplication.isPlaying) return "In play mode - abort.";
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Home") return "Active scene is " + scene.name;
        foreach (var c in Object.FindObjectsByType<PlanetIdleMotion>(FindObjectsInactive.Include, FindObjectsSortMode.None)) EditorUtility.SetDirty(c);
        foreach (var c in Object.FindObjectsByType<HomeIntroDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None)) EditorUtility.SetDirty(c);
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveOpenScenes();
        Debug.Log("[SaveHomeScene] saved=" + saved);
        return "saved=" + saved;
    }
}
