using UnityEditor.SceneManagement;
using UnityEngine;

public static class SaveOpenScenes
{
    public static void Execute()
    {
        bool saved = EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[Coplay] SaveOpenScenes -> {saved}");
    }
}
