using UnityEditor;
using UnityEngine;

public static class RefreshBuildScenePaths
{
    public static void Execute()
    {
        var scenes = EditorBuildSettings.scenes;
        for (int i = 0; i < scenes.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(scenes[i].guid.ToString());
            if (!string.IsNullOrEmpty(path) && path != scenes[i].path)
            {
                Debug.Log($"[RefreshBuildScenePaths] {scenes[i].path} -> {path}");
                scenes[i] = new EditorBuildSettingsScene(path, scenes[i].enabled);
            }
        }
        EditorBuildSettings.scenes = scenes;
        AssetDatabase.SaveAssets();
        Debug.Log("[RefreshBuildScenePaths] done");
    }
}
