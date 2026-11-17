using System.Linq;
using UnityEditor;

public static class EnsureMenuSceneInBuild
{
    public static string Execute()
    {
        const string path = "Assets/Scenes/FlashCardsMenu.unity";
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == path))
        {
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
        AssetDatabase.SaveAssets();
        return "Build scenes now: " + string.Join(", ", EditorBuildSettings.scenes.Select(s => s.path));
    }
}
