using UnityEditor;
using UnityEngine;
using KrumbleHut.Drummi.UI;

/// <summary>
/// Writes homeSceneName = "Home" explicitly into OverlayUtilCanvas.prefab.
/// Needed because Unity's imported copy of the prefab kept the OLD C# default
/// ("New_Home") — changing a field initializer does not touch already-imported
/// assets, so the running instance still said New_Home.
/// </summary>
public static class SetOverlayHomeSceneName
{
    public static void Execute()
    {
        if (EditorApplication.isPlaying) { Debug.Log("[SetOverlayHome] in play mode - abort"); return; }
        const string path = "Assets/Prefabs/OverlayUtilCanvas.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        var ctrl = root.GetComponentInChildren<OverlayUIController>(true);
        if (ctrl == null) { Debug.Log("[SetOverlayHome] no OverlayUIController in prefab"); PrefabUtility.UnloadPrefabContents(root); return; }
        var so = new SerializedObject(ctrl);
        var prop = so.FindProperty("homeSceneName");
        string before = prop.stringValue;
        prop.stringValue = "Home";
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.SaveAssets();
        Debug.Log("[SetOverlayHome] prefab homeSceneName '" + before + "' -> 'Home' saved");
    }
}
