using UnityEditor;

/// <summary>Flushes pending asset/settings changes (e.g. EditorBuildSettings) to disk.</summary>
public static class SaveAssetsNow
{
    public static string Execute()
    {
        AssetDatabase.SaveAssets();
        return "Assets saved.";
    }
}
