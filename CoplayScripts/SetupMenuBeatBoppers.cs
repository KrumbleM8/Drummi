using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

/// <summary>
/// EDIT-MODE, Home.unity open. Builds the "bop to the menu music" setup:
///   1. Root object MenuMusic — AudioSource + MenuBeatClock (Boppy_110, 110 bpm),
///      appended to FlashCardsMenuController.menuObjects so it plays only while
///      the globe menu shows.
///   2. MenuBeatBopper on the MenuSign prefab root (all five signs inherit it).
///   3. MenuBeatBopper on every PlanetPivot decor prop (BushA–H, MountainOnGlobe)
///      with per-prop tuning.
/// Idempotent — re-running only re-applies the tuning values. Saves prefab + scene.
/// </summary>
public static class SetupMenuBeatBoppers
{
    private const string MusicPath  = "Assets/Audio/Songs/Boppy_110.wav";
    private const double MusicBpm   = 110.0;
    private const string PrefabPath = "Assets/Prefabs/MenuSign.prefab";

    // hierarchy path → (stretchY, squashX, everyN, offset)
    private static readonly (string path, float stretch, float squash, int everyN, int offset)[] Decor =
    {
        ("PlanetPivot/BushA", 1.12f, 0.4f, 1, 0),
        ("PlanetPivot/BushB", 1.10f, 0.4f, 1, 0),
        ("PlanetPivot/BushC", 1.12f, 0.4f, 1, 0),
        ("PlanetPivot/BushD", 1.10f, 0.4f, 1, 0),
        ("PlanetPivot/BushE", 1.12f, 0.4f, 1, 0),
        ("PlanetPivot/BushF", 1.10f, 0.4f, 1, 0),
        ("PlanetPivot/BushG", 1.12f, 0.4f, 1, 0),
        ("PlanetPivot/BushH", 1.10f, 0.4f, 1, 0),
        ("PlanetPivot/MountainOnGlobe", 1.03f, 0.2f, 2, 0),
        // Big parallax mountain: pure slight vertical bounce, one per two beats.
        ("ParallaxMid/MountainMid", 1.015f, 0f, 2, 0),
    };

    public static string Execute()
    {
        var sb = new StringBuilder();
        if (EditorApplication.isPlaying) return "In play mode - abort.";
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Home") return "Active scene is " + scene.name + " - open Home first.";

        // ── 1. MenuMusic root + clock ───────────────────────────────────────
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath);
        if (clip == null) sb.AppendLine("WARN: music clip not found at " + MusicPath);

        GameObject musicGo = GameObject.Find("MenuMusic");
        if (musicGo == null)
        {
            musicGo = new GameObject("MenuMusic");
            Undo.RegisterCreatedObjectUndo(musicGo, "MenuMusic");
            sb.AppendLine("Created MenuMusic");
        }
        var src = musicGo.GetComponent<AudioSource>();
        if (src == null) src = musicGo.AddComponent<AudioSource>();
        src.playOnAwake  = false;
        src.loop         = true;
        src.spatialBlend = 0f;
        src.clip         = clip;

        var clock = musicGo.GetComponent<MenuBeatClock>();
        if (clock == null) clock = musicGo.AddComponent<MenuBeatClock>();
        var cso = new SerializedObject(clock);
        cso.FindProperty("music").objectReferenceValue  = clip;
        cso.FindProperty("bpm").doubleValue             = MusicBpm;
        cso.FindProperty("firstBeatOffset").floatValue  = 0f;
        cso.FindProperty("source").objectReferenceValue = src;
        cso.FindProperty("volume").floatValue           = 0.8f;
        cso.ApplyModifiedPropertiesWithoutUndo();
        sb.AppendLine($"MenuBeatClock: clip={(clip ? clip.name : "null")} bpm={MusicBpm}");

        var controller = Object.FindFirstObjectByType<FlashCardsMenuController>(FindObjectsInactive.Include);
        if (controller == null) sb.AppendLine("WARN: FlashCardsMenuController not found - MenuMusic not added to menuObjects");
        else
        {
            var so = new SerializedObject(controller);
            var list = so.FindProperty("menuObjects");
            bool present = false;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == musicGo) present = true;
            if (!present)
            {
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = musicGo;
                so.ApplyModifiedPropertiesWithoutUndo();
                sb.AppendLine("MenuMusic appended to menuObjects (count " + list.arraySize + ")");
            }
            else sb.AppendLine("MenuMusic already in menuObjects");
            EditorUtility.SetDirty(controller);
        }
        EditorUtility.SetDirty(musicGo);

        // ── 2. Sign prefab ──────────────────────────────────────────────────
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (prefabRoot == null) sb.AppendLine("WARN: prefab not loaded: " + PrefabPath);
        else
        {
            var bop = prefabRoot.GetComponent<MenuBeatBopper>();
            if (bop == null) bop = prefabRoot.AddComponent<MenuBeatBopper>();
            Tune(bop, 1.06f, 0.3f, 1, 0);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
            sb.AppendLine("MenuSign prefab: MenuBeatBopper stretch 1.06");
        }

        // ── 3. Globe decor ──────────────────────────────────────────────────
        foreach (var d in Decor)
        {
            var go = GameObject.Find(d.path);
            if (go == null) { sb.AppendLine(d.path + ": NOT FOUND"); continue; }
            var bop = go.GetComponent<MenuBeatBopper>();
            if (bop == null) bop = go.AddComponent<MenuBeatBopper>();
            Tune(bop, d.stretch, d.squash, d.everyN, d.offset);
            EditorUtility.SetDirty(go);
            sb.AppendLine($"{d.path}: stretch {d.stretch} every {d.everyN}");
        }

        // Sign instances are prefab instances: make sure the new prefab
        // component shows up in the scene file too.
        foreach (var node in Object.FindObjectsByType<MenuSignNode>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(node.transform);
            EditorUtility.SetDirty(node.gameObject);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        sb.AppendLine("Scene saved: " + saved);

        string result = sb.ToString();
        Debug.Log("[SetupMenuBeatBoppers]\n" + result);
        return result;
    }

    private static void Tune(MenuBeatBopper bop, float stretch, float squash, int everyN, int offset)
    {
        var so = new SerializedObject(bop);
        so.FindProperty("stretchY").floatValue     = stretch;
        so.FindProperty("squashX").floatValue      = squash;
        so.FindProperty("relaxPower").floatValue   = 2.2f;
        so.FindProperty("anticipation").floatValue = 0.12f;
        so.FindProperty("everyNBeats").intValue    = everyN;
        so.FindProperty("beatOffset").intValue     = offset;
        so.FindProperty("fallbackBpm").floatValue  = (float)MusicBpm;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
