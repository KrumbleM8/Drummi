using UnityEngine;
using UnityEditor;

public class BumpHsKey
{
    public static void Execute()
    {
        const string path = "Assets/ScriptableObjects/FlashCards/FlashCards Normal.asset";
        var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
        if (asset == null) { Debug.Log($"[HsKey] Asset not found at {path}"); return; }

        var so = new SerializedObject(asset);
        var p  = so.FindProperty("highScoreKey");
        if (p == null) { Debug.Log("[HsKey] highScoreKey property not found"); return; }
        Debug.Log($"[HsKey] {p.stringValue} -> FlashCardsHS2");
        p.stringValue = "FlashCardsHS2";
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        Debug.Log("[HsKey] Saved.");
    }
}
