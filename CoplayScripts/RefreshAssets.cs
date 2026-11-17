using UnityEditor;
using UnityEngine;

// EDIT-MODE: force an AssetDatabase refresh so edited Assets/Scripts recompile
// even while the editor window is unfocused (auto-refresh only fires on focus).
public static class RefreshAssets
{
    public static string Execute()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        Debug.Log("[RefreshAssets] refresh requested, compiling=" + EditorApplication.isCompiling);
        return "refresh requested, compiling=" + EditorApplication.isCompiling;
    }
}
