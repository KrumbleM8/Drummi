using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using KrumbleHut.Drummi.Dungeons;

public static class MakeFlickEnemyPrefab
{
    public static string Execute()
    {
        var sweep = Object.FindObjectOfType<FlickTargetSweep>(true);
        if (sweep == null) return "ERROR: no FlickTargetSweep in scene";
        var go = sweep.gameObject;

        if (PrefabUtility.IsPartOfPrefabInstance(go))
            return "SKIP: FlickTarget is already a prefab instance of " +
                   PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);

        const string path = "Assets/Prefabs/FlickEnemy.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(go, path, InteractionMode.AutomatedAction, out bool success);
        if (!success || prefab == null) return "ERROR: SaveAsPrefabAssetAndConnect failed";

        EditorSceneManager.MarkAllScenesDirty();
        bool saved = EditorSceneManager.SaveOpenScenes();
        return $"OK — prefab created at {path}, scene instance connected, scene saved={saved}";
    }
}
