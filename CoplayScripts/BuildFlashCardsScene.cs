using System.Text;
using KrumbleHut.Drummi.Dungeons;
using KrumbleHut.Drummi.FlashCards;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// PHASE 4.1b — builds Assets/Scenes/FlashCards.unity from scratch:
/// camera, GameClock, TimingCoordinator, Metronome, AudioManager prefab,
/// EventSystem, canvas (Main + Score menu pages, GameplayUI with drum rig /
/// HUD / FlashCardPanel), transition canvas, GameManager + PauseHandler,
/// FlashCard systems object, full wiring, Build Settings entry.
/// Idempotent: overwrites the scene file. Reopens the Dungeon scene when done.
/// </summary>
public static class BuildFlashCardsScene
{
    private const string ScenePath = "Assets/Scenes/FlashCards.unity";

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

        // ── Shared timing systems ────────────────────────────────────────────
        var clockGo  = new GameObject("GameClock", typeof(GameClock));
        var timingGo = new GameObject("TimingCoordinator", typeof(TimingCoordinator));
        var metroGo  = new GameObject("Metronome", typeof(Metronome));
        var metronome = metroGo.GetComponent<Metronome>();
        metronome.bpm = 105;

        // ── AudioManager (persistent prefab — dedupes when arriving from Bootstrap) ──
        var amPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/AudioManager.prefab");
        if (amPrefab == null) return "FAIL: AudioManager prefab missing.";
        PrefabUtility.InstantiatePrefab(amPrefab);
        sb.AppendLine("AudioManager prefab instanced.");

        // ── EventSystem (new Input System) ───────────────────────────────────
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        // ── Main canvas ──────────────────────────────────────────────────────
        var canvasGo = new GameObject("FlashCardsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.layer = 5;
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800, 600); // project convention
        scaler.matchWidthOrHeight = 0f;

        // ── Menu pages ───────────────────────────────────────────────────────
        var mainPage = CreateStretchRect("MainPage", canvasGo.transform);
        var mainBg = mainPage.AddComponent<Image>();
        mainBg.color = new Color(0.12f, 0.09f, 0.18f, 1f);

        MakeText(mainPage.transform, "Title", "FLASH CARDS", 64, new Vector2(0.5f, 1f), new Vector2(0, -160), new Vector2(700, 100));
        MakeText(mainPage.transform, "Subtitle", "Endless sight-reading — 3 strikes and you're out", 24,
                 new Vector2(0.5f, 1f), new Vector2(0, -250), new Vector2(700, 60));

        var scorePage = CreateStretchRect("ScorePage", canvasGo.transform);
        var scoreBg = scorePage.AddComponent<Image>();
        scoreBg.color = new Color(0.10f, 0.08f, 0.15f, 1f);

        MakeText(scorePage.transform, "ResultsTitle", "RUN OVER", 56, new Vector2(0.5f, 1f), new Vector2(0, -120), new Vector2(700, 90));
        var finalScoreText   = MakeText(scorePage.transform, "FinalScoreText",   "0", 72, new Vector2(0.5f, 1f), new Vector2(0, -230), new Vector2(700, 100));
        var perfectHitsText  = MakeText(scorePage.transform, "PerfectHitsText",  "Perfect Hits: 0", 30, new Vector2(0.5f, 1f), new Vector2(0, -330), new Vector2(700, 60));
        var bestStreakText   = MakeText(scorePage.transform, "BestStreakText",   "Best Streak: 0", 30, new Vector2(0.5f, 1f), new Vector2(0, -390), new Vector2(700, 60));
        var cardsClearedText = MakeText(scorePage.transform, "CardsClearedText", "Cards Cleared: 0", 30, new Vector2(0.5f, 1f), new Vector2(0, -450), new Vector2(700, 60));

        var scoreScreen = scorePage.AddComponent<ScoreScreen>();
        scoreScreen.finalScoreText  = finalScoreText;
        scoreScreen.perfectHitsText = perfectHitsText;
        // highScoreText / roomsClearedText / titleText intentionally unassigned (recon).

        // ── UIMenuManager ────────────────────────────────────────────────────
        var menuGo = new GameObject("MenuUI", typeof(RectTransform), typeof(UIMenuManager));
        menuGo.transform.SetParent(canvasGo.transform, false);
        menuGo.layer = 5;
        var menuManager = menuGo.GetComponent<UIMenuManager>();

        // ── Gameplay UI (inactive until StartGame) ───────────────────────────
        var gameplayUI = CreateStretchRect("GameplayUI", canvasGo.transform);

        // Drum (single wide pad, bottom-center) — the DrumPadTouch center zone.
        var drum = new GameObject("Drum", typeof(RectTransform), typeof(Image));
        drum.transform.SetParent(gameplayUI.transform, false);
        drum.layer = 5;
        var drumRt = drum.GetComponent<RectTransform>();
        drumRt.anchorMin = drumRt.anchorMax = new Vector2(0.5f, 0f);
        drumRt.pivot = new Vector2(0.5f, 0f);
        drumRt.anchoredPosition = new Vector2(0, 40);
        drumRt.sizeDelta = new Vector2(420, 300);
        var drumImg = drum.GetComponent<Image>();
        drumImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        drumImg.color = new Color(0.85f, 0.68f, 0.42f, 1f);
        drumImg.preserveAspect = false;

        // HUD: score (top-right), strike pips (top-left), streak (under pips).
        var scoreHud = MakeText(gameplayUI.transform, "ScoreText", "0", 44, new Vector2(1f, 1f), new Vector2(-100, -50), new Vector2(220, 70));
        scoreHud.alignment = TextAlignmentOptions.Right;

        var pips = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            var pip = new GameObject($"StrikePip{i}", typeof(RectTransform), typeof(Image));
            pip.transform.SetParent(gameplayUI.transform, false);
            pip.layer = 5;
            var prt = pip.GetComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = new Vector2(0f, 1f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.anchoredPosition = new Vector2(60 + i * 44, -60);
            prt.sizeDelta = new Vector2(32, 32);
            pips[i] = pip.GetComponent<Image>();
            pips[i].sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            pips[i].color = new Color(1f, 1f, 1f, 0.25f);
            pips[i].raycastTarget = false;
        }
        var streakHud = MakeText(gameplayUI.transform, "StreakText", "Streak: 0  Best: 0", 24, new Vector2(0f, 1f), new Vector2(160, -110), new Vector2(320, 50));
        streakHud.alignment = TextAlignmentOptions.Left;

        // ── FlashCard systems object ─────────────────────────────────────────
        var systemsGo = new GameObject("FlashCardSystems",
            typeof(DrumPadTouch), typeof(DungeonInputReader), typeof(DungeonEvaluator),
            typeof(FlashCardController), typeof(FlashCardView), typeof(FlashCardsModeController));
        var drumPad    = systemsGo.GetComponent<DrumPadTouch>();
        var reader     = systemsGo.GetComponent<DungeonInputReader>();
        var evaluator  = systemsGo.GetComponent<DungeonEvaluator>();
        var controller = systemsGo.GetComponent<FlashCardController>();
        var view       = systemsGo.GetComponent<FlashCardView>();
        var mode       = systemsGo.GetComponent<FlashCardsModeController>();

        // ── Card panel (same geometry as the Dungeon panel) ──────────────────
        var panel = new GameObject("FlashCardPanel", typeof(RectTransform));
        panel.transform.SetParent(gameplayUI.transform, false);
        panel.layer = 5;
        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 1f);
        panelRt.pivot = new Vector2(0.5f, 1f);
        panelRt.anchoredPosition = new Vector2(0f, -170f);
        panelRt.sizeDelta = new Vector2(660f, 380f);

        var border = CreateStretchRect("CardBorder", panel.transform, -12f);
        var borderImg = border.AddComponent<Image>();
        borderImg.color = new Color(0f, 0f, 0f, 0.35f);
        borderImg.raycastTarget = false;

        var bg = CreateStretchRect("CardBackground", panel.transform);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.10f, 0.08f, 0.14f, 0.92f);
        bgImg.raycastTarget = false;

        float[] colX = { -228f, -76f, 76f, 228f };
        float[] rowY = { -108f, -264f };
        var squareRoots = new GameObject[8];
        for (int i = 0; i < 8; i++)
        {
            int row = i / 4, col = i % 4;
            var sq = new GameObject($"Sq{i}", typeof(RectTransform), typeof(Image));
            sq.transform.SetParent(panel.transform, false);
            sq.layer = 5;
            var rt = sq.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(colX[col], rowY[row]);
            rt.sizeDelta = new Vector2(140f, 140f);
            var backImg = sq.GetComponent<Image>();
            backImg.color = new Color(0.16f, 0.13f, 0.22f, 1f);
            backImg.raycastTarget = false;

            var glyph = new GameObject("Glyph", typeof(RectTransform), typeof(Image));
            glyph.transform.SetParent(sq.transform, false);
            glyph.layer = 5;
            var grt = glyph.GetComponent<RectTransform>();
            grt.sizeDelta = new Vector2(96f, 96f);
            var glyphImg = glyph.GetComponent<Image>();
            glyphImg.enabled = false;
            glyphImg.raycastTarget = false;
            glyphImg.preserveAspect = true;

            var cursor = CreateStretchRect("Cursor", sq.transform);
            var cursorImg = cursor.AddComponent<Image>();
            cursorImg.enabled = false;
            cursorImg.raycastTarget = false;

            squareRoots[i] = sq;
        }
        panel.SetActive(false);
        gameplayUI.SetActive(false);

        // ── Transition canvas (topmost) ──────────────────────────────────────
        var transCanvasGo = new GameObject("TransitionCanvas", typeof(Canvas), typeof(CanvasScaler));
        transCanvasGo.layer = 5;
        var transCanvas = transCanvasGo.GetComponent<Canvas>();
        transCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        transCanvas.sortingOrder = 100;
        var transPanel = CreateStretchRect("TransitionPanel", transCanvasGo.transform);
        var transImg = transPanel.AddComponent<Image>();
        transImg.color = Color.black;
        transImg.raycastTarget = false;
        var screenTransition = transPanel.AddComponent<ScreenTransition>();

        // ── GameManager + PauseHandler ───────────────────────────────────────
        var gmGo = new GameObject("GameManager", typeof(GameManager), typeof(PauseHandler));
        var gm = gmGo.GetComponent<GameManager>();
        var pause = gmGo.GetComponent<PauseHandler>();

        // ── Buttons ──────────────────────────────────────────────────────────
        MakeButton(mainPage.transform, "PlayButton", "Play", new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(320, 110),
                   b => UnityEventTools.AddPersistentListener(b.onClick, mode.StartFromMenu));
        MakeButton(mainPage.transform, "HomeButton", "Home", new Vector2(0.5f, 0.5f), new Vector2(0, -160), new Vector2(320, 90),
                   b => UnityEventTools.AddPersistentListener(b.onClick, menuManager.GoToHomeScene));
        MakeButton(scorePage.transform, "AgainButton", "Again", new Vector2(0.5f, 0f), new Vector2(-90, 140), new Vector2(240, 90), b =>
        {
            UnityEventTools.AddPersistentListener(b.onClick, gm.ResetGameValues);
            UnityEventTools.AddPersistentListener(b.onClick, mode.StartFromMenu);
        });
        MakeButton(scorePage.transform, "MenuButton", "Menu", new Vector2(0.5f, 0f), new Vector2(90 + 75, 140), new Vector2(240, 90), b =>
        {
            UnityEventTools.AddPersistentListener(b.onClick, gm.ResetGameValues);
            UnityEventTools.AddStringPersistentListener(b.onClick, menuManager.ShowPageLeft, "Main");
        });
        var pauseBtn = MakeButton(gameplayUI.transform, "PauseButton", "II", new Vector2(0.5f, 1f), new Vector2(0, -55), new Vector2(70, 70),
                   b => UnityEventTools.AddPersistentListener(b.onClick, gm.TogglePause));
        pauseBtn.transform.SetAsLastSibling();

        // ── Wiring: serialized fields ────────────────────────────────────────
        var notation = new (string prop, string path)[]
        {
            ("noteSprite",           "Assets/Images/Notation/QuarterNote.png"),
            ("restSprite",           "Assets/Images/Notation/QuarterRest.png"),
            ("eighthPairSprite",     "Assets/Images/Notation/EighthPair.png"),
            ("downbeatEighthSprite", "Assets/Images/Notation/DownbeatEighth.png"),
            ("offbeatEighthSprite",  "Assets/Images/Notation/OffbeatEighth.png"),
        };

        // DrumPadTouch: single wide drum routed to the center pad; no L/R zones.
        Set(drumPad, so => so.FindProperty("centerPad").objectReferenceValue = drumRt);

        // DungeonInputReader: pad + evaluator; visualController/health stay null (recon).
        Set(reader, so =>
        {
            so.FindProperty("drumPadTouch").objectReferenceValue = drumPad;
            so.FindProperty("evaluator").objectReferenceValue    = evaluator;
        });

        // DungeonEvaluator: FlashCards high-score key, no double-fail zeroing, HUD score.
        Set(evaluator, so =>
        {
            so.FindProperty("metronome").objectReferenceValue = metronome;
            so.FindProperty("scoreText").objectReferenceValue = scoreHud;
            so.FindProperty("highScoreKey").stringValue       = "FlashCardsHS";
            so.FindProperty("resetScoreOnRepeatedFailure").boolValue = false;
        });

        // FlashCardController: metronome + reader + evaluator; modeController stays null
        // (endless host passes the grid origin explicitly).
        Set(controller, so =>
        {
            so.FindProperty("metronome").objectReferenceValue   = metronome;
            so.FindProperty("inputReader").objectReferenceValue = reader;
            so.FindProperty("evaluator").objectReferenceValue   = evaluator;
        });

        // FlashCardView: panel + squares + glyphs.
        Set(view, so =>
        {
            so.FindProperty("controller").objectReferenceValue = controller;
            so.FindProperty("cardRoot").objectReferenceValue   = panelRt;
            so.FindProperty("cardBorder").objectReferenceValue = borderImg;
            foreach (var (prop, path) in notation)
                so.FindProperty(prop).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var squares = so.FindProperty("squares");
            squares.arraySize = 8;
            for (int i = 0; i < 8; i++)
            {
                var el = squares.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("root").objectReferenceValue   = squareRoots[i].GetComponent<RectTransform>();
                el.FindPropertyRelative("back").objectReferenceValue   = squareRoots[i].GetComponent<Image>();
                el.FindPropertyRelative("glyph").objectReferenceValue  = squareRoots[i].transform.Find("Glyph").GetComponent<Image>();
                el.FindPropertyRelative("cursor").objectReferenceValue = squareRoots[i].transform.Find("Cursor").GetComponent<Image>();
            }
        });

        // FlashCardsModeController: systems + HUD + results texts.
        Set(mode, so =>
        {
            so.FindProperty("metronome").objectReferenceValue      = metronome;
            so.FindProperty("cardController").objectReferenceValue = controller;
            so.FindProperty("evaluator").objectReferenceValue      = evaluator;
            var pipProp = so.FindProperty("strikePips");
            pipProp.arraySize = 3;
            for (int i = 0; i < 3; i++) pipProp.GetArrayElementAtIndex(i).objectReferenceValue = pips[i];
            so.FindProperty("streakText").objectReferenceValue       = streakHud;
            so.FindProperty("bestStreakText").objectReferenceValue   = bestStreakText;
            so.FindProperty("cardsClearedText").objectReferenceValue = cardsClearedText;
        });

        // UIMenuManager: pages + transitioner + canvas rect.
        Set(menuManager, so =>
        {
            var pages = so.FindProperty("menuPages");
            pages.arraySize = 2;
            var p0 = pages.GetArrayElementAtIndex(0);
            p0.FindPropertyRelative("pageName").stringValue = "Main";
            p0.FindPropertyRelative("pageTransform").objectReferenceValue = mainPage.GetComponent<RectTransform>();
            var p1 = pages.GetArrayElementAtIndex(1);
            p1.FindPropertyRelative("pageName").stringValue = "Score";
            p1.FindPropertyRelative("pageTransform").objectReferenceValue = scorePage.GetComponent<RectTransform>();
            so.FindProperty("startingPageName").stringValue = "Main";
            so.FindProperty("screenTransitioner").objectReferenceValue  = screenTransition;
            so.FindProperty("canvasRectTransform").objectReferenceValue = canvasGo.GetComponent<RectTransform>();
        });

        // GameManager: shared systems + mode registration.
        Set(gm, so =>
        {
            so.FindProperty("metronome").objectReferenceValue         = metronome;
            so.FindProperty("timingCoordinator").objectReferenceValue = timingGo.GetComponent<TimingCoordinator>();
            so.FindProperty("screenTransition").objectReferenceValue  = screenTransition;
            so.FindProperty("menuManager").objectReferenceValue       = menuManager;
            so.FindProperty("gameplayElements").objectReferenceValue  = gameplayUI;
            so.FindProperty("pageToCloseOnStart").objectReferenceValue = mainPage;
            var modes = so.FindProperty("modeControllers");
            modes.arraySize = 1;
            modes.GetArrayElementAtIndex(0).objectReferenceValue = mode;
            so.FindProperty("defaultModeId").stringValue = "FlashCards";
        });

        Set(pause, so => so.FindProperty("metronome").objectReferenceValue = metronome);

        // ── Save + Build Settings ────────────────────────────────────────────
        bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
        sb.AppendLine($"Scene saved: {saved} → {ScenePath}");

        var buildScenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool present = buildScenes.Exists(s => s.path == ScenePath);
        if (!present)
        {
            buildScenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = buildScenes.ToArray();
            sb.AppendLine("Added to Build Settings.");
        }

        EditorSceneManager.OpenScene("Assets/Scenes/Dungeon.unity", OpenSceneMode.Single);
        sb.AppendLine("Reopened Dungeon scene.");
        return sb.ToString();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

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

    private static Button MakeButton(Transform parent, string name, string label,
                                     Vector2 anchor, Vector2 pos, Vector2 size,
                                     System.Action<Button> wire)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.layer = 5;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        img.type = Image.Type.Sliced;
        img.color = new Color(0.93f, 0.79f, 0.44f, 1f);

        var labelTmp = MakeText(go.transform, "Label", label, 34, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        labelTmp.color = Color.black;

        var button = go.GetComponent<Button>();
        wire(button);
        return button;
    }

    private static void Set(Object target, System.Action<SerializedObject> edit)
    {
        var so = new SerializedObject(target);
        edit(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
