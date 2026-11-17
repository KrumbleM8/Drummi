using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KrumbleHut.Drummi.Flick;

public static class WireScorePage
{
    public static string Execute()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Flick.unity", OpenSceneMode.Single);

        GameObject scorePage = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name != "FlickCanvas") continue;
            var t = root.transform.Find("ScorePage");
            if (t != null) scorePage = t.gameObject;
        }
        if (scorePage == null) return "ScorePage not found";

        var mode = Object.FindFirstObjectByType<FlickModeController>(FindObjectsInactive.Include);
        var so = new SerializedObject(mode);
        so.FindProperty("scorePage").objectReferenceValue = scorePage;
        so.ApplyModifiedPropertiesWithoutUndo();

        bool wasActive = scorePage.activeSelf;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveOpenScenes();
        return $"Wired scorePage (default activeSelf={wasActive}); scene saved";
    }
}
