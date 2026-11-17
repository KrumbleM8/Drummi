using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

public static class ResaveLockFlags
{
    public static string Execute()
    {
        var sb = new StringBuilder();
        if (EditorApplication.isPlaying) return "Still in play mode — abort.";

        var nodes = Object.FindObjectsByType<MenuSignNode>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var node in nodes)
        {
            var so = new SerializedObject(node);
            var lockProp = so.FindProperty("lockedByDefault");
            bool isInstance = PrefabUtility.IsPartOfPrefabInstance(node);
            sb.AppendLine(node.name + " lockedByDefault=" + lockProp.boolValue + " prefabInstance=" + isInstance);

            if (lockProp.boolValue && isInstance)
                PrefabUtility.RecordPrefabInstancePropertyModifications(node);
            if (lockProp.boolValue)
                EditorUtility.SetDirty(node);
        }

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("sceneDirtyMarked, SaveOpenScenes=" + saved);
        return sb.ToString();
    }
}
