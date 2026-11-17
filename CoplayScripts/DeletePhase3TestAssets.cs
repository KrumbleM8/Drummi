using UnityEditor;

/// <summary>
/// EDITOR ONLY — removes the temporary Phase 3 verification assets
/// (Assets/ScriptableObjects/Phase3Test/, whole folder) after verification.
/// </summary>
public static class DeletePhase3TestAssets
{
    public static string Execute()
    {
        const string dir = "Assets/ScriptableObjects/Phase3Test";
        if (!AssetDatabase.IsValidFolder(dir)) return "Nothing to delete — folder absent.";
        bool ok = AssetDatabase.DeleteAsset(dir);
        AssetDatabase.SaveAssets();
        return ok ? "Deleted Assets/ScriptableObjects/Phase3Test." : "FAIL: could not delete folder.";
    }
}
