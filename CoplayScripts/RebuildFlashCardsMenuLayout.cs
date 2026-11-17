using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using KrumbleHut.Drummi.FlashCards;

/// <summary>
/// Reworks FlashCardsMenu from lateral scrolling to forward dolly travel:
///   - MenuSign prefab: post/board/canvas sorting orders → 0 (distance sorts them).
///   - Signs along the road at z 0/12/24 with lateral offsets, facing -z.
///   - Landscape rebuilt in tiers: far mountains (z 150-300, order -100/-99),
///     discrete mid props at order 0 (z -2..40, ≥2 units off the road),
///     flat x-rotated ground (order -20) + road segments (order -10).
///   - Sky = camera background colour (no sprite).
///   - Camera transparencySortMode = Perspective (explicit).
///   - All landscape sprites on Sprite-Unlit-Default.
/// </summary>
public static class RebuildFlashCardsMenuLayout
{
    public static string Execute()
    {
        var log = new List<string>();

        Sprite rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        Sprite knob    = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        Material unlit = AssetDatabase.LoadAssetAtPath<Material>(
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
        if (unlit == null) return "FATAL: Sprite-Unlit-Default not found";

        // ── 1. Prefab: flatten sorting orders to 0 ────────────────────────────
        var prefabRoot = PrefabUtility.LoadPrefabContents("Assets/Prefabs/MenuSign.prefab");
        try
        {
            foreach (var sr in prefabRoot.GetComponentsInChildren<SpriteRenderer>(true))
                sr.sortingOrder = 0;
            foreach (var cv in prefabRoot.GetComponentsInChildren<Canvas>(true))
                cv.sortingOrder = 0;
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, "Assets/Prefabs/MenuSign.prefab");
            log.Add("Prefab: sprite + canvas sorting orders → 0");
        }
        finally { PrefabUtility.UnloadPrefabContents(prefabRoot); }

        // ── 2. Scene ──────────────────────────────────────────────────────────
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/FlashCardsMenu.unity", OpenSceneMode.Single);

        var cam = Camera.main;
        cam.transparencySortMode = TransparencySortMode.Perspective;
        log.Add("Camera: transparencySortMode = Perspective");

        // Signs along the road, facing -z (toward the approaching camera).
        var signPlacements = new (string name, Vector3 pos, float yaw)[]
        {
            ("MenuSign_Normal",     new Vector3(-1.5f, 0f, 0f),  -4f),
            ("MenuSign_DoubleTime", new Vector3( 1.5f, 0f, 12f),  4f),
            ("MenuSign_Variant3",   new Vector3(-1.0f, 0f, 24f), -3f),
        };
        foreach (var p in signPlacements)
        {
            var go = GameObject.Find(p.name);
            if (go == null) { log.Add($"MISSING sign {p.name}"); continue; }
            go.transform.SetPositionAndRotation(p.pos, Quaternion.Euler(0f, p.yaw, 0f));
            log.Add($"{p.name} → {p.pos}, yaw {p.yaw}");
        }

        // Landscape: clear and rebuild.
        var landscape = GameObject.Find("Landscape");
        for (int i = landscape.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(landscape.transform.GetChild(i).gameObject);
        log.Add("Cleared old landscape children");

        GameObject Sprite(string name, Sprite sprite, Vector3 pos, Quaternion rot, float w, float h, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(landscape.transform, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            sr.sharedMaterial = unlit;
            Vector2 size = sprite.bounds.size;
            go.transform.localScale = new Vector3(w / size.x, h / size.y, 1f);
            return go;
        }
        Quaternion up = Quaternion.identity;

        // Far tier (z 150-300): horizon-filling mountains + sun. Backstop orders.
        Sprite("MountainFarL", knob, new Vector3(-40f, 18f, 220f), up, 180f, 55f, new Color(0.45f, 0.58f, 0.62f), -100);
        Sprite("MountainFarR", knob, new Vector3(45f, 15f, 240f),  up, 200f, 60f, new Color(0.42f, 0.55f, 0.60f), -100);
        Sprite("MountainMid",  knob, new Vector3(0f, 12f, 160f),   up, 140f, 45f, new Color(0.40f, 0.58f, 0.48f), -99);
        Sprite("Sun",          knob, new Vector3(-35f, 65f, 295f), up, 26f, 26f, new Color(1f, 0.93f, 0.55f), -99);

        // Mid tier (order 0, distance-sorted): discrete props ≥2 units off the road.
        Sprite("HillPropA", knob, new Vector3(-7f, 1.2f, 38f),  up, 12f, 5f,   new Color(0.42f, 0.64f, 0.42f), 0);
        Sprite("HillPropB", knob, new Vector3(8f, 1.0f, 33f),   up, 10f, 4.5f, new Color(0.40f, 0.62f, 0.40f), 0);
        Sprite("HillPropC", knob, new Vector3(-9f, 1.5f, 28f),  up, 14f, 6f,   new Color(0.38f, 0.60f, 0.40f), 0);
        Sprite("TreeA",     knob, new Vector3(-4.5f, 1.5f, 20f), up, 3f, 4f,   new Color(0.28f, 0.52f, 0.30f), 0);
        Sprite("TreeB",     knob, new Vector3(5.5f, 1.4f, 14f),  up, 2.5f, 3.5f, new Color(0.26f, 0.50f, 0.29f), 0);
        Sprite("BushA",     knob, new Vector3(4.5f, 0.6f, 8f),   up, 3f, 1.8f, new Color(0.30f, 0.55f, 0.32f), 0);
        Sprite("BushB",     knob, new Vector3(-4f, 0.5f, 2f),    up, 2.5f, 1.5f, new Color(0.30f, 0.55f, 0.32f), 0);
        Sprite("BushC",     knob, new Vector3(4.2f, 0.5f, -2f),  up, 2.2f, 1.4f, new Color(0.28f, 0.53f, 0.30f), 0);

        // Near tier: flat ground plane (x-rotated), z -10..60.
        Sprite("Ground", rounded, new Vector3(0f, 0f, 25f), Quaternion.Euler(90f, 0f, 0f), 90f, 70f,
               new Color(0.46f, 0.62f, 0.34f), -20);

        // Road: flat segments following the sign lateral offsets.
        var waypoints = new Vector2[] // (x, z)
        {
            new(-1.5f, -10f), new(-1.5f, 4f), new(1.5f, 16f), new(-1f, 28f), new(-1f, 60f),
        };
        for (int i = 0; i < waypoints.Length - 1; i++)
        {
            Vector2 a = waypoints[i], b = waypoints[i + 1];
            Vector2 mid = (a + b) * 0.5f;
            float len = Vector2.Distance(a, b) + 1f; // slight overlap between segments
            float yaw = Mathf.Atan2(b.x - a.x, b.y - a.y) * Mathf.Rad2Deg;
            Sprite($"RoadSeg{i}", rounded,
                   new Vector3(mid.x, 0.02f + i * 0.01f, mid.y),
                   Quaternion.Euler(90f, yaw, 0f), 3f, len,
                   new Color(0.78f, 0.68f, 0.48f), -10);
        }
        log.Add("Landscape rebuilt: 4 far, 8 mid props, ground + 4 road segments");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveOpenScenes();
        log.Add("Scene saved");

        return string.Join("\n", log);
    }
}
