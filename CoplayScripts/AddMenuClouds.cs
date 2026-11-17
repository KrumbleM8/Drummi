using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AddMenuClouds
{
    public static string Execute()
    {
        var sb = new StringBuilder();
        if (EditorApplication.isPlaying) return "In play mode — abort.";

        var parallaxFar = GameObject.Find("ParallaxFar");
        if (parallaxFar == null) return "ParallaxFar not found — abort.";

        // Guard against double-runs.
        if (parallaxFar.transform.Find("Clouds") != null)
            return "Clouds already exist — aborting.";

        // 1) Delete the blue circle placeholders.
        foreach (string name in new[] { "MountainFarL", "MountainFarR" })
        {
            var t = parallaxFar.transform.Find(name);
            if (t != null) { Object.DestroyImmediate(t.gameObject); sb.AppendLine("Deleted " + name); }
            else sb.AppendLine(name + " not found!");
        }

        // 2) Scrolling cloud quad, same recipe as Bongo's Clouds/Quad.
        var cloudMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/CloudMaterial.mat");
        if (cloudMat == null) return "CloudMaterial.mat not found — abort.";

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Clouds";
        Object.DestroyImmediate(quad.GetComponent<MeshCollider>());
        quad.transform.SetParent(parallaxFar.transform, false);
        quad.transform.localPosition = new Vector3(0f, 16f, 45f);
        quad.transform.localScale = new Vector3(60f, 13f, 1f); // ~Bongo's 4.6:1 band

        var rend = quad.GetComponent<MeshRenderer>();
        rend.sharedMaterial = cloudMat;
        rend.sortingOrder = -100; // far-layer order (matched the deleted blobs)
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;

        var scroll = quad.AddComponent<ScrollingTexture>();
        var so = new SerializedObject(scroll);
        so.FindProperty("scrollSpeedX").floatValue = 0.0067f;
        so.FindProperty("targetMaterial").objectReferenceValue = cloudMat;
        so.ApplyModifiedProperties();
        sb.AppendLine("Created Clouds quad under ParallaxFar.");

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        bool saved = EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("SaveOpenScenes=" + saved);
        return sb.ToString();
    }
}
