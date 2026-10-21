using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

/// <summary>
/// Reorders the Home globe signs to Bongo (0 deg) -> Classic (35) -> Play (70)
/// by swapping the Play and Bongo sign instances between SignMount_0 and
/// SignMount_2 and swapping their entries in FlashCardsMenuController.nodes.
/// signMounts order is untouched. Idempotent.
/// </summary>
public static class ReorderSignsBongoClassicPlay
{
    public static void Execute()
    {
        var sb = new StringBuilder();
        if (EditorApplication.isPlaying) { Debug.Log("[Reorder] In play mode - abort."); return; }
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Home") { Debug.Log("[Reorder] Active scene is " + scene.name + " - open Home first."); return; }

        var pivot = GameObject.Find("PlanetPivot").transform;
        var flow  = GameObject.Find("MenuFlow").GetComponent<FlashCardsMenuController>();
        Transform m0 = pivot.Find("SignMount_0"), m2 = pivot.Find("SignMount_2");
        Transform play  = FindSign(pivot, "MenuSign_Normal");
        Transform bongo = FindSign(pivot, "MenuSign_Bongo");
        if (m0 == null || m2 == null || play == null || bongo == null)
        { Debug.Log("[Reorder] missing: m0=" + (m0 != null) + " m2=" + (m2 != null) + " play=" + (play != null) + " bongo=" + (bongo != null)); return; }

        Reparent(bongo, m0); Reparent(play, m2);
        sb.AppendLine("bongo -> " + bongo.parent.name + ", play -> " + play.parent.name);

        var fso = new SerializedObject(flow);
        var nodes = fso.FindProperty("nodes");
        nodes.GetArrayElementAtIndex(0).objectReferenceValue = bongo.GetComponent<MenuSignNode>();
        nodes.GetArrayElementAtIndex(2).objectReferenceValue = play.GetComponent<MenuSignNode>();
        fso.ApplyModifiedPropertiesWithoutUndo();
        for (int i = 0; i < nodes.arraySize; i++)
            sb.AppendLine("node[" + i + "] = " + ((MenuSignNode)nodes.GetArrayElementAtIndex(i).objectReferenceValue).name
                          + " on " + ((MenuSignNode)nodes.GetArrayElementAtIndex(i).objectReferenceValue).transform.parent.name);

        foreach (var t in new[] { play, bongo })
        {
            var node = t.GetComponent<MenuSignNode>();
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            PrefabUtility.RecordPrefabInstancePropertyModifications(node);
            EditorUtility.SetDirty(t); EditorUtility.SetDirty(node);
        }
        EditorUtility.SetDirty(flow);
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("SaveOpenScenes=" + saved);
        Debug.Log("[Reorder]\n" + sb);
    }

    private static Transform FindSign(Transform pivot, string name)
    {
        foreach (Transform mount in pivot) { var s = mount.Find(name); if (s != null) return s; }
        return null;
    }

    private static void Reparent(Transform sign, Transform mount)
    {
        if (sign.parent == mount) return;
        sign.SetParent(mount, false);
        sign.localPosition = Vector3.zero;
        sign.localRotation = Quaternion.identity;
        sign.localScale    = Vector3.one;
    }
}
