using UnityEditor;
using UnityEditor.SceneManagement;

public class SaveActiveScene
{
    public static void Execute()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEngine.Debug.Log($"[SaveActiveScene] Saving: {scene.path}");
        EditorSceneManager.SaveScene(scene, scene.path);
        UnityEngine.Debug.Log("[SaveActiveScene] Done");
    }
}
