using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

/// <summary>
/// Globe goes back to THREE signs: Play (Snake) 0deg, Classic 35deg, Bongo 70deg.
/// Re-points the two middle signs (formerly Double Time / Flash Cards) at the
/// Classic / Bongo definitions, unlocks them, deletes SignMount_3/4 (and their
/// sign instances), trims signMounts/nodes to 3, and restores BushH to its
/// pre-change spot (ring ~100deg, where it reads as scenery past the last sign).
/// Open FlashCardsMenu first. Idempotent.
/// </summary>
public static class SwapMiddleSigns
{
    public static void Execute()
    {
        var sb = new StringBuilder();
        if (EditorApplication.isPlaying) { Debug.Log("[SwapSigns] In play mode - abort."); return; }
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "FlashCardsMenu") { Debug.Log("[SwapSigns] Active scene is " + scene.name); return; }

        GameObject pivot  = GameObject.Find("PlanetPivot");
        GameObject flowGo = GameObject.Find("MenuFlow");
        var classicDef = AssetDatabase.LoadAssetAtPath<MenuSignDefinition>("Assets/ScriptableObjects/FlashCards/FlashCards Classic.asset");
        var bongoDef   = AssetDatabase.LoadAssetAtPath<MenuSignDefinition>("Assets/ScriptableObjects/FlashCards/Bongo Scene.asset");
        if (pivot == null || flowGo == null || classicDef == null || bongoDef == null)
        {
            Debug.Log("[SwapSigns] Missing: pivot=" + (pivot != null) + " flow=" + (flowGo != null)
                      + " classic=" + (classicDef != null) + " bongo=" + (bongoDef != null));
            return;
        }

        MenuSignNode n1 = Repoint(pivot.transform, "SignMount_1", "MenuSign_Classic", classicDef, sb);
        MenuSignNode n2 = Repoint(pivot.transform, "SignMount_2", "MenuSign_Bongo",   bongoDef,   sb);
        if (n1 == null || n2 == null) { Debug.Log("[SwapSigns]\n" + sb); return; }

        // Drop the extra mounts (and the sign instances under them).
        foreach (string extra in new[] { "SignMount_3", "SignMount_4" })
        {
            Transform t = pivot.transform.Find(extra);
            if (t != null) { Object.DestroyImmediate(t.gameObject); sb.AppendLine("deleted " + extra); }
        }

        // Trim the lists back to 3 (indices 0..2 already hold mounts 0..2 / their nodes).
        var spinner = pivot.GetComponent<PlanetSpinner>();
        var so = new SerializedObject(spinner);
        var mountsProp = so.FindProperty("signMounts");
        mountsProp.arraySize = 3;
        so.ApplyModifiedPropertiesWithoutUndo();

        var flow = flowGo.GetComponent<FlashCardsMenuController>();
        var fso = new SerializedObject(flow);
        var nodesProp = fso.FindProperty("nodes");
        nodesProp.arraySize = 3;
        nodesProp.GetArrayElementAtIndex(1).objectReferenceValue = n1;
        nodesProp.GetArrayElementAtIndex(2).objectReferenceValue = n2;
        fso.ApplyModifiedPropertiesWithoutUndo();
        sb.AppendLine("signMounts=" + mountsProp.arraySize + " nodes=" + nodesProp.arraySize);

        // BushH back to its committed transform.
        Transform bush = pivot.transform.Find("BushH");
        if (bush != null)
        {
            bush.localPosition = new Vector3(0f, -1.7364819f, 9.848078f);
            bush.localRotation = new Quaternion(0.76604444f, 0f, 0f, 0.64278764f);
            sb.AppendLine("BushH restored -> " + bush.localPosition);
        }

        foreach (var node in new[] { n1, n2 })
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(node);
            EditorUtility.SetDirty(node);
        }
        EditorUtility.SetDirty(spinner);
        EditorUtility.SetDirty(flow);
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("SaveOpenScenes=" + saved);
        Debug.Log("[SwapSigns]\n" + sb);
    }

    private static MenuSignNode Repoint(Transform pivot, string mountName, string newName, MenuSignDefinition def, StringBuilder sb)
    {
        Transform mount = pivot.Find(mountName);
        if (mount == null || mount.childCount == 0) { sb.AppendLine("FAILED: " + mountName + " missing/empty"); return null; }
        var node = mount.GetChild(0).GetComponent<MenuSignNode>();
        if (node == null) { sb.AppendLine("FAILED: no MenuSignNode under " + mountName); return null; }

        string oldName = node.gameObject.name;
        node.gameObject.name = newName;
        var nso = new SerializedObject(node);
        var defProp = nso.FindProperty("definition");
        string oldDef = defProp.objectReferenceValue != null ? defProp.objectReferenceValue.name : "<null>";
        defProp.objectReferenceValue = def;
        nso.FindProperty("lockedByDefault").boolValue = false;
        nso.ApplyModifiedPropertiesWithoutUndo();
        sb.AppendLine(mountName + ": " + oldName + " (" + oldDef + ") -> " + newName + " (" + def.name + "), unlocked");
        return node;
    }
}
