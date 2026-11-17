using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

/// <summary>
/// Swaps the Pulse and Play signs on the Home globe: Pulse (MenuSign_Pulse)
/// moves to SignMount_2 (70°) and Play (MenuSign_Normal) to SignMount_1 (35°),
/// and their entries in FlashCardsMenuController.nodes[1]/[2] are swapped.
/// Result: Bongo → Play → Pulse → Garden → Double Time. signMounts untouched.
/// Opens Home.unity if needed. Idempotent. Edit mode only.
/// (Clone of ReorderSignsBongoClassicPlay.)
/// </summary>
public static class SwapPulseAndPlaySigns
{
    public static string Execute()
    {
        if (EditorApplication.isPlaying) return "ABORT: exit play mode first.";
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Home")
            scene = EditorSceneManager.OpenScene("Assets/Scenes/Home.unity", OpenSceneMode.Single);

        var sb = new StringBuilder();
        var pivot = GameObject.Find("PlanetPivot").transform;
        var flow  = GameObject.Find("MenuFlow").GetComponent<FlashCardsMenuController>();
        Transform m1 = pivot.Find("SignMount_1"), m2 = pivot.Find("SignMount_2");
        Transform pulse = FindSign(pivot, "MenuSign_Pulse");
        Transform play  = FindSign(pivot, "MenuSign_Normal");
        if (m1 == null || m2 == null || pulse == null || play == null)
            return $"ABORT missing: m1={m1 != null} m2={m2 != null} pulse={pulse != null} play={play != null}";

        Reparent(play, m1); Reparent(pulse, m2);

        var fso = new SerializedObject(flow);
        var nodes = fso.FindProperty("nodes");
        nodes.GetArrayElementAtIndex(1).objectReferenceValue = play.GetComponent<MenuSignNode>();
        nodes.GetArrayElementAtIndex(2).objectReferenceValue = pulse.GetComponent<MenuSignNode>();
        fso.ApplyModifiedPropertiesWithoutUndo();
        for (int i = 0; i < nodes.arraySize; i++)
        {
            var n = (MenuSignNode)nodes.GetArrayElementAtIndex(i).objectReferenceValue;
            sb.AppendLine($"node[{i}] = {n.name} on {n.transform.parent.name}");
        }

        foreach (var t in new[] { play, pulse })
        {
            var node = t.GetComponent<MenuSignNode>();
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            PrefabUtility.RecordPrefabInstancePropertyModifications(node);
            EditorUtility.SetDirty(t); EditorUtility.SetDirty(node);
        }
        EditorUtility.SetDirty(flow);
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("SaveOpenScenes=" + EditorSceneManager.SaveOpenScenes());
        return sb.ToString();
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
