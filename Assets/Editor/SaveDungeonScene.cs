using UnityEditor;
using UnityEditor.SceneManagement;

public static class SaveDungeonScene
{
    public static void Execute()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene, scene.path);
        UnityEngine.Debug.Log($"[SaveDungeonScene] Saved to: {scene.path}");
    }
}
