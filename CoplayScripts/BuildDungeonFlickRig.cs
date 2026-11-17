using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KrumbleHut.Drummi.Dungeons;

public static class BuildDungeonFlickRig
{
    private const string AccentClipGuid = "2a969eb5f12f98a4894a5f8e7a1fb3c6"; // same clip Flick.unity's accent source uses
    private const string EnemyPrefabPath = "Assets/Prefabs/FlickEnemy.prefab";

    public static void Execute()
    {
        // ── Existing dungeon systems ─────────────────────────────────────────
        var metronome  = Object.FindObjectOfType<Metronome>(true);
        var evaluator  = Object.FindObjectOfType<DungeonEvaluator>(true);
        var health     = Object.FindObjectOfType<DungeonHealth>(true);
        var roomCtrl   = Object.FindObjectOfType<RoomController>(true);
        Camera cam     = Camera.main != null ? Camera.main : Object.FindObjectOfType<Camera>(true);

        if (metronome == null || evaluator == null || health == null || roomCtrl == null || cam == null)
        {
            Debug.LogError($"[BuildFlickRig] Missing dungeon systems: metronome={metronome} evaluator={evaluator} " +
                           $"health={health} roomCtrl={roomCtrl} cam={cam}");
            return;
        }

        // Idempotency: bail if the rig already exists.
        if (GameObject.Find("FlickRig") != null)
        {
            Debug.LogError("[BuildFlickRig] FlickRig already exists in the scene — aborting.");
            return;
        }

        // ── Hierarchy ────────────────────────────────────────────────────────
        var rig = new GameObject("FlickRig");
        rig.transform.position = Vector3.zero;

        var bmGO       = NewChild(rig, "FlickBeatManager", Vector3.zero);
        var readerGO   = NewChild(rig, "FlickInputReader", Vector3.zero);
        var leftAnchor = NewChild(rig, "SweepLeftAnchor",  new Vector3(-3.5f, 2.5f, 0f));
        var rightAnchor= NewChild(rig, "SweepRightAnchor", new Vector3( 3.5f, 2.5f, 0f));
        var homeAnchor = NewChild(rig, "BallHomeAnchor",   new Vector3(0f, -3.5f, 0f));

        // ── Components ───────────────────────────────────────────────────────
        var reader = readerGO.AddComponent<FlickInputReader>();
        var bm     = bmGO.AddComponent<FlickBeatManager>();

        // Accent tick source — same clip/pitch as Flick.unity's dedicated source.
        var accent = bmGO.AddComponent<AudioSource>();
        accent.playOnAwake = false;
        accent.pitch = 1.5f;
        string clipPath = AssetDatabase.GUIDToAssetPath(AccentClipGuid);
        var clip = string.IsNullOrEmpty(clipPath) ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
        accent.clip = clip;
        if (clip == null) Debug.LogWarning("[BuildFlickRig] Accent clip GUID not found — runtime fallback will be used.");

        // Enemy prefab instance (leftAnchor/rightAnchor/aimCamera/beatManager are the
        // four scene-external refs the prefab leaves null).
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
        if (prefab == null) { Debug.LogError("[BuildFlickRig] FlickEnemy.prefab not found."); return; }
        var enemyGO = (GameObject)PrefabUtility.InstantiatePrefab(prefab, rig.transform);
        var sweep   = enemyGO.GetComponent<FlickTargetSweep>();
        var enemy   = enemyGO.GetComponent<FlickEnemy>();
        var visual  = enemyGO.GetComponentInChildren<FlickTargetVisual>(true);

        // Ball — mirrors Flick.unity's FlickBall (built-in Knob sprite, order 11).
        var ballGO = NewChild(rig, "FlickBall", new Vector3(0f, -3.5f, 0f));
        ballGO.transform.localScale = Vector3.one * 6f;
        var ballSr = ballGO.AddComponent<SpriteRenderer>();
        ballSr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        ballSr.color = new Color(0.4f, 0.8f, 1f, 1f);
        ballSr.sortingOrder = 11;
        var ball = ballGO.AddComponent<FlickBallController>();

        // Director on the rig root (as in Flick.unity) — dormant between flick rooms.
        var director = rig.AddComponent<FlickEncounterDirector>();

        // ── Wiring (SerializedObject — fields are private) ───────────────────
        Set(reader, ("encounterActive", false));

        SetRefs(bm, ("metronome", metronome), ("evaluator", evaluator), ("flickReader", reader),
                    ("health", health), ("accentSource", accent));

        SetRefs(sweep, ("leftAnchor", leftAnchor.transform), ("rightAnchor", rightAnchor.transform),
                       ("aimCamera", cam));
        SetRefs(visual, ("beatManager", bm));

        SetRefs(ball, ("beatManager", bm), ("targetSweep", sweep), ("targetVisual", visual),
                      ("metronome", metronome), ("homeAnchor", homeAnchor.transform),
                      ("flickReader", reader), ("aimCamera", cam));

        SetRefs(director, ("beatManager", bm), ("flickReader", reader), ("targetSweep", sweep),
                          ("ball", ball), ("targetVisual", visual), ("flickEnemy", enemy));
        Set(director, ("dormantOnStart", true));

        SetRefs(roomCtrl, ("flickBeatManager", bm), ("flickDirector", director));

        // ── Sorting report ───────────────────────────────────────────────────
        var roomSo = new SerializedObject(roomCtrl);
        var bgProp  = roomSo.FindProperty("backgroundRenderer").objectReferenceValue as SpriteRenderer;
        var bgzProp = roomSo.FindProperty("backgroundZoomerRenderer").objectReferenceValue as SpriteRenderer;
        Debug.Log($"[BuildFlickRig] SORTING — background: {Fmt(bgProp)}, zoomer: {Fmt(bgzProp)}, " +
                  $"flick visual order=10, ball/bar order=11-12 (all default layer)");

        EditorSceneManager.MarkSceneDirty(rig.scene);
        bool saved = EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[BuildFlickRig] Dungeon flick rig built and wired — saved={saved}");
    }

    private static string Fmt(SpriteRenderer sr) => sr == null
        ? "null"
        : $"'{sr.gameObject.name}' layer={sr.sortingLayerName} order={sr.sortingOrder}";

    private static GameObject NewChild(GameObject parent, string name, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = localPos;
        return go;
    }

    private static void SetRefs(Component c, params (string field, Object value)[] refs)
    {
        var so = new SerializedObject(c);
        foreach (var (field, value) in refs)
        {
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[BuildFlickRig] Field '{field}' not found on {c.GetType().Name}"); continue; }
            p.objectReferenceValue = value;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Set(Component c, params (string field, bool value)[] bools)
    {
        var so = new SerializedObject(c);
        foreach (var (field, value) in bools)
        {
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[BuildFlickRig] Field '{field}' not found on {c.GetType().Name}"); continue; }
            p.boolValue = value;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
