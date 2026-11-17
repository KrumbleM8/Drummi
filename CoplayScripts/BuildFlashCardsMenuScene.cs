using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using TMPro;
using KrumbleHut.Drummi.FlashCards;

/// <summary>
/// Builds Assets/Scenes/FlashCardsMenu.unity — the signpost menu:
/// perspective portrait camera, placeholder parallax landscape (SpriteRenderers
/// at staggered z-depths), three MenuSign prefab instances with their variant
/// definitions, overlay canvas (chevrons + back + ScreenTransition), EventSystem
/// with InputSystemUIInputModule, and the wired FlashCardsMenuController.
/// </summary>
public static class BuildFlashCardsMenuScene
{
    public static string Execute()
    {
        var log = new List<string>();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        void SetProp(Object obj, string prop, System.Action<SerializedProperty> setter)
        {
            var so = new SerializedObject(obj);
            var p = so.FindProperty(prop);
            if (p == null) { log.Add($"MISSING PROP {prop} on {obj.GetType().Name}"); return; }
            setter(p);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        void SetRef(Object obj, string prop, Object value) => SetProp(obj, prop, p => p.objectReferenceValue = value);

        Sprite rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        Sprite knob    = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        Material spriteMat = AssetDatabase.LoadAssetAtPath<Material>(
            AssetDatabase.GUIDToAssetPath("a97c105638bdf8b4a8650670310a4cd3")); // Sprite-Lit-Default

        // ── Camera ────────────────────────────────────────────────────────────
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.orthographic = false;
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 150f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.55f, 0.78f, 0.95f, 1f);
        camGO.AddComponent<AudioListener>();
        cam.GetUniversalAdditionalCameraData(); // ensure URP camera data exists
        // Position is overridden by FlashCardsMenuController.Start (snap to anchor 0).
        camGO.transform.SetPositionAndRotation(new Vector3(0f, 0.9f, -4.2f), Quaternion.Euler(-12f, 0f, 0f));

        // ── Global 2D light (Sprite-Lit-Default is black without one) ─────────
        var lightGO = new GameObject("Global Light 2D");
        var light2d = lightGO.AddComponent<Light2D>();
        light2d.lightType = Light2D.LightType.Global;
        light2d.intensity = 1f;
        light2d.color = Color.white;

        // ── Landscape (placeholder parallax layers — art to be replaced) ──────
        var landscape = new GameObject("Landscape");

        void Layer(string name, Sprite sprite, Vector3 pos, float w, float h, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(landscape.transform, false);
            go.transform.localPosition = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            if (spriteMat != null) sr.sharedMaterial = spriteMat;
            Vector2 size = sprite.bounds.size;
            go.transform.localScale = new Vector3(w / size.x, h / size.y, 1f);
        }

        // Far → near. Signs sit around z = 0; camera anchors around z = -4.
        Layer("Sky",       rounded, new Vector3(7f, 14f, 80f),  260f, 140f, new Color(0.62f, 0.82f, 0.97f), -100);
        Layer("Sun",       knob,    new Vector3(-14f, 30f, 70f), 12f, 12f,  new Color(1f, 0.93f, 0.55f),    -95);
        Layer("HillFarL",  knob,    new Vector3(-12f, -2f, 40f), 60f, 22f,  new Color(0.55f, 0.75f, 0.55f), -90);
        Layer("HillFarR",  knob,    new Vector3(24f, -3f, 42f),  70f, 26f,  new Color(0.50f, 0.72f, 0.52f), -90);
        Layer("HillMidL",  knob,    new Vector3(-6f, -3f, 20f),  36f, 14f,  new Color(0.42f, 0.66f, 0.42f), -80);
        Layer("HillMidR",  knob,    new Vector3(16f, -3.5f, 22f), 40f, 16f, new Color(0.38f, 0.63f, 0.40f), -80);
        Layer("BushA",     knob,    new Vector3(-3f, -0.6f, 8f),  4f, 2.6f, new Color(0.30f, 0.55f, 0.32f), -70);
        Layer("BushB",     knob,    new Vector3(5f, -0.7f, 9f),   5f, 3f,   new Color(0.27f, 0.52f, 0.30f), -70);
        Layer("BushC",     knob,    new Vector3(11f, -0.5f, 7f),  3.5f, 2.2f, new Color(0.30f, 0.55f, 0.32f), -70);
        Layer("Ground",    rounded, new Vector3(7f, -5.2f, 10f), 90f, 12f,  new Color(0.46f, 0.62f, 0.34f), -60);
        Layer("PathStripe",rounded, new Vector3(7f, -1.35f, 1f), 40f, 1.2f, new Color(0.78f, 0.68f, 0.48f), -50);
        Layer("FgTuftA",   knob,    new Vector3(-2.2f, -1.2f, -2f), 1.6f, 1f, new Color(0.24f, 0.48f, 0.27f), 20);
        Layer("FgTuftB",   knob,    new Vector3(9.5f, -1.3f, -2.2f), 1.8f, 1.1f, new Color(0.22f, 0.46f, 0.26f), 20);

        // ── Sign instances ────────────────────────────────────────────────────
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MenuSign.prefab");
        if (prefab == null) return "FATAL: Assets/Prefabs/MenuSign.prefab not found";

        var defs = new[]
        {
            "Assets/ScriptableObjects/FlashCards/FlashCards Normal.asset",
            "Assets/ScriptableObjects/FlashCards/FlashCards Double Time.asset",
            "Assets/ScriptableObjects/FlashCards/FlashCards Variant 3.asset",
        }.Select(AssetDatabase.LoadAssetAtPath<FlashCardsModeDefinition>).ToArray();

        var placements = new (string name, Vector3 pos, float yaw)[]
        {
            ("MenuSign_Normal",     new Vector3(0f, 0f, 0f),   -5f),
            ("MenuSign_DoubleTime", new Vector3(7f, 0.25f, 0.6f), 0f),
            ("MenuSign_Variant3",   new Vector3(14f, 0f, 0f),   6f),
        };

        var nodes = new MenuSignNode[3];
        for (int i = 0; i < 3; i++)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            inst.name = placements[i].name;
            inst.transform.SetPositionAndRotation(placements[i].pos, Quaternion.Euler(0f, placements[i].yaw, 0f));

            nodes[i] = inst.GetComponent<MenuSignNode>();
            SetRef(nodes[i], "definition", defs[i]);
            if (defs[i] == null) log.Add($"MISSING definition asset for sign {i}");

            var signCanvas = inst.GetComponentInChildren<Canvas>(true);
            if (signCanvas != null) signCanvas.worldCamera = cam;
            log.Add($"Placed {inst.name} → {(defs[i] != null ? defs[i].displayName : "NULL")}");
        }

        // ── Overlay canvas ────────────────────────────────────────────────────
        var overlayGO = new GameObject("OverlayCanvas", typeof(RectTransform));
        var overlay = overlayGO.AddComponent<Canvas>();
        overlay.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = overlayGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(412f, 815f);
        scaler.matchWidthOrHeight = 0.5f;
        overlayGO.AddComponent<GraphicRaycaster>();

        Button MakeButton(string name, Vector2 anchor, Vector2 anchoredPos, Vector2 size, string text, float fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(overlayGO.transform, false);
            rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.sprite = knob;
            img.color = new Color(0.12f, 0.35f, 0.38f, 0.9f); // house teal
            var b = go.AddComponent<Button>();
            b.targetGraphic = img;

            var lblGO = new GameObject("Label", typeof(RectTransform));
            var lblRt = lblGO.GetComponent<RectTransform>();
            lblRt.SetParent(rt, false);
            lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = Vector2.zero; lblRt.offsetMax = Vector2.zero;
            var lbl = lblGO.AddComponent<TextMeshProUGUI>();
            lbl.text = text;
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.fontSize = fontSize;
            lbl.color = new Color(0.95f, 0.9f, 0.7f, 1f); // house gold
            lbl.raycastTarget = false;
            return b;
        }

        var prevBtn = MakeButton("PrevButton", new Vector2(0f, 0.5f), new Vector2(48f, 0f),  new Vector2(72f, 72f), "<", 40f);
        var nextBtn = MakeButton("NextButton", new Vector2(1f, 0.5f), new Vector2(-48f, 0f), new Vector2(72f, 72f), ">", 40f);
        var backBtn = MakeButton("BackButton", new Vector2(0f, 1f),  new Vector2(48f, -48f), new Vector2(64f, 64f), "\u2302", 34f);

        // ── Screen transition (covers on load, auto-reveals; controller covers on exit) ──
        var transGO = new GameObject("TransitionPanel", typeof(RectTransform));
        var transRt = transGO.GetComponent<RectTransform>();
        transRt.SetParent(overlayGO.transform, false);
        transRt.anchorMin = Vector2.zero; transRt.anchorMax = Vector2.one;
        transRt.offsetMin = Vector2.zero; transRt.offsetMax = Vector2.zero;
        var transImg = transGO.AddComponent<Image>();
        transImg.color = Color.black;
        var transition = transGO.AddComponent<ScreenTransition>();
        transition.transitionDuration = 0.5f;
        transRt.SetAsLastSibling();

        // ── EventSystem ───────────────────────────────────────────────────────
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
        esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

        // ── Menu controller ───────────────────────────────────────────────────
        var flowGO = new GameObject("MenuFlow");
        var controller = flowGO.AddComponent<FlashCardsMenuController>();
        SetProp(controller, "nodes", p =>
        {
            p.arraySize = 3;
            for (int i = 0; i < 3; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = nodes[i];
        });
        SetRef(controller, "menuCamera", cam);
        SetRef(controller, "nextButton", nextBtn);
        SetRef(controller, "prevButton", prevBtn);
        SetRef(controller, "backButton", backBtn);
        SetRef(controller, "screenTransitioner", transition);

        // ── Save + build settings ─────────────────────────────────────────────
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/FlashCardsMenu.unity");
        log.Add("Saved Assets/Scenes/FlashCardsMenu.unity");

        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == "Assets/Scenes/FlashCardsMenu.unity"))
        {
            scenes.Add(new EditorBuildSettingsScene("Assets/Scenes/FlashCardsMenu.unity", true));
            EditorBuildSettings.scenes = scenes.ToArray();
            log.Add("Added FlashCardsMenu.unity to build settings");
        }

        return string.Join("\n", log);
    }
}
