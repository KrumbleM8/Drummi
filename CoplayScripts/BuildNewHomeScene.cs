using System.Reflection;
using System.Text;
using KrumbleHut.Drummi.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Builds Assets/Scenes/New_Home.unity from scratch — the redesigned home menu skeleton:
/// drum-roll curtain (2-pad bongo input) → animated title + tap-to-start fill bar →
/// alternating slide-in game-mode bars (Bongo / Dungeon / Garden / FlashCards) with
/// two placeholder bubble buttons. Idempotent: overwrites the scene file.
/// </summary>
public static class BuildNewHomeScene
{
    private const string ScenePath = "Assets/Scenes/New_Home.unity";

    public static string Execute()
    {
        var sb = new StringBuilder();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── Camera ───────────────────────────────────────────────────────────
        var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camGo.tag = "MainCamera";
        var cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.09f, 0.07f, 0.13f, 1f);
        cam.orthographic = true;

        // ── AudioManager (persistent prefab — dedupes when arriving from Bootstrap) ──
        var amPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/AudioManager.prefab");
        if (amPrefab == null) return "FAIL: AudioManager prefab missing.";
        PrefabUtility.InstantiatePrefab(amPrefab);
        sb.AppendLine("AudioManager prefab instanced.");

        // ── EventSystem (new Input System) ───────────────────────────────────
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        // ── Main canvas ──────────────────────────────────────────────────────
        var canvasGo = MakeCanvas("MainCanvas", 0);

        var background = CreateStretchRect("Background", canvasGo.transform);
        var bgImg = background.AddComponent<Image>();
        bgImg.color = new Color(0.10f, 0.08f, 0.15f, 1f);
        bgImg.raycastTarget = false;

        // ── Bongo rig (copied from Home.unity — 2-pad, centerPad null) ───────
        // Own canvas above the curtain, like Home's OverlayCanvas (sort 150 > menu 0).
        var bongoCanvasGo = MakeCanvas("BongoOverlayCanvas", 60);
        var bongoSprite = LoadSprite("Assets/Images/Bongo.png", "Bongo_0");

        var bongosGo = new GameObject("Bongos", typeof(RectTransform), typeof(BongoAnimator), typeof(DrumPadTouch));
        bongosGo.transform.SetParent(bongoCanvasGo.transform, false);
        bongosGo.layer = 5;
        var bongosRt = bongosGo.GetComponent<RectTransform>();
        bongosRt.anchorMin = bongosRt.anchorMax = new Vector2(0.5f, 0f);
        bongosRt.pivot = new Vector2(0.5f, 0.5f);
        bongosRt.anchoredPosition = new Vector2(6.15f, 88f);
        bongosRt.sizeDelta = new Vector2(1200f, 586.05f);
        var drumPad = bongosGo.GetComponent<DrumPadTouch>();

        // Animator feel copied from Home.unity's Bongos.
        var bongoAnimator = bongosGo.GetComponent<BongoAnimator>();
        bongoAnimator.scaleAmount      = 1.055f;
        bongoAnimator.scaleDuration    = 0.043f;
        bongoAnimator.rotationAmount   = 10.5f;
        bongoAnimator.rotationDuration = 0.04f;
        bongoAnimator.rangeMultiplier  = 1.14f;

        MakeSpriteImage(bongosGo.transform, "BongoBackShadow", bongoSprite, Color.black,
                        new Vector2(22.77f, 91.02f), new Vector2(719.13f, 404.01f));
        MakeSpriteImage(bongosGo.transform, "BongoImage", bongoSprite, Color.white,
                        new Vector2(22.77f, 91.02f), new Vector2(719.13f, 404.01f));

        var leftPad  = MakePadZone(bongosGo.transform, "L", new Vector2(0f, 0f), new Vector2(0f, 0f),
                                   new Vector2(193.85f, 195.35f), new Vector2(387.70f, 390.70f));
        var rightPad = MakePadZone(bongosGo.transform, "R", new Vector2(1f, 0f), new Vector2(1f, 0f),
                                   new Vector2(-206.15f, 195.35f), new Vector2(412.30f, 390.70f));

        // ── Title page (curtain hides it on Start, reveals on threshold) ─────
        var titlePage = CreateStretchRect("TitlePage", canvasGo.transform);

        var titleText = MakeText(titlePage.transform, "TitleText", "DRUMMI", 120,
                                 new Vector2(0.5f, 0.65f), Vector2.zero, new Vector2(700, 180));
        titleText.color = new Color(0.95f, 0.80f, 0.30f, 1f);
        titleText.fontStyle = FontStyles.Bold;

        var titleDock = new GameObject("TitleDock", typeof(RectTransform));
        titleDock.transform.SetParent(titlePage.transform, false);
        titleDock.layer = 5;
        var dockRt = titleDock.GetComponent<RectTransform>();
        dockRt.anchorMin = dockRt.anchorMax = new Vector2(0.5f, 1f);
        dockRt.pivot = new Vector2(0.5f, 0.5f);
        dockRt.anchoredPosition = new Vector2(0, -90);   // top of screen (frame zooms away first)
        dockRt.sizeDelta = new Vector2(10, 10);

        var tapRoot = CreateStretchRect("TapToStartRoot", titlePage.transform);

        // Tap catcher covers everything above the pads; taps start immediately.
        var tapCatcher = new GameObject("TapCatcher", typeof(RectTransform), typeof(Image), typeof(Button));
        tapCatcher.transform.SetParent(tapRoot.transform, false);
        tapCatcher.layer = 5;
        var tcRt = tapCatcher.GetComponent<RectTransform>();
        tcRt.anchorMin = new Vector2(0f, 0.35f);
        tcRt.anchorMax = Vector2.one;
        tcRt.offsetMin = tcRt.offsetMax = Vector2.zero;
        var tcImg = tapCatcher.GetComponent<Image>();
        tcImg.color = Color.clear;               // invisible but raycastable
        var tapButton = tapCatcher.GetComponent<Button>();
        tapButton.transition = Selectable.Transition.None;

        MakeText(tapRoot.transform, "TapToStartLabel", "TAP TO START", 44,
                 new Vector2(0.5f, 0.45f), Vector2.zero, new Vector2(500, 70));
        var hint = MakeText(tapRoot.transform, "DrumHintLabel", "...or drum to fill!", 22,
                            new Vector2(0.5f, 0.45f), new Vector2(0, -125), new Vector2(500, 40));
        hint.color = new Color(1f, 1f, 1f, 0.6f);

        var fillBarGo = new GameObject("FillBar", typeof(RectTransform), typeof(Image));
        fillBarGo.transform.SetParent(tapRoot.transform, false);
        fillBarGo.layer = 5;
        var fbRt = fillBarGo.GetComponent<RectTransform>();
        fbRt.anchorMin = fbRt.anchorMax = new Vector2(0.5f, 0.45f);
        fbRt.pivot = new Vector2(0.5f, 0.5f);
        fbRt.anchoredPosition = new Vector2(0, -80);
        fbRt.sizeDelta = new Vector2(420, 30);
        var fbBg = fillBarGo.GetComponent<Image>();
        fbBg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        fbBg.type = Image.Type.Sliced;
        fbBg.color = new Color(0f, 0f, 0f, 0.45f);
        fbBg.raycastTarget = false;

        var fillGo = CreateStretchRect("Fill", fillBarGo.transform, -4f);
        var fillImg = fillGo.AddComponent<Image>();
        fillImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImg.fillAmount = 0f;
        fillImg.color = new Color(0.95f, 0.60f, 0.25f, 1f);
        fillImg.raycastTarget = false;

        var fillBar = fillBarGo.AddComponent<TapStartFillBar>();

        // ── Bars menu (inactive; BarsMenuController activates + slides in) ───
        // Root spans the open region between the bongo rig (bottom-anchored,
        // ~390 units tall) and the docked title, so it adapts to any screen height.
        var barsMenuGo = new GameObject("BarsMenu", typeof(RectTransform));
        barsMenuGo.transform.SetParent(canvasGo.transform, false);
        barsMenuGo.layer = 5;
        var barsMenuRt = barsMenuGo.GetComponent<RectTransform>();
        barsMenuRt.anchorMin = Vector2.zero;
        barsMenuRt.anchorMax = Vector2.one;
        barsMenuRt.offsetMin = new Vector2(0f, 400f);    // clear the bongos
        barsMenuRt.offsetMax = new Vector2(0f, -150f);   // clear the docked title
        var barsMenu = barsMenuGo.AddComponent<BarsMenuController>();

        var barDefs = new (string name, string label, Color color)[]
        {
            ("Bar_Bongo",      "BONGO",       new Color(0.93f, 0.72f, 0.40f, 1f)),
            ("Bar_Dungeon",    "DUNGEON",     new Color(0.55f, 0.40f, 0.75f, 1f)),
            ("Bar_Garden",     "GARDEN",      new Color(0.45f, 0.75f, 0.45f, 1f)),
            ("Bar_FlashCards", "FLASH CARDS", new Color(0.40f, 0.60f, 0.85f, 1f)),
        };
        const float barHeightFrac = 0.15f;   // bar thickness as fraction of region height
        const float slotSpacing   = 0.21f;   // distance between bar centers (0.25 = evenly spread; smaller = tighter)
        var barRects = new RectTransform[barDefs.Length];
        var barButtons = new Button[barDefs.Length];
        for (int i = 0; i < barDefs.Length; i++)
        {
            var (name, label, color) = barDefs[i];
            var bar = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            bar.transform.SetParent(barsMenuGo.transform, false);
            bar.layer = 5;
            var rt = bar.GetComponent<RectTransform>();
            // Bars cluster around the region's vertical center with slotSpacing
            // between centers; thickness and spacing are fractions of the region
            // height, so the layout scales with the device's vertical resolution.
            float slotCenter = 0.5f + (1.5f - i) * slotSpacing;
            rt.anchorMin = new Vector2(0f, slotCenter - barHeightFrac * 0.5f);
            rt.anchorMax = new Vector2(1f, slotCenter + barHeightFrac * 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(40f, 0f);    // side margins
            rt.offsetMax = new Vector2(-40f, 0f);
            var img = bar.GetComponent<Image>();
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            img.color = color;
            var lbl = MakeText(bar.transform, "Label", label, 48, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var lblRt = lbl.rectTransform;
            lblRt.anchorMin = Vector2.zero;
            lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = new Vector2(20f, 10f);
            lblRt.offsetMax = new Vector2(-20f, -10f);
            lbl.enableAutoSizing = true;
            lbl.fontSizeMin = 24;
            lbl.fontSizeMax = 72;
            lbl.color = Color.black;
            barRects[i] = rt;
            barButtons[i] = bar.GetComponent<Button>();
        }

        // Bubble placeholders (asymmetric, non-functional for now).
        var bubbleA = MakeBubble(barsMenuGo.transform, "Bubble_A", new Vector2(1f, 1f), new Vector2(-100, 30),
                                 new Vector2(120, 120), Color.white);
        var bubbleB = MakeBubble(barsMenuGo.transform, "Bubble_B", new Vector2(0f, 0f), new Vector2(90, 0),
                                 new Vector2(95, 95), Color.white);

        barsMenuGo.SetActive(false);

        // ── Curtain canvas (curtain sprite + stage-frame overlay, as in Home) ─
        var curtainCanvasGo = MakeCanvas("CurtainCanvas", 50);

        var curtainPanel = CreateStretchRect("Curtain", curtainCanvasGo.transform);
        var curtainRt = curtainPanel.GetComponent<RectTransform>();
        curtainRt.anchoredPosition = new Vector2(0f, 9f);
        curtainRt.sizeDelta = new Vector2(1621.69f, -137.25f);   // Home.unity values
        var curtainImg = curtainPanel.AddComponent<Image>();
        curtainImg.sprite = LoadSprite("Assets/Images/PLACEHOLDER GENERATED/Curtain.png", "Curtain_0");
        curtainImg.color = Color.white;
        var curtainHint = MakeText(curtainPanel.transform, "CurtainHint", "DRUM TO RAISE THE CURTAIN", 36,
                                   new Vector2(0.5f, 0f), new Vector2(0, 480), new Vector2(700, 90));
        curtainHint.color = new Color(1f, 0.92f, 0.75f, 1f);
        var curtain = curtainPanel.AddComponent<CurtainController>();

        // Frame draws after (above) the curtain, below the bongos — decorative only.
        var framePanel = CreateStretchRect("Frame", curtainCanvasGo.transform);
        var frameRt = framePanel.GetComponent<RectTransform>();
        frameRt.anchoredPosition = new Vector2(0f, 27f);
        frameRt.sizeDelta = new Vector2(428.14f, 179.48f);       // Home.unity values
        var frameImg = framePanel.AddComponent<Image>();
        frameImg.sprite = LoadSprite("Assets/Images/PLACEHOLDER GENERATED/StageFrame.png", "StageFrame_0");
        frameImg.color = Color.white;
        frameImg.raycastTarget = false;

        // ── Transition canvas (topmost) ──────────────────────────────────────
        var transCanvasGo = MakeCanvas("TransitionCanvas", 100);
        var transPanel = CreateStretchRect("TransitionPanel", transCanvasGo.transform);
        var transImg = transPanel.AddComponent<Image>();
        transImg.color = Color.black;
        transImg.raycastTarget = false;
        var screenTransition = transPanel.AddComponent<ScreenTransition>();

        // ── Flow controller (+ BongoInputReader for sound & squish, as in Home) ──
        var flowGo = new GameObject("HomeFlowController", typeof(HomeFlowController), typeof(BongoInputReader));
        var flow = flowGo.GetComponent<HomeFlowController>();
        var bongoReader = flowGo.GetComponent<BongoInputReader>();

        // ── Wiring: serialized fields ────────────────────────────────────────
        Set(drumPad, so =>
        {
            so.FindProperty("leftPad").objectReferenceValue  = leftPad;
            so.FindProperty("rightPad").objectReferenceValue = rightPad;
            // centerPad stays null — 2-pad bongo layout.
        });

        Set(curtain, so =>
        {
            so.FindProperty("curtainRect").objectReferenceValue   = curtainRt;
            so.FindProperty("drumPadTouch").objectReferenceValue  = drumPad;
            so.FindProperty("ObjectToSpawn").objectReferenceValue = titlePage;
            // Physics tuned to match Home.unity's curtain feel.
            so.FindProperty("pushForce").floatValue       = 600f;
            so.FindProperty("gravity").floatValue         = 2230f;
            so.FindProperty("maxUpSpeed").floatValue      = 1500f;
            so.FindProperty("revealThreshold").floatValue = 1378f;
        });

        Set(bongoReader, so => so.FindProperty("drumPadTouch").objectReferenceValue = drumPad);

        Set(fillBar, so =>
        {
            so.FindProperty("drumPadTouch").objectReferenceValue = drumPad;
            so.FindProperty("fillImage").objectReferenceValue    = fillImg;
        });

        Set(barsMenu, so =>
        {
            var list = so.FindProperty("bars");
            list.arraySize = barRects.Length;
            for (int i = 0; i < barRects.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = barRects[i];
        });

        Set(flow, so =>
        {
            so.FindProperty("titleRect").objectReferenceValue          = titleText.rectTransform;
            so.FindProperty("titleDockTarget").objectReferenceValue    = dockRt;
            so.FindProperty("tapToStartRoot").objectReferenceValue     = tapRoot;
            so.FindProperty("frameRect").objectReferenceValue          = frameRt;
            so.FindProperty("barsMenu").objectReferenceValue           = barsMenu;
            so.FindProperty("screenTransitioner").objectReferenceValue = screenTransition;
            // themeMusic intentionally left null (placeholder hook).
        });

        foreach (var bubble in new[] { bubbleA, bubbleB })
            Set(bubble, so => so.FindProperty("startUnlocked").boolValue = true);

        // ── Wiring: persistent listeners ─────────────────────────────────────
        AddListener(curtain, "onRevealed", flow.OnCurtainRevealed);
        AddListener(fillBar, "onFilled", flow.StartGame);
        UnityEventTools.AddPersistentListener(tapButton.onClick, flow.StartGame);
        UnityEventTools.AddPersistentListener(barButtons[0].onClick, flow.GoToBongoScene);
        UnityEventTools.AddPersistentListener(barButtons[1].onClick, flow.GoToDungeonScene);
        UnityEventTools.AddPersistentListener(barButtons[2].onClick, flow.GoToGardenScene);
        UnityEventTools.AddPersistentListener(barButtons[3].onClick, flow.GoToFlashCardsScene);
        sb.AppendLine("Listeners wired.");

        // ── Save + Build Settings ────────────────────────────────────────────
        bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
        sb.AppendLine($"Scene saved: {saved} → {ScenePath}");

        var buildScenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!buildScenes.Exists(s => s.path == ScenePath))
        {
            buildScenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = buildScenes.ToArray();
            sb.AppendLine("Added to Build Settings.");
        }

        return sb.ToString();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static GameObject MakeCanvas(string name, int sortingOrder)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.layer = 5;
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800, 600); // project convention
        scaler.matchWidthOrHeight = 0f;
        return go;
    }

    private static Sprite LoadSprite(string path, string spriteName)
    {
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is Sprite s && s.name == spriteName) return s;
        Debug.LogWarning($"[BuildNewHomeScene] Sprite '{spriteName}' not found at {path}");
        return null;
    }

    private static void MakeSpriteImage(Transform parent, string name, Sprite sprite, Color color,
                                        Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.layer = 5;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
    }

    /// <summary>Invisible touch zone rect, matching Home.unity's L / R pad setup.</summary>
    private static RectTransform MakePadZone(Transform parent, string name, Vector2 anchor,
                                             Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.layer = 5;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0f);   // invisible, like Home's pad zones
        return rt;
    }

    private static BubbleButton MakeBubble(Transform parent, string name, Vector2 anchor,
                                           Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image),
                                typeof(UIBubbleFloat), typeof(BubbleButton));
        go.transform.SetParent(parent, false);
        go.layer = 5;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.sprite = LoadSprite("Assets/Images/PLACEHOLDER GENERATED/Bubble.png", "Bubble");
        img.color = color;
        return go.GetComponent<BubbleButton>();
    }

    private static GameObject CreateStretchRect(string name, Transform parent, float expand = 0f)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = 5;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-expand, -expand);
        rt.offsetMax = new Vector2(expand, expand);
        return go;
    }

    private static TMP_Text MakeText(Transform parent, string name, string text, float size,
                                     Vector2 anchor, Vector2 pos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        go.layer = 5;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }

    /// <summary>Adds a persistent listener to a private serialized UnityEvent field.</summary>
    private static void AddListener(Object target, string fieldName, UnityAction call)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        var evt = (UnityEvent)field.GetValue(target);
        if (evt == null)
        {
            evt = new UnityEvent();
            field.SetValue(target, evt);
        }
        UnityEventTools.AddPersistentListener(evt, call);
        EditorUtility.SetDirty(target);
    }

    private static void Set(Object target, System.Action<SerializedObject> edit)
    {
        var so = new SerializedObject(target);
        edit(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
