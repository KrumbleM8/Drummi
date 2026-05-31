using UnityEditor;
using UnityEditor.SceneManagement;

public static class FixCurtainScene
{
    /// <summary>
    /// Step 1: Open the correct scene.
    /// </summary>
    public static void OpenCorrectScene()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Dungeon.unity", OpenSceneMode.Single);
        UnityEngine.Debug.Log("[FixCurtainScene] Opened Assets/Scenes/Dungeon.unity");
    }

    /// <summary>
    /// Step 2: Re-run setup and save to the correct path.
    /// </summary>
    public static void SetupAndSave()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEngine.Debug.Log($"[FixCurtainScene] Active scene path: {scene.path}");

        SetupCurtain.Execute();

        EditorSceneManager.SaveScene(scene, scene.path);
        UnityEngine.Debug.Log($"[FixCurtainScene] Saved to: {scene.path}");
    }
}
