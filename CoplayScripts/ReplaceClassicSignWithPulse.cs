using KrumbleHut.Drummi.FlashCards;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Replaces the Classic sign on the Home globe with the Pulse scene sign:
/// creates Assets/ScriptableObjects/FlashCards/Pulse Scene.asset (SceneLaunchDefinition),
/// points MenuSign_Classic at it, renames it MenuSign_Pulse, and hides its Classic
/// how-to-play preview (MenuSignNode.preview = null + PreviewPanel inactive; the
/// PlanetSpinner rim cache already includes inactive canvases). nodes[]/signMounts[]
/// are untouched. Opens Home.unity if needed. Idempotent. Edit mode only.
/// </summary>
public static class ReplaceClassicSignWithPulse
{
    private const string AssetPath = "Assets/ScriptableObjects/FlashCards/Pulse Scene.asset";

    public static string Execute()
    {
        if (EditorApplication.isPlaying) return "ABORT: exit play mode first.";

        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Home")
            scene = EditorSceneManager.OpenScene("Assets/Scenes/Home.unity", OpenSceneMode.Single);

        // ── Definition asset ──
        var def = AssetDatabase.LoadAssetAtPath<SceneLaunchDefinition>(AssetPath);
        if (def == null)
        {
            def = ScriptableObject.CreateInstance<SceneLaunchDefinition>();
            AssetDatabase.CreateAsset(def, AssetPath);
        }
        def.displayName  = "Pulse";
        def.highScoreKey = "PulseHS";
        def.sceneName    = "Pulse";
        EditorUtility.SetDirty(def);
        AssetDatabase.SaveAssets();

        // ── Sign ──
        var node = FindSign("MenuSign_Classic") ?? FindSign("MenuSign_Pulse");
        if (node == null) return "ABORT: neither MenuSign_Classic nor MenuSign_Pulse found in Home.";

        var so = new SerializedObject(node);
        so.FindProperty("definition").objectReferenceValue = def;
        so.FindProperty("preview").objectReferenceValue    = null;
        so.FindProperty("lockedByDefault").boolValue       = false;
        so.ApplyModifiedProperties();
        node.gameObject.name = "MenuSign_Pulse";

        var panel = node.transform.Find("PreviewPanel");
        if (panel != null) panel.gameObject.SetActive(false);

        PrefabUtility.RecordPrefabInstancePropertyModifications(node);
        PrefabUtility.RecordPrefabInstancePropertyModifications(node.gameObject);
        if (panel != null) PrefabUtility.RecordPrefabInstancePropertyModifications(panel.gameObject);
        EditorUtility.SetDirty(node);
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveOpenScenes();

        return $"Sign '{node.gameObject.name}' under '{node.transform.parent.name}' → {def.displayName} (scene {def.sceneName}); " +
               $"preview {(panel != null ? "hidden" : "not found")}; scene saved={saved}";
    }

    private static MenuSignNode FindSign(string name)
    {
        foreach (var n in Object.FindObjectsByType<MenuSignNode>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (n.gameObject.name == name) return n;
        return null;
    }
}
