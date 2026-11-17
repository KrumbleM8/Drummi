using System.Text;
using KrumbleHut.Drummi.Pulse;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Builds Assets/Scenes/Pulse.unity from scratch (BuildFlashCardsScene skeleton):
/// camera, GameClock, TimingCoordinator, Metronome @100, AudioManager prefab,
/// EventSystem, canvas (blank Main page + Score page, GameplayUI with the shaker
/// pad / HUD / pause), transition canvas (no auto-reveal — the mode auto-starts),
/// GameManager + PauseHandler, PulseSystems (DrumPadTouch + PulseModeController),
/// full wiring, Build Settings entry. Run BuildPulseAudio first.
/// Idempotent: overwrites the scene file and leaves it open.
/// </summary>
public static class BuildPulseScene
{
    private const string ScenePath = "Assets/Scenes/Pulse.unity";
    private const string AudioDir  = "Assets/Audio/Pulse/";

    public static string Execute()
    {
        if (EditorApplication.isPlaying) return "ABORT: exit play mode first.";
        var sb = new StringBuilder();

        var shaker = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "Shaker.wav");
        string[] stemNames = { "Kick", "Hats", "Clap", "Bass" };
        var stems = new AudioClip[stemNames.Length];
        for (int i = 0; i < stemNames.Length; i++) stems[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + stemNames[i] + ".wav");
        if (shaker == null || System.Array.Exists(stems, c => c == null)) return "ABORT: run BuildPulseAudio first (clips missing).";

        var amPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/AudioManager.prefab");
        if (amPrefab == null) return "ABORT: AudioManager prefab missing.";

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── Camera + shared systems ──────────────────────────────────────────
        var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camGo.tag = "MainCamera";
        var cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.07f, 0.11f, 0.13f, 1f);
        cam.orthographic = true;

        new GameObject("GameClock", typeof(GameClock));
        var timingGo = new GameObject("TimingCoordinator", typeof(TimingCoordinator));
        var metroGo  = new GameObject("Metronome", typeof(Metronome));
        var metronome = metroGo.GetComponent<Metronome>();
        metronome.bpm = 100;
        PrefabUtility.InstantiatePrefab(amPrefab);
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        // ── Canvas ───────────────────────────────────────────────────────────
        var canvasGo = new GameObject("PulseCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.layer = 5;
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800, 600);
        scaler.matchWidthOrHeight = 0f;

        // Blank Main page: UIMenuManager shows page 0 on load; nothing else lives here.
        var mainPage = CreateStretchRect("MainPage", canvasGo.transform);

        var scorePage = CreateStretchRect("ScorePage", canvasGo.transform);
        var scoreBg = scorePage.AddComponent<Image>();
        scoreBg.color = new Color(0.08f, 0.10f, 0.14f, 1f);
        MakeText(scorePage.transform, "ResultsTitle", "PULSE LOST", 56, new Vector2(0.5f, 1f), new Vector2(0, -120), new Vector2(700, 90));
        var finalScoreText  = MakeText(scorePage.transform, "FinalScoreText",  "0", 72, new Vector2(0.5f, 1f), new Vector2(0, -230), new Vector2(700, 100));
        var perfectHitsText = MakeText(scorePage.transform, "PerfectHitsText", "Perfect Hits: 0", 30, new Vector2(0.5f, 1f), new Vector2(0, -330), new Vector2(700, 60));
        var highScoreText   = MakeText(scorePage.transform, "HighScoreText",   "High Score: 0", 30, new Vector2(0.5f, 1f), new Vector2(0, -390), new Vector2(700, 60));
        var scoreScreen = scorePage.AddComponent<ScoreScreen>();
        scoreScreen.finalScoreText  = finalScoreText;
        scoreScreen.perfectHitsText = perfectHitsText;
        scoreScreen.highScoreText   = highScoreText;

        var menuGo = new GameObject("MenuUI", typeof(RectTransform), typeof(UIMenuManager));
        menuGo.transform.SetParent(canvasGo.transform, false);
        menuGo.layer = 5;
        var menuManager = menuGo.GetComponent<UIMenuManager>();

        // ── Gameplay UI ──────────────────────────────────────────────────────
        var gameplayUI = CreateStretchRect("GameplayUI", canvasGo.transform);

        var shakerGo = new GameObject("ShakerPad", typeof(RectTransform), typeof(Image));
        shakerGo.transform.SetParent(gameplayUI.transform, false);
        shakerGo.layer = 5;
        var shakerRt = shakerGo.GetComponent<RectTransform>();
        shakerRt.anchorMin = shakerRt.anchorMax = new Vector2(0.5f, 0f);
        shakerRt.pivot = new Vector2(0.5f, 0.5f);
        shakerRt.anchoredPosition = new Vector2(0, 190);
        shakerRt.sizeDelta = new Vector2(300, 300);
        var shakerImg = shakerGo.GetComponent<Image>();
        shakerImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        shakerImg.color = new Color(0.95f, 0.78f, 0.35f, 1f);

        var scoreHud = MakeText(gameplayUI.transform, "ScoreText", "0", 44, new Vector2(1f, 1f), new Vector2(-100, -50), new Vector2(220, 70));
        scoreHud.alignment = TextAlignmentOptions.Right;
        var streakHud = MakeText(gameplayUI.transform, "StreakText", "Streak: 0", 30, new Vector2(0f, 1f), new Vector2(130, -50), new Vector2(240, 50));
        streakHud.alignment = TextAlignmentOptions.Left;
        var stageHud = MakeText(gameplayUI.transform, "StageText", "Stage 1", 30, new Vector2(0f, 1f), new Vector2(130, -95), new Vector2(240, 50));
        stageHud.alignment = TextAlignmentOptions.Left;
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
        screenTransition.autoRevealOnStart = false; // GameManager.StartGameSequence owns the first reveal

        // ── GameManager + Pulse systems ──────────────────────────────────────
        var gmGo = new GameObject("GameManager", typeof(GameManager), typeof(PauseHandler));
        var gm = gmGo.GetComponent<GameManager>();
        var pause = gmGo.GetComponent<PauseHandler>();

        var systemsGo = new GameObject("PulseSystems", typeof(DrumPadTouch), typeof(PulseModeController));
        var drumPad = systemsGo.GetComponent<DrumPadTouch>();
        var mode    = systemsGo.GetComponent<PulseModeController>();

        // ── Buttons ──────────────────────────────────────────────────────────
        MakeButton(scorePage.transform, "AgainButton", "Again", new Vector2(0.5f, 0f), new Vector2(-130, 140), new Vector2(240, 90), b =>
        {
            UnityEventTools.AddPersistentListener(b.onClick, gm.ResetGameValues);
            UnityEventTools.AddPersistentListener(b.onClick, gm.StartGame);
        });
        MakeButton(scorePage.transform, "HomeButton", "Home", new Vector2(0.5f, 0f), new Vector2(130, 140), new Vector2(240, 90),
                   b => UnityEventTools.AddPersistentListener(b.onClick, menuManager.GoToHomeScene));
        MakeButton(gameplayUI.transform, "PauseButton", "II", new Vector2(0.5f, 1f), new Vector2(0, -55), new Vector2(70, 70),
                   b => UnityEventTools.AddPersistentListener(b.onClick, gm.TogglePause)).transform.SetAsLastSibling();

        // ── Wiring ───────────────────────────────────────────────────────────
        Set(drumPad, so => so.FindProperty("centerPad").objectReferenceValue = shakerRt);

        Set(mode, so =>
        {
            so.FindProperty("drumPad").objectReferenceValue      = drumPad;
            so.FindProperty("shakerClip").objectReferenceValue   = shaker;
            so.FindProperty("streakText").objectReferenceValue   = streakHud;
            so.FindProperty("stageText").objectReferenceValue    = stageHud;
            so.FindProperty("scoreText").objectReferenceValue    = scoreHud;
            so.FindProperty("shakerVisual").objectReferenceValue = shakerRt;
            var stemProp = so.FindProperty("stems");
            stemProp.arraySize = stems.Length;
            for (int i = 0; i < stems.Length; i++) stemProp.GetArrayElementAtIndex(i).objectReferenceValue = stems[i];

            // streak → [Kick, Hats, Clap, Bass]; last stage drops the plain pulse.
            (int streak, float[] vols)[] table =
            {
                (0,  new[] { 1f, 0f, 0f, 0f }),
                (8,  new[] { 1f, 1f, 0f, 0f }),
                (16, new[] { 1f, 1f, 1f, 0f }),
                (24, new[] { 1f, 1f, 1f, 1f }),
                (32, new[] { 0f, 1f, 1f, 1f }),
            };
            var stagesProp = so.FindProperty("stages");
            stagesProp.arraySize = table.Length;
            for (int i = 0; i < table.Length; i++)
            {
                var el = stagesProp.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("streak").intValue = table[i].streak;
                var vols = el.FindPropertyRelative("layerVolumes");
                vols.arraySize = table[i].vols.Length;
                for (int j = 0; j < vols.arraySize; j++) vols.GetArrayElementAtIndex(j).floatValue = table[i].vols[j];
            }
        });

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

        Set(gm, so =>
        {
            so.FindProperty("metronome").objectReferenceValue         = metronome;
            so.FindProperty("timingCoordinator").objectReferenceValue = timingGo.GetComponent<TimingCoordinator>();
            so.FindProperty("screenTransition").objectReferenceValue  = screenTransition;
            so.FindProperty("menuManager").objectReferenceValue       = menuManager;
            so.FindProperty("gameplayElements").objectReferenceValue  = gameplayUI;
            so.FindProperty("pageToCloseOnStart").objectReferenceValue = scorePage; // "Again" hides the results
            var modes = so.FindProperty("modeControllers");
            modes.arraySize = 1;
            modes.GetArrayElementAtIndex(0).objectReferenceValue = mode;
            so.FindProperty("defaultModeId").stringValue = "Pulse";
        });

        Set(pause, so => so.FindProperty("metronome").objectReferenceValue = metronome);

        // ── Save + Build Settings ────────────────────────────────────────────
        bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
        sb.AppendLine($"Scene saved: {saved} → {ScenePath}");

        var buildScenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!buildScenes.Exists(s => s.path == ScenePath))
        {
            buildScenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = buildScenes.ToArray();
            sb.AppendLine("Added to Build Settings at index " + (buildScenes.Count - 1));
        }
        return sb.ToString();
    }

    // ── Helpers (BuildFlashCardsScene) ──────────────────────────────────────

    private static GameObject CreateStretchRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = 5;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
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
                                     Vector2 anchor, Vector2 pos, Vector2 size, System.Action<Button> wire)
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
