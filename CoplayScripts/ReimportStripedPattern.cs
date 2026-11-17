using UnityEditor;
using UnityEngine;

public static class ReimportStripedPattern
{
    public static string Execute()
    {
        const string path = "Assets/Images/Striped Pattern.png";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        return $"Reimported: {tex.width}x{tex.height}, wrap={tex.wrapMode}";
    }
}
