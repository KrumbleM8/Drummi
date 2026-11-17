using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using KrumbleHut.Drummi.FlashCards;

/// <summary>
/// One-shot rebuild of the FlashCardsMenu scene from the road-dolly layout to
/// the tiny-planet layout: generates a placeholder planet disc sprite, makes
/// the menu camera static/orthographic, builds ParallaxFar/ParallaxMid/
/// PlanetPivot, mounts the signs on the rim, re-tiers the landscape props,
/// wires PlanetSpinner + MenuFlow, and deletes the road/chevrons/anchors.
/// </summary>
public static class RebuildPlanetMenu
{
    private const float PlanetRadius = 4f;       // sign mount radius (disc surface)
    private const float AnglePerSign = 35f;
    private static readonly Vector3 PivotPos = new Vector3(0f, -7f, 0f);

    public static void Execute()
    {
        // ── 1. Placeholder planet disc sprite ────────────────────────────────
        Sprite discSprite = CreateDiscSprite();
        if (discSprite == null) { Debug.Log("FAILED: disc sprite not created"); return; }

        // ── 2. Camera: static, orthographic, straight-on ─────────────────────
        GameObject cam = GameObject.Find("Main Camera");
        cam.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
        Camera camera = cam.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5f;

        // ── 3. New roots ─────────────────────────────────────────────────────
        GameObject far  = new GameObject("ParallaxFar");
        GameObject mid  = new GameObject("ParallaxMid");
        GameObject pivot = new GameObject("PlanetPivot");
        far.transform.position  = PivotPos;
        mid.transform.position  = PivotPos;
        pivot.transform.position = PivotPos;

        // Planet disc (child of pivot so painted features rotate with it).
        GameObject disc = new GameObject("PlanetDisc");
        disc.transform.SetParent(pivot.transform, false);
        SpriteRenderer discSr = disc.AddComponent<SpriteRenderer>();
        discSr.sprite = discSprite;
        discSr.color = new Color(0.46f, 0.62f, 0.34f); // ground green

        // ── 4. Re-tier landscape props ───────────────────────────────────────
        // Far: sun + far mountains (barely move). Local offsets are relative to
        // the pivot position so rotation arcs stay concentric with the planet.
        MoveProp("Landscape/Sun",          far.transform, new Vector3(1.8f, 10.6f, 40f), 0f, new Vector3(6f, 6f, 1f));
        MoveProp("Landscape/MountainFarL", far.transform, new Vector3(-2.5f, 5.4f, 30f), 0f, new Vector3(50f, 17.5f, 1f));
        MoveProp("Landscape/MountainFarR", far.transform, new Vector3(2.6f, 5.6f, 30f),  0f, new Vector3(45f, 16f, 1f));

        // Mid: hills behind the planet horizon.
        MoveProp("Landscape/MountainMid",  mid.transform, new Vector3(0f, 5f, 20f), 0f, new Vector3(60f, 25f, 1f));

        // Near: decor standing radially on the planet rim (rotates 1:1).
        PlaceOnRim("Landscape/TreeA",    pivot.transform, -20f, 5.0f, new Vector3(7.5f, 10f, 1f));
        PlaceOnRim("Landscape/TreeB",    pivot.transform,  55f, 5.0f, new Vector3(7.5f, 10f, 1f));
        PlaceOnRim("Landscape/BushA",    pivot.transform,  15f, 4.5f, new Vector3(5f, 5f, 1f));
        PlaceOnRim("Landscape/BushB",    pivot.transform, -40f, 4.5f, new Vector3(5f, 5f, 1f));
        PlaceOnRim("Landscape/BushC",    pivot.transform,  90f, 4.5f, new Vector3(5f, 5f, 1f));
        PlaceOnRim("Landscape/HillPropA", pivot.transform, -35f, 4.0f, new Vector3(12f, 6f, 1f));
        PlaceOnRim("Landscape/HillPropB", pivot.transform,  50f, 4.0f, new Vector3(12f, 6f, 1f));
        PlaceOnRim("Landscape/HillPropC", pivot.transform, 100f, 4.0f, new Vector3(10f, 5f, 1f));

        // ── 5. Sign mounts + signs ───────────────────────────────────────────
        string[] signNames = { "MenuSign_Normal", "MenuSign_DoubleTime", "MenuSign_Variant3" };
        var mounts = new List<Transform>();
        var nodes  = new List<MenuSignNode>();
        for (int i = 0; i < signNames.Length; i++)
        {
            GameObject sign = GameObject.Find(signNames[i]);
            if (sign == null) { Debug.Log("FAILED: missing sign " + signNames[i]); return; }

            // Drop the obsolete camera anchor.
            Transform anchor = sign.transform.Find("CameraAnchor");
            if (anchor != null) Object.DestroyImmediate(anchor.gameObject);

            GameObject mount = new GameObject("SignMount_" + i);
            mount.transform.SetParent(pivot.transform, false);
            float rad = i * AnglePerSign * Mathf.Deg2Rad;
            mount.transform.localPosition = new Vector3(Mathf.Sin(rad), Mathf.Cos(rad), -0.1f) * PlanetRadius;
            mount.transform.localRotation = Quaternion.identity;

            sign.transform.SetParent(mount.transform, false);
            sign.transform.localPosition = new Vector3(0f, 0f, -0.1f);
            sign.transform.localRotation = Quaternion.identity; // kill old 3D y-angling
            mounts.Add(mount.transform);
            nodes.Add(sign.GetComponent<MenuSignNode>());

            // Drags starting on the sign spin the planet instead of clicking.
            Transform button = sign.transform.Find("SignCanvas/SignButton");
            if (button != null && button.GetComponent<TapOrDragFilter>() == null)
                button.gameObject.AddComponent<TapOrDragFilter>();
        }

        // ── 6. Components + wiring ───────────────────────────────────────────
        PlanetSpinner spinner = pivot.AddComponent<PlanetSpinner>();
        var so = new SerializedObject(spinner);
        so.FindProperty("anglePerSign").floatValue = AnglePerSign;
        so.FindProperty("radius").floatValue = PlanetRadius;
        SerializedProperty mountsProp = so.FindProperty("signMounts");
        mountsProp.arraySize = mounts.Count;
        for (int i = 0; i < mounts.Count; i++)
            mountsProp.GetArrayElementAtIndex(i).objectReferenceValue = mounts[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        SetLayerFactor(far, spinner, 0.05f);
        SetLayerFactor(mid, spinner, 0.4f);

        GameObject menuFlow = GameObject.Find("MenuFlow");
        var flow = new SerializedObject(menuFlow.GetComponent<FlashCardsMenuController>());
        flow.FindProperty("spinner").objectReferenceValue = spinner;
        SerializedProperty nodesProp = flow.FindProperty("nodes");
        nodesProp.arraySize = nodes.Count;
        for (int i = 0; i < nodes.Count; i++)
            nodesProp.GetArrayElementAtIndex(i).objectReferenceValue = nodes[i];
        string[] menuRoots = { "Main Camera", "ParallaxFar", "ParallaxMid", "PlanetPivot", "OverlayCanvas" };
        SerializedProperty menuObjsProp = flow.FindProperty("menuObjects");
        menuObjsProp.arraySize = menuRoots.Length;
        for (int i = 0; i < menuRoots.Length; i++)
            menuObjsProp.GetArrayElementAtIndex(i).objectReferenceValue = GameObject.Find(menuRoots[i]);
        flow.ApplyModifiedPropertiesWithoutUndo();

        // ── 7. Delete obsolete objects ───────────────────────────────────────
        DeleteIfFound("OverlayCanvas/PrevButton");
        DeleteIfFound("OverlayCanvas/NextButton");
        DeleteIfFound("Landscape"); // only Ground + RoadSeg0-3 remain inside

        EditorSceneManager.MarkAllScenesDirty();
        bool saved = EditorSceneManager.SaveOpenScenes();
        Debug.Log("RebuildPlanetMenu done. Saved=" + saved
            + " mounts=" + mounts.Count + " nodes=" + nodes.Count);
    }

    // Generates a white anti-aliased disc PNG (512px, PPU 64 → 8 world units).
    private static Sprite CreateDiscSprite()
    {
        const string path = "Assets/Images/PLACEHOLDER GENERATED/PlanetDisc.png";
        if (!File.Exists(path))
        {
            const int size = 512;
            float r = size * 0.5f - 1f;
            Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                    byte a = (byte)(255f * Mathf.Clamp01(r - d + 0.5f)); // 1px AA edge
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
        }

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = 64f;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void MoveProp(string path, Transform parent, Vector3 localPos, float zRot, Vector3 scale)
    {
        GameObject go = GameObject.Find(path);
        if (go == null) { Debug.Log("WARN: prop not found " + path); return; }
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, 0f, zRot);
        go.transform.localScale = scale;
    }

    // Stands a prop radially on the rim: centre at radialDist along the angle's
    // direction (0° = top, clockwise positive), up pointing away from the core.
    private static void PlaceOnRim(string path, Transform pivot, float angleDeg, float radialDist, Vector3 scale)
    {
        GameObject go = GameObject.Find(path);
        if (go == null) { Debug.Log("WARN: prop not found " + path); return; }
        float rad = angleDeg * Mathf.Deg2Rad;
        go.transform.SetParent(pivot, false);
        go.transform.localPosition = new Vector3(Mathf.Sin(rad) * radialDist, Mathf.Cos(rad) * radialDist, -0.05f);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, -angleDeg);
        go.transform.localScale = scale;
    }

    private static void SetLayerFactor(GameObject layer, PlanetSpinner spinner, float factor)
    {
        var comp = layer.AddComponent<ParallaxRotationLayer>();
        var so = new SerializedObject(comp);
        so.FindProperty("source").objectReferenceValue = spinner;
        so.FindProperty("factor").floatValue = factor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void DeleteIfFound(string path)
    {
        GameObject go = GameObject.Find(path);
        if (go != null) Object.DestroyImmediate(go);
        else Debug.Log("WARN: delete target not found " + path);
    }
}
