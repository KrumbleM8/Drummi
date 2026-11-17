using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

/// <summary>
/// Converts the 2D-disc planet menu into the 3D Mario-Galaxy-style globe:
/// perspective static camera above the surface, sphere mesh, sign mounts on
/// the forward great circle (standing radially), decor on the sphere surface,
/// backdrop rings re-tiered, disc sprite deleted.
/// </summary>
public static class ConvertToGlobe
{
    private const float R = 10f;             // globe surface radius
    private const float AnglePerSign = 35f;

    public static void Execute()
    {
        // ── 1. Camera: static perspective hovering above the surface ─────────
        GameObject cam = GameObject.Find("Main Camera");
        Camera camera = cam.GetComponent<Camera>();
        camera.orthographic = false;
        camera.fieldOfView = 60f;
        cam.transform.SetPositionAndRotation(new Vector3(0f, 13.5f, -8f), Quaternion.Euler(20f, 0f, 0f));

        // ── 2. Globe pivot at origin + sphere mesh ───────────────────────────
        GameObject pivot = GameObject.Find("PlanetPivot");
        pivot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        Transform oldDisc = pivot.transform.Find("PlanetDisc");
        if (oldDisc != null) Object.DestroyImmediate(oldDisc.gameObject);

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "GlobeMesh";
        Object.DestroyImmediate(sphere.GetComponent<SphereCollider>());
        sphere.transform.SetParent(pivot.transform, false);
        sphere.transform.localPosition = Vector3.zero;
        sphere.transform.localScale = Vector3.one * (R * 2f);
        sphere.GetComponent<MeshRenderer>().sharedMaterial = GetGlobeMaterial();

        // ── 3. Sign mounts onto the forward great circle ─────────────────────
        PlanetSpinner spinner = pivot.GetComponent<PlanetSpinner>();
        var so = new SerializedObject(spinner);
        so.FindProperty("radius").floatValue = R;
        so.ApplyModifiedPropertiesWithoutUndo();

        for (int i = 0; i < 3; i++)
        {
            Transform mount = pivot.transform.Find("SignMount_" + i);
            if (mount == null) { Debug.Log("FAILED: SignMount_" + i + " missing"); return; }
            float a = i * AnglePerSign;
            float rad = a * Mathf.Deg2Rad;
            mount.localPosition = new Vector3(0f, Mathf.Cos(rad), Mathf.Sin(rad)) * R;
            mount.localRotation = Quaternion.Euler(a, 0f, 0f);
            if (mount.childCount > 0)
            {
                mount.GetChild(0).localPosition = Vector3.zero;
                mount.GetChild(0).localRotation = Quaternion.identity;
            }
        }

        // ── 4. Decor standing on the sphere surface ──────────────────────────
        // (ring angle a: 0 = focus top, + = ahead over horizon; lateral b: + = right)
        PlaceOnSphere(pivot.transform, "TreeA",    -12f, -14f, 11.2f, new Vector3(10f, 14f, 1f));
        PlaceOnSphere(pivot.transform, "TreeB",     50f,  10f, 11.2f, new Vector3(10f, 14f, 1f));
        PlaceOnSphere(pivot.transform, "BushA",      8f,  12f, 10.4f, new Vector3(5f, 5f, 1f));
        PlaceOnSphere(pivot.transform, "BushB",    -18f,  18f, 10.4f, new Vector3(5f, 5f, 1f));
        PlaceOnSphere(pivot.transform, "BushC",     75f, -12f, 10.4f, new Vector3(5f, 5f, 1f));
        PlaceOnSphere(pivot.transform, "HillPropA", 20f, -25f, 10f,   new Vector3(15f, 7f, 1f));
        PlaceOnSphere(pivot.transform, "HillPropB", 55f,  20f, 10f,   new Vector3(15f, 7f, 1f));
        PlaceOnSphere(pivot.transform, "HillPropC", 100f,  0f, 10f,   new Vector3(14f, 7f, 1f));

        // ── 5. Backdrop rings concentric with the globe ──────────────────────
        GameObject far = GameObject.Find("ParallaxFar");
        GameObject mid = GameObject.Find("ParallaxMid");
        far.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        mid.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        SetLocal(far, "Sun",          new Vector3(7f, 18f, 55f),   new Vector3(12f, 12f, 1f));
        SetLocal(far, "MountainFarL", new Vector3(-12f, 4f, 45f),  new Vector3(100f, 35f, 1f));
        SetLocal(far, "MountainFarR", new Vector3(12f, 4.5f, 45f), new Vector3(90f, 32f, 1f));
        SetLocal(mid, "MountainMid",  new Vector3(0f, 2f, 28f),    new Vector3(120f, 45f, 1f));

        // 3D roll arcs are larger than 2D — pull the factors way down.
        SetFactor(far, 0.03f);
        SetFactor(mid, 0.12f);

        // ── 6. World-space sign canvases need the menu camera for raycasts ───
        foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (canvas.renderMode == RenderMode.WorldSpace && canvas.name == "SignCanvas")
                canvas.worldCamera = camera;

        // ── 7. Drop the unused 2D disc sprite asset ──────────────────────────
        AssetDatabase.DeleteAsset("Assets/Images/PLACEHOLDER GENERATED/PlanetDisc.png");

        EditorSceneManager.MarkAllScenesDirty();
        bool saved = EditorSceneManager.SaveOpenScenes();
        Debug.Log("ConvertToGlobe done. Saved=" + saved);
    }

    private static Material GetGlobeMaterial()
    {
        const string path = "Assets/Materials/GlobePlaceholder.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        var mat = new Material(shader);
        mat.color = new Color(0.46f, 0.62f, 0.34f); // ground green
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static void PlaceOnSphere(Transform pivot, string name, float ringDeg, float lateralDeg, float dist, Vector3 scale)
    {
        Transform t = pivot.Find(name);
        if (t == null) { Debug.Log("WARN: decor not found " + name); return; }
        float a = ringDeg * Mathf.Deg2Rad;
        float b = lateralDeg * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(Mathf.Sin(b), Mathf.Cos(b) * Mathf.Cos(a), Mathf.Cos(b) * Mathf.Sin(a));
        t.localPosition = dir * dist;
        t.localRotation = Quaternion.Euler(ringDeg, 0f, 0f) * Quaternion.Euler(0f, 0f, -lateralDeg);
        t.localScale = scale;
    }

    private static void SetLocal(GameObject parent, string child, Vector3 localPos, Vector3 scale)
    {
        Transform t = parent.transform.Find(child);
        if (t == null) { Debug.Log("WARN: backdrop not found " + child); return; }
        t.localPosition = localPos;
        t.localRotation = Quaternion.identity;
        t.localScale = scale;
    }

    private static void SetFactor(GameObject layer, float factor)
    {
        var comp = layer.GetComponent<ParallaxRotationLayer>();
        var so = new SerializedObject(comp);
        so.FindProperty("factor").floatValue = factor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
