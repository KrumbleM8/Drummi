using UnityEditor.SceneManagement;

public static class SaveScenes
{
    public static string Execute()
    {
        bool ok = EditorSceneManager.SaveOpenScenes();
        return ok ? "Scenes saved" : "Save FAILED";
    }
}
