using UnityEditor;
using UnityEditor.SceneManagement;

public class SaveScene
{
    public static void Execute()
    {
        EditorSceneManager.SaveOpenScenes();
        UnityEngine.Debug.Log("[SaveScene] Scene saved.");
    }
}
