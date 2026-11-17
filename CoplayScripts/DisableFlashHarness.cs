using KrumbleHut.Drummi.Dungeons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>EDIT MODE: disables FlashCardTestRunner in the Dungeon scene and saves (manual-play ready).</summary>
public static class DisableFlashHarness
{
    public static string Execute()
    {
        if (Application.isPlaying) return "FAIL: exit play mode first.";
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Dungeon.unity")
            scene = EditorSceneManager.OpenScene("Assets/Scenes/Dungeon.unity", OpenSceneMode.Single);

        var harness = Object.FindFirstObjectByType<FlashCardTestRunner>(FindObjectsInactive.Include);
        if (harness == null) return "FAIL: FlashCardTestRunner not found.";
        harness.enabled = false;
        EditorUtility.SetDirty(harness);
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveOpenScenes();
        return $"FlashCardTestRunner disabled. Saved: {saved}";
    }
}
