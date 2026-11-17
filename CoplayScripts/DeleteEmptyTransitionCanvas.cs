using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class DeleteEmptyTransitionCanvas
{
    // Deletes the now-childless TransitionCanvas left behind after its
    // TransitionPanel child was removed. Refuses if it still has children.
    public static string Execute()
    {
        var go = GameObject.Find("TransitionCanvas");
        if (go == null) return "ERROR: TransitionCanvas not found in open scene";
        if (go.transform.childCount > 0) return $"ERROR: TransitionCanvas has {go.transform.childCount} children — not deleting";

        var scene = go.scene;
        Object.DestroyImmediate(go);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveOpenScenes();
        return $"Deleted empty TransitionCanvas; saved scene '{scene.name}'.";
    }
}
