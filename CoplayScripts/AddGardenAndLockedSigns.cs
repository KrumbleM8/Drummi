using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

/// <summary>
/// EDIT-MODE, Home.unity open. Adds SignMount_3 (Garden scene launch, 105 deg) and
/// SignMount_4 (Double Time, locked by default, 140 deg) to the globe, appends them to
/// PlanetSpinner.signMounts + FlashCardsMenuController.nodes, moves BushH off the
/// 105 deg path, swings MountainOnGlobe sideways (lateral -35 deg) so it no longer sits
/// where sign 4 stands, halves the parallax factors so the sky travels the same
/// total distance over 0-140 deg as it used to over 0-70 deg, lowers MountainMid's rest
/// to keep its bottom edge hidden at the far signs, and saves. Idempotent.
/// </summary>
public static class AddGardenAndLockedSigns
{
    private const float R = 10f;
    private const float AnglePerSign = 35f;
    private const string GardenDefPath = "Assets/ScriptableObjects/FlashCards/Garden Scene.asset";

    public static string Execute()
    {
        var sb = new StringBuilder();
        if (EditorApplication.isPlaying) return "In play mode - abort.";
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Home") return "Active scene is " + scene.name + " - open Home first.";

        GameObject pivot  = GameObject.Find("PlanetPivot");
        GameObject camGo  = GameObject.Find("Main Camera");
        GameObject flowGo = GameObject.Find("MenuFlow");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MenuSign.prefab");
        var dtDef  = AssetDatabase.LoadAssetAtPath<MenuSignDefinition>("Assets/ScriptableObjects/FlashCards/FlashCards Double Time.asset");
        if (pivot == null || camGo == null || flowGo == null || prefab == null || dtDef == null)
            return "Missing: pivot=" + (pivot != null) + " cam=" + (camGo != null) + " flow=" + (flowGo != null) + " prefab=" + (prefab != null) + " dt=" + (dtDef != null);

        // -- Garden launch definition asset --------------------------------------
        var gardenDef = AssetDatabase.LoadAssetAtPath<SceneLaunchDefinition>(GardenDefPath);
        if (gardenDef == null)
        {
            gardenDef = ScriptableObject.CreateInstance<SceneLaunchDefinition>();
            gardenDef.displayName  = "Garden";
            gardenDef.highScoreKey = "GardenSign";
            gardenDef.sceneName    = "Garden";
            AssetDatabase.CreateAsset(gardenDef, GardenDefPath);
            AssetDatabase.SaveAssets();
            sb.AppendLine("created " + GardenDefPath);
        }

        Camera cam  = camGo.GetComponent<Camera>();
        var spinner = pivot.GetComponent<PlanetSpinner>();
        var flow    = flowGo.GetComponent<FlashCardsMenuController>();

        MenuSignNode gardenNode = EnsureSign(pivot.transform, prefab, cam, 3, "MenuSign_Garden",     gardenDef, false, sb);
        MenuSignNode lockedNode = EnsureSign(pivot.transform, prefab, cam, 4, "MenuSign_DoubleTime", dtDef,     true,  sb);
        if (gardenNode == null || lockedNode == null) return sb.ToString();

        var so = new SerializedObject(spinner);
        var mountsProp = so.FindProperty("signMounts");
        SetArrayElement(mountsProp, 3, gardenNode.transform.parent);
        SetArrayElement(mountsProp, 4, lockedNode.transform.parent);
        so.ApplyModifiedPropertiesWithoutUndo();

        var fso = new SerializedObject(flow);
        var nodesProp = fso.FindProperty("nodes");
        SetArrayElement(nodesProp, 3, gardenNode);
        SetArrayElement(nodesProp, 4, lockedNode);
        fso.ApplyModifiedPropertiesWithoutUndo();
        sb.AppendLine("signMounts=" + mountsProp.arraySize + " nodes=" + nodesProp.arraySize);

        // -- Decor off the new sign paths ----------------------------------------
        Transform bush = pivot.transform.Find("BushH");          // was ring 100, lateral 0 -> under sign 3
        if (bush != null) { Vector3 k = bush.localScale; PlaceOnSphere(bush, 122f, 14f, R); bush.localScale = k; sb.AppendLine("BushH -> ring 122 lat 14 " + bush.localPosition); }
        Transform mtn = pivot.transform.Find("MountainOnGlobe"); // was ring 140, lateral 0 -> exactly sign 4's spot
        if (mtn != null) { Vector3 k = mtn.localScale; PlaceOnSphere(mtn, 140f, -35f, 12f); mtn.localScale = k; sb.AppendLine("MountainOnGlobe -> ring 140 lat -35 " + mtn.localPosition + " rot " + mtn.localEulerAngles); }

        // -- Parallax: same total travel over 5 signs as before over 3 -----------
        SetFactor("ParallaxMid", 0.06f, sb);
        SetFactor("ParallaxFar", 0.015f, sb);
        var mid = GameObject.Find("ParallaxMid/MountainMid");
        if (mid != null) { var lp = mid.transform.localPosition; lp.y = -1.0f; mid.transform.localPosition = lp; EditorUtility.SetDirty(mid.transform); sb.AppendLine("MountainMid rest y -> -1.0"); }

        // -- Save (prefab-instance double pass) ----------------------------------
        foreach (var node in new[] { gardenNode, lockedNode })
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(node);
            EditorUtility.SetDirty(node);
            Transform btn = node.transform.Find("SignCanvas/SignButton");
            var filter = btn != null ? btn.GetComponent<TapOrDragFilter>() : null;
            if (filter != null) EditorUtility.SetDirty(filter);
            var canvas = node.GetComponentInChildren<Canvas>(true);
            if (canvas != null) { PrefabUtility.RecordPrefabInstancePropertyModifications(canvas); EditorUtility.SetDirty(canvas); }
        }
        EditorUtility.SetDirty(spinner);
        EditorUtility.SetDirty(flow);
        if (bush != null) EditorUtility.SetDirty(bush);
        if (mtn != null) EditorUtility.SetDirty(mtn);
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("SaveOpenScenes=" + saved);
        return sb.ToString();
    }

    private static void SetFactor(string layerName, float value, StringBuilder sb)
    {
        var go = GameObject.Find(layerName);
        var layer = go != null ? go.GetComponent<ParallaxRotationLayer>() : null;
        if (layer == null) { sb.AppendLine("WARN: no ParallaxRotationLayer on " + layerName); return; }
        var lso = new SerializedObject(layer);
        var p = lso.FindProperty("factor");
        sb.AppendLine(layerName + " factor " + p.floatValue + " -> " + value);
        p.floatValue = value;
        lso.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(layer);
    }

    private static MenuSignNode EnsureSign(Transform pivot, GameObject prefab, Camera cam, int index, string signName,
                                           MenuSignDefinition def, bool locked, StringBuilder sb)
    {
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
        nso.FindProperty("lockedByDefault").boolValue = locked;
        nso.ApplyModifiedPropertiesWithoutUndo();
        sb.AppendLine(signName + " @" + a + "deg def=" + def.name + " locked=" + locked + " pos=" + mount.localPosition);
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
