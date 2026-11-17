using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using KrumbleHut.Drummi.Dungeons;
using KrumbleHut.Drummi.Flick;

public static class BuildFlickScene
{
    public static string Execute()
    {
        var log = new List<string>();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Flick.unity", OpenSceneMode.Single);

        GameObject Find(string path)
        {
            var go = GameObject.Find(path);
            if (go == null)
            {
                // GameObject.Find misses inactive objects — walk manually.
                var parts = path.Split('/');
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.name != parts[0]) continue;
                    Transform t = root.transform;
                    for (int i = 1; i < parts.Length && t != null; i++)
                        t = t.Find(parts[i]);
                    if (t != null) return t.gameObject;
                }
            }
            return go;
        }

        void SetProp(Object obj, string prop, System.Action<SerializedProperty> setter)
        {
            var so = new SerializedObject(obj);
            var p = so.FindProperty(prop);
            if (p == null) { log.Add($"MISSING PROP {prop} on {obj.GetType().Name}"); return; }
            setter(p);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        void SetRef(Object obj, string prop, Object value) => SetProp(obj, prop, p => p.objectReferenceValue = value);
        void SetFloat(Object obj, string prop, float v)    => SetProp(obj, prop, p => p.floatValue = v);
        void SetInt(Object obj, string prop, int v)        => SetProp(obj, prop, p => p.intValue = v);
        void SetBool(Object obj, string prop, bool v)      => SetProp(obj, prop, p => p.boolValue = v);
        void SetString(Object obj, string prop, string v)  => SetProp(obj, prop, p => p.stringValue = v);

        // ── 1. Remove the flash-card gameplay stack ───────────────────────────
        var fcSystems = Find("FlashCardSystems");
        if (fcSystems != null) { Object.DestroyImmediate(fcSystems); log.Add("Deleted FlashCardSystems"); }

        foreach (var path in new[]
        {
            "FlashCardsCanvas/GameplayUI/Drum",
            "FlashCardsCanvas/GameplayUI/FlashCardPanel",
            "FlashCardsCanvas/GameplayUI/StrikePip0",
            "FlashCardsCanvas/GameplayUI/StrikePip1",
            "FlashCardsCanvas/GameplayUI/StrikePip2",
            "FlashCardsCanvas/GameplayUI/StreakText",
            "FlashCardsCanvas/ScorePage/BestStreakText",
            "FlashCardsCanvas/ScorePage/CardsClearedText",
        })
        {
            var go = Find(path);
            if (go != null) { Object.DestroyImmediate(go); log.Add($"Deleted {path}"); }
            else log.Add($"NOT FOUND (skip delete): {path}");
        }

        // ── 2. Shared refs ────────────────────────────────────────────────────
        var cam        = Find("Main Camera").GetComponent<Camera>();
        var metronome  = Find("Metronome").GetComponent<Metronome>();
        var gameMgr    = Find("GameManager").GetComponent<GameManager>();
        var gameplayUI = Find("FlashCardsCanvas/GameplayUI");
        var scoreText  = Find("FlashCardsCanvas/GameplayUI/ScoreText").GetComponent<TMP_Text>();
        scoreText.raycastTarget = false;

        Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        string matPath = AssetDatabase.GUIDToAssetPath("a97c105638bdf8b4a8650670310a4cd3");
        Material spriteMat = string.IsNullOrEmpty(matPath) ? null : AssetDatabase.LoadAssetAtPath<Material>(matPath);
        log.Add($"Sprite mat: {(spriteMat != null ? matPath : "default (guid unresolved)")}");

        // ── 3. Build the flick rig (values verbatim from the verified FlickTest spike) ──
        var rig = new GameObject("FlickRig");

        GameObject Child(string name, Vector3 pos, Vector3? scale = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(rig.transform, false);
            go.transform.localPosition = pos;
            if (scale.HasValue) go.transform.localScale = scale.Value;
            return go;
        }

        var readerGO   = Child("FlickInputReader", Vector3.zero);
        var reader     = readerGO.AddComponent<FlickInputReader>();
        SetBool(reader, "encounterActive", false);

        var healthGO   = Child("DungeonHealth", Vector3.zero);
        var health     = healthGO.AddComponent<DungeonHealth>();

        var evalGO     = Child("DungeonEvaluator", Vector3.zero);
        var evaluator  = evalGO.AddComponent<DungeonEvaluator>();
        SetRef(evaluator, "metronome", metronome);
        SetFloat(evaluator, "perfectFraction", 0.051f);
        SetFloat(evaluator, "goodFraction", 0.199f);
        SetString(evaluator, "highScoreKey", "FlickHS");
        SetBool(evaluator, "resetScoreOnRepeatedFailure", false);

        var beatMgrGO  = Child("FlickBeatManager", Vector3.zero);
        var beatMgr    = beatMgrGO.AddComponent<FlickBeatManager>();
        var accent     = beatMgrGO.AddComponent<AudioSource>();
        accent.playOnAwake = false;
        accent.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath("2a969eb5f12f98a4894a5f8e7a1fb3c6"));
        accent.pitch = 1.5f;
        log.Add($"Accent clip: {(accent.clip != null ? accent.clip.name : "MISSING")}");

        var leftAnchor  = Child("SweepLeftAnchor",  new Vector3(-3.5f, 2.5f, 0f));
        var rightAnchor = Child("SweepRightAnchor", new Vector3( 3.5f, 2.5f, 0f));
        var homeAnchor  = Child("BallHomeAnchor",   new Vector3(0f, -3.5f, 0f));

        var targetGO = Child("FlickTarget", new Vector3(-3.5f, 2.5f, 0f));
        var sweep    = targetGO.AddComponent<FlickTargetSweep>();
        SetRef(sweep, "leftAnchor", leftAnchor.transform);
        SetRef(sweep, "rightAnchor", rightAnchor.transform);
        SetFloat(sweep, "beatsPerCrossing", 4f);
        SetFloat(sweep, "hitboxRadiusWorld", 1.2f);
        SetRef(sweep, "aimCamera", cam);

        var visualGO = new GameObject("TargetVisual");
        visualGO.transform.SetParent(targetGO.transform, false);
        visualGO.transform.localScale = new Vector3(8f, 8f, 8f);
        var visualSr = visualGO.AddComponent<SpriteRenderer>();
        visualSr.sprite = knob;
        visualSr.color = new Color(1f, 0.55f, 0.15f, 1f);
        visualSr.sortingOrder = 10;
        if (spriteMat != null) visualSr.sharedMaterial = spriteMat;
        var visual = visualGO.AddComponent<FlickTargetVisual>();
        SetRef(visual, "beatManager", beatMgr);
        SetRef(visual, "spriteRenderer", visualSr);

        var ballGO = Child("FlickBall", new Vector3(0f, -3.5f, 0f), new Vector3(6f, 6f, 6f));
        var ballSr = ballGO.AddComponent<SpriteRenderer>();
        ballSr.sprite = knob;
        ballSr.color = new Color(0.4f, 0.8f, 1f, 1f);
        ballSr.sortingOrder = 11;
        if (spriteMat != null) ballSr.sharedMaterial = spriteMat;
        var ball = ballGO.AddComponent<FlickBallController>();
        SetRef(ball, "beatManager", beatMgr);
        SetRef(ball, "targetSweep", sweep);
        SetRef(ball, "targetVisual", visual);
        SetRef(ball, "metronome", metronome);
        SetRef(ball, "homeAnchor", homeAnchor.transform);
        SetRef(ball, "flickReader", reader);
        SetRef(ball, "aimCamera", cam);

        SetRef(beatMgr, "metronome", metronome);
        SetRef(beatMgr, "evaluator", evaluator);
        SetRef(beatMgr, "flickReader", reader);
        SetRef(beatMgr, "health", health);
        SetRef(beatMgr, "accentSource", accent);

        // ── 4. Health bar (minimal slider, no handle, non-raycastable) ────────
        var barGO = new GameObject("HealthBar", typeof(RectTransform));
        var barRt = barGO.GetComponent<RectTransform>();
        barRt.SetParent(gameplayUI.transform, false);
        barRt.anchorMin = new Vector2(0.5f, 1f);
        barRt.anchorMax = new Vector2(0.5f, 1f);
        barRt.pivot     = new Vector2(0.5f, 1f);
        barRt.anchoredPosition = new Vector2(0f, -60f);
        barRt.sizeDelta = new Vector2(640f, 36f);

        var bgGO = new GameObject("Background", typeof(RectTransform));
        var bgRt = bgGO.GetComponent<RectTransform>();
        bgRt.SetParent(barRt, false);
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.5f);
        bgImg.raycastTarget = false;

        var fillAreaGO = new GameObject("Fill Area", typeof(RectTransform));
        var faRt = fillAreaGO.GetComponent<RectTransform>();
        faRt.SetParent(barRt, false);
        faRt.anchorMin = Vector2.zero; faRt.anchorMax = Vector2.one;
        faRt.offsetMin = new Vector2(4f, 4f); faRt.offsetMax = new Vector2(-4f, -4f);

        var fillGO = new GameObject("Fill", typeof(RectTransform));
        var fillRt = fillGO.GetComponent<RectTransform>();
        fillRt.SetParent(faRt, false);
        fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;
        var fillImg = fillGO.AddComponent<Image>();
        fillImg.color = new Color(0.35f, 0.85f, 0.4f, 1f);
        fillImg.raycastTarget = false;

        var slider = barGO.AddComponent<Slider>();
        slider.transition = Selectable.Transition.None;
        slider.interactable = false;
        slider.fillRect = fillRt;
        slider.minValue = 0f;
        slider.maxValue = 100f;
        slider.value = 100f;

        SetRef(health, "healthSlider", slider);

        // ── 5. Mode controller + wiring ───────────────────────────────────────
        var flickMode = rig.AddComponent<FlickModeController>();
        SetRef(flickMode, "metronome", metronome);
        SetRef(flickMode, "beatManager", beatMgr);
        SetRef(flickMode, "flickReader", reader);
        SetRef(flickMode, "targetSweep", sweep);
        SetRef(flickMode, "ball", ball);
        SetRef(flickMode, "targetVisual", visual);
        SetRef(flickMode, "health", health);
        SetRef(flickMode, "scoreText", scoreText);

        // GameManager registration.
        SetProp(gameMgr, "modeControllers", p =>
        {
            p.arraySize = 1;
            p.GetArrayElementAtIndex(0).objectReferenceValue = flickMode;
        });
        SetString(gameMgr, "defaultModeId", "Flick");

        // ── 6. Buttons: drop broken (null-target) calls, wire StartFromMenu ───
        void RewireButton(string path)
        {
            var btn = Find(path).GetComponent<Button>();
            for (int i = btn.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                if (btn.onClick.GetPersistentTarget(i) == null)
                    UnityEventTools.RemovePersistentListener(btn.onClick, i);
            UnityEventTools.AddVoidPersistentListener(btn.onClick, new UnityAction(flickMode.StartFromMenu));
            EditorUtility.SetDirty(btn);
            log.Add($"Rewired {path}: {btn.onClick.GetPersistentEventCount()} call(s)");
        }
        RewireButton("FlashCardsCanvas/MainPage/PlayButton");
        RewireButton("FlashCardsCanvas/ScorePage/AgainButton");

        // ── 7. Retitle + rename canvas ────────────────────────────────────────
        var title = Find("FlashCardsCanvas/MainPage/Title");
        if (title != null) title.GetComponent<TMP_Text>().text = "Flick!";
        var subtitle = Find("FlashCardsCanvas/MainPage/Subtitle");
        if (subtitle != null) subtitle.GetComponent<TMP_Text>().text = "Throw on the beat \u2014 survive!";
        var canvas = Find("FlashCardsCanvas");
        if (canvas != null) canvas.name = "FlickCanvas";

        // ── 8. Build settings ─────────────────────────────────────────────────
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == "Assets/Scenes/Flick.unity"))
        {
            scenes.Add(new EditorBuildSettingsScene("Assets/Scenes/Flick.unity", true));
            EditorBuildSettings.scenes = scenes.ToArray();
            log.Add("Added Flick.unity to build settings");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveOpenScenes();
        log.Add("Scene saved");

        return string.Join("\n", log);
    }
}
