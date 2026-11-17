using UnityEngine;
using UnityEngine.SceneManagement;

public static class DumpActiveScene
{
    public static string Execute()
    {
        if (!Application.isPlaying) return "ERROR: not in play mode";
        return $"Active scene: {SceneManager.GetActiveScene().name}";
    }
}
