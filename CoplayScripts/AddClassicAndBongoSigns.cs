using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

/// <summary>
/// Adds SignMount_3 (Classic, 105 deg) and SignMount_4 (Bongo, 140 deg) to the
/// globe in the OPEN FlashCardsMenu scene, instantiates MenuSign prefab
/// instances on them (same per-instance setup as the existing three), appends
/// them to PlanetSpinner.signMounts + FlashCardsMenuController.nodes, moves
/// BushH off the 105 deg path, and saves. Idempotent: re-running updates in place.
/// </summary>
public static class AddClassicAndBongoSigns
{
    private const float R = 10f;
    private const float AnglePerSign = 35f;

    public static void Execute()
    {
        var sb = new StringBuilder();
        if (EditorApplication.isPlaying) { Debug.Log("[AddSigns] In play mode - abort."); return; }

        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "FlashCardsMenu") { Debug.Log("[AddSigns] Active scene is " + scene.name + " - open FlashCardsMenu first."); return; }

        GameObject pivot  = GameObject.Find("PlanetPivot");
        GameObject camGo  = GameObject.Find("Main Camera");
        GameObject flowGo = GameObject.Find("MenuFlow");
        var prefab     = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MenuSign.prefab");
        var classicDef = AssetDatabase.LoadAssetAtPath<MenuSignDefinition>("Assets/ScriptableObjects/FlashCards/FlashCards Classic.asset");
        var bongoDef   = AssetDatabase.LoadAssetAtPath<MenuSignDefinition>("Assets/ScriptableObjects/FlashCards/Bongo Scene.asset");
        if (pivot == null || camGo == null || flowGo == null || prefab == null || classicDef == null || bongoDef == null)
        {
            Debug.Log("[AddSigns] Missing: pivot=" + (pivot != null) + " cam=" + (camGo != null) + " flow=" + (flowGo != null)
                      + " prefab=" + (prefab != null) + " classic=" + (classicDef != null) + " bongo=" + (bongoDef != null));
            return;
        }
        Camera cam = camGo.GetComponent<Camera>();

        var spinner = pivot.GetComponent<PlanetSpinner>();
        var flow    = flowGo.GetComponent<FlashCardsMenuController>();

        MenuSignNode classicNode = EnsureSign(pivot.transform, prefab, cam, 3, "MenuSign_Classic", classicDef, sb);
        MenuSignNode bongoNode   = EnsureSign(pivot.transform, prefab, cam, 4, "MenuSign_Bongo",   bongoDef,   sb);
        if (classicNode == null || bongoNode == null) { Debug.Log(sb.ToString()); return; }

        // ── Append to signMounts / nodes (index 3 = Classic, 4 = Bongo) ──────
        var so = new SerializedObject(spinner);
        var mountsProp = so.FindProperty("signMounts");
        SetArrayElement(mountsProp, 3, classicNode.transform.parent);
        SetArrayElement(mountsProp, 4, bongoNode.transform.parent);
        so.ApplyModifiedPropertiesWithoutUndo();

        var fso = new SerializedObject(flow);
        var nodesProp = fso.FindProperty("nodes");
        SetArrayElement(nodesProp, 3, classicNode);
        SetArrayElement(nodesProp, 4, bongoNode);
        fso.ApplyModifiedPropertiesWithoutUndo();
        sb.AppendLine("signMounts=" + mountsProp.arraySize + " nodes=" + nodesProp.arraySize);

        // ── BushH sat at ring ~100 deg, lateral 0 -> directly under SignMount_3 ──
        Transform bush = pivot.transform.Find("BushH");
        if (bush != null)
        {
            Vector3 keepScale = bush.localScale;
            PlaceOnSphere(bush, 122f, 14f, R);
            bush.localScale = keepScale;
            sb.AppendLine("BushH -> " + bush.localPosition);
        }
        else sb.AppendLine("WARN: BushH not found");

        // ── Save (prefab-instance double pass - see memory gotcha) ───────────
        foreach (var node in new[] { classicNode, bongoNode })
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(node);
            EditorUtility.SetDirty(node);
            Transform btn = node.transform.Find("SignCanvas/SignButton");
            var filter = btn != null ? btn.GetComponent<TapOrDragFilter>() : null;
            if (filter != null) EditorUtility.SetDirty(filter);
        }
        EditorUtility.SetDirty(spinner);
        EditorUtility.SetDirty(flow);
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("SaveOpenScenes=" + saved);
        Debug.Log("[AddSigns]\n" + sb);
    }

    private static MenuSignNode EnsureSign(Transform pivot, GameObject prefab, Camera cam, int index, string signName,
                                           MenuSignDefinition def, StringBuilder sb)
    {
        // Mount on the forward great circle, standing radially (ConvertToGlobe math).
        string mountName = "SignMount_" + index;
        Transform mount = pivot.Find(mountName);
        if (mount == null)
        {
            var go = new GameObject(mountName);
            go.transform.SetParent(pivot, false);
            mount = go.transform;
            sb.AppendLine("created " + mountName);
        }
        float a = index * AnglePerSign;
        float rad = a * Mathf.Deg2Rad;
        mount.localPosition = new Vector3(0f, Mathf.Cos(rad), Mathf.Sin(rad)) * R;
        mount.localRotation = Quaternion.Euler(a, 0f, 0f);
        mount.localScale    = Vector3.one;

        // Sign prefab instance under the mount.
        Transform signT = mount.Find(signName);
        GameObject sign;
        if (signT == null)
        {
            sign = (GameObject)PrefabUtility.InstantiatePrefab(prefab, mount);
            sign.name = signName;
            sb.AppendLine("instantiated " + signName);
        }
        else sign = signT.gameObject;
        sign.transform.localPosition = Vector3.zero;
        sign.transform.localRotation = Quaternion.identity;
        sign.transform.localScale    = Vector3.one;

        // Same per-instance setup as the existing signs.
        Transform anchor = sign.transform.Find("CameraAnchor");
        if (anchor != null) Object.DestroyImmediate(anchor.gameObject);

        Transform canvasT = sign.transform.Find("SignCanvas");
        if (canvasT != null)
        {
            var canvas = canvasT.GetComponent<Canvas>();
            if (canvas != null) canvas.worldCamera = cam;
        }
        Transform button = sign.transform.Find("SignCanvas/SignButton");
        if (button != null && button.GetComponent<TapOrDragFilter>() == null)
            button.gameObject.AddComponent<TapOrDragFilter>();

        var node = sign.GetComponent<MenuSignNode>();
        if (node == null) { sb.AppendLine("FAILED: no MenuSignNode on " + signName); return null; }
        var nso = new SerializedObject(node);
        nso.FindProperty("definition").objectReferenceValue = def;
        nso.FindProperty("lockedByDefault").boolValue = false;
        nso.ApplyModifiedPropertiesWithoutUndo();
        sb.AppendLine(signName + " @" + a + "deg def=" + def.name + " pos=" + mount.localPosition);
        return node;
    }

    private static void SetArrayElement(SerializedProperty arr, int index, Object value)
    {
        if (arr.arraySize <= index) arr.arraySize = index + 1;
        arr.GetArrayElementAtIndex(index).objectReferenceValue = value;
    }

    // ring a: 0 = focus top, + = ahead over horizon; lateral b: + = right.
    private static void PlaceOnSphere(Transform t, float ringDeg, float lateralDeg, float dist)
    {
        float a = ringDeg * Mathf.Deg2Rad;
        float b = lateralDeg * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(Mathf.Sin(b), Mathf.Cos(b) * Mathf.Cos(a), Mathf.Cos(b) * Mathf.Sin(a));
        t.localPosition = dir * dist;
        t.localRotation = Quaternion.Euler(ringDeg, 0f, 0f) * Quaternion.Euler(0f, 0f, -lateralDeg);
    }
}
