using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// Wires all Inspector references in the Dungeon scene and creates the placeholder SongItem.
/// Run once after DungeonSceneBuilder.Execute().
/// </summary>
public class DungeonSceneWiring
{
    public static void Execute()
    {
        // ── Locate every key object up-front ─────────────────────────────
        var gameManagerGO     = GameObject.Find("GameManager");
        var metronomeGO       = GameObject.Find("Metronome");
        var timingCoordGO     = GameObject.Find("TimingCoordinator");
        var audioManagerGO    = GameObject.Find("AudioManager");
        var dungeonModeGO     = GameObject.Find("DungeonMode");
        var drumPadTouchGO    = GameObject.Find("DrumPadTouch");
        var barSliderGO       = GameObject.Find("Canvas/GameplayUI/BarSlider");
        var responseFillGO    = GameObject.Find("Canvas/GameplayUI/ResponseFill");
        var scoreTextGO       = GameObject.Find("Canvas/GameplayUI/ScoreText");
        var leftPadGO         = GameObject.Find("Canvas/InputPads/LeftPad");
        var centerPadGO       = GameObject.Find("Canvas/InputPads/CenterPad");
        var rightPadGO        = GameObject.Find("Canvas/InputPads/RightPad");
        var startButtonGO     = GameObject.Find("Canvas/StartButton");

        if (gameManagerGO == null || metronomeGO == null || dungeonModeGO == null)
        {
            Debug.LogError("[DungeonSceneWiring] Essential GameObjects missing — run DungeonSceneBuilder first.");
            return;
        }

        // ── 1. SongCarousel + placeholder SongItem ────────────────────────
        var carouselGO = new GameObject("SongCarousel");
        var songItemGO = new GameObject("Song_Placeholder", typeof(RectTransform));
        songItemGO.transform.SetParent(carouselGO.transform, false);
        var si    = songItemGO.AddComponent<SongItem>();
        si.title  = "Placeholder";
        si.bpm    = 120f;
        Debug.Log("[DungeonSceneWiring] SongCarousel created with placeholder (120 BPM).");

        // ── 2. GameManager ────────────────────────────────────────────────
        var gmComp = gameManagerGO.GetComponent<GameManager>();
        var gmSO   = new SerializedObject(gmComp);

        gmSO.FindProperty("metronome").objectReferenceValue =
            metronomeGO.GetComponent<Metronome>();
        gmSO.FindProperty("timingCoordinator").objectReferenceValue =
            timingCoordGO.GetComponent<TimingCoordinator>();
        gmSO.FindProperty("pauseHandler").objectReferenceValue =
            gameManagerGO.GetComponent<PauseHandler>();
        gmSO.FindProperty("songCarouselContent").objectReferenceValue =
            carouselGO.transform;
        gmSO.FindProperty("defaultModeId").stringValue = "Dungeon";

        var modeListProp = gmSO.FindProperty("modeControllers");
        modeListProp.arraySize = 1;
        modeListProp.GetArrayElementAtIndex(0).objectReferenceValue =
            dungeonModeGO.GetComponent<DungeonModeController>();

        gmSO.ApplyModifiedProperties();
        Debug.Log("[DungeonSceneWiring] GameManager wired.");

        // ── 3. PauseHandler ───────────────────────────────────────────────
        var ph   = gameManagerGO.GetComponent<PauseHandler>();
        var phSO = new SerializedObject(ph);
        phSO.FindProperty("metronome").objectReferenceValue =
            metronomeGO.GetComponent<Metronome>();
        phSO.ApplyModifiedProperties();
        Debug.Log("[DungeonSceneWiring] PauseHandler wired.");

        // ── 4. AudioManager ───────────────────────────────────────────────
        var am   = audioManagerGO.GetComponent<AudioManager>();
        var amSO = new SerializedObject(am);

        var speakersProp = amSO.FindProperty("speakers");
        speakersProp.arraySize = 3;
        speakersProp.GetArrayElementAtIndex(0).objectReferenceValue =
            audioManagerGO.transform.Find("Speaker_Music").GetComponent<AudioSource>();
        speakersProp.GetArrayElementAtIndex(1).objectReferenceValue =
            audioManagerGO.transform.Find("Speaker_Bongos").GetComponent<AudioSource>();
        speakersProp.GetArrayElementAtIndex(2).objectReferenceValue =
            audioManagerGO.transform.Find("Speaker_Misc").GetComponent<AudioSource>();

        // Allocate empty clip slots — user assigns clips in Inspector
        amSO.FindProperty("musicTracks").arraySize  = 1;  // 1 music track slot
        amSO.FindProperty("bongoSounds").arraySize  = 2;  // left / right bongo
        amSO.FindProperty("otherSounds").arraySize  = 6;  // incorrect/correct/fail/allperfect/passable/turnsignal
        amSO.FindProperty("drumMachineSounds").arraySize = 0;

        amSO.ApplyModifiedProperties();
        Debug.Log("[DungeonSceneWiring] AudioManager speakers wired (clip slots allocated).");

        // ── 5. DungeonModeController ──────────────────────────────────────
        var dmc   = dungeonModeGO.GetComponent<DungeonModeController>();
        var dmcSO = new SerializedObject(dmc);
        dmcSO.FindProperty("beatManager").objectReferenceValue =
            dungeonModeGO.GetComponent<DungeonBeatManager>();
        dmcSO.FindProperty("evaluator").objectReferenceValue =
            dungeonModeGO.GetComponent<DungeonEvaluator>();
        dmcSO.FindProperty("visualController").objectReferenceValue =
            dungeonModeGO.GetComponent<DungeonVisualController>();
        dmcSO.ApplyModifiedProperties();
        Debug.Log("[DungeonSceneWiring] DungeonModeController wired.");

        // ── 6. DungeonBeatManager ─────────────────────────────────────────
        var dbm   = dungeonModeGO.GetComponent<DungeonBeatManager>();
        var dbmSO = new SerializedObject(dbm);
        dbmSO.FindProperty("metronome").objectReferenceValue =
            metronomeGO.GetComponent<Metronome>();
        dbmSO.FindProperty("evaluator").objectReferenceValue =
            dungeonModeGO.GetComponent<DungeonEvaluator>();
        dbmSO.FindProperty("inputReader").objectReferenceValue =
            dungeonModeGO.GetComponent<DungeonInputReader>();
        dbmSO.FindProperty("visualController").objectReferenceValue =
            dungeonModeGO.GetComponent<DungeonVisualController>();

        var easProp = dbmSO.FindProperty("enemyAudioSources");
        easProp.arraySize = 3;
        easProp.GetArrayElementAtIndex(0).objectReferenceValue =
            dungeonModeGO.transform.Find("EnemyAudio_Left").GetComponent<AudioSource>();
        easProp.GetArrayElementAtIndex(1).objectReferenceValue =
            dungeonModeGO.transform.Find("EnemyAudio_Center").GetComponent<AudioSource>();
        easProp.GetArrayElementAtIndex(2).objectReferenceValue =
            dungeonModeGO.transform.Find("EnemyAudio_Right").GetComponent<AudioSource>();

        dbmSO.FindProperty("enemySoundClips").arraySize = 3; // user assigns clips
        dbmSO.ApplyModifiedProperties();
        Debug.Log("[DungeonSceneWiring] DungeonBeatManager wired.");

        // ── 7. DungeonEvaluator ───────────────────────────────────────────
        var de   = dungeonModeGO.GetComponent<DungeonEvaluator>();
        var deSO = new SerializedObject(de);
        deSO.FindProperty("metronome").objectReferenceValue =
            metronomeGO.GetComponent<Metronome>();
        if (scoreTextGO != null)
            deSO.FindProperty("scoreText").objectReferenceValue =
                scoreTextGO.GetComponent<TMP_Text>();
        deSO.ApplyModifiedProperties();
        Debug.Log("[DungeonSceneWiring] DungeonEvaluator wired.");

        // ── 8. DungeonInputReader ─────────────────────────────────────────
        var dir   = dungeonModeGO.GetComponent<DungeonInputReader>();
        var dirSO = new SerializedObject(dir);
        if (drumPadTouchGO != null)
            dirSO.FindProperty("drumPadTouch").objectReferenceValue =
                drumPadTouchGO.GetComponent<DrumPadTouch>();
        dirSO.FindProperty("evaluator").objectReferenceValue =
            dungeonModeGO.GetComponent<DungeonEvaluator>();
        dirSO.FindProperty("visualController").objectReferenceValue =
            dungeonModeGO.GetComponent<DungeonVisualController>();
        dirSO.ApplyModifiedProperties();
        Debug.Log("[DungeonSceneWiring] DungeonInputReader wired.");

        // ── 9. DungeonVisualController ────────────────────────────────────
        var dvc   = dungeonModeGO.GetComponent<DungeonVisualController>();
        var dvcSO = new SerializedObject(dvc);

        if (barSliderGO != null)
        {
            dvcSO.FindProperty("barSlider").objectReferenceValue =
                barSliderGO.GetComponent<Slider>();

            var ipTransform = barSliderGO.transform.Find("IndicatorParent");
            if (ipTransform != null)
                dvcSO.FindProperty("indicatorParent").objectReferenceValue =
                    ipTransform.GetComponent<RectTransform>();
        }

        if (responseFillGO != null)
            dvcSO.FindProperty("responseFill").objectReferenceValue =
                responseFillGO.GetComponent<Image>();

        dvcSO.FindProperty("metronome").objectReferenceValue =
            metronomeGO.GetComponent<Metronome>();

        var spawnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Dungeon/SpawnIndicator.prefab");
        var inputPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Dungeon/InputIndicator.prefab");
        dvcSO.FindProperty("spawnIndicatorPrefab").objectReferenceValue = spawnPrefab;
        dvcSO.FindProperty("inputIndicatorPrefab").objectReferenceValue = inputPrefab;

        dvcSO.ApplyModifiedProperties();
        Debug.Log("[DungeonSceneWiring] DungeonVisualController wired.");

        // ── 10. DrumPadTouch ──────────────────────────────────────────────
        if (drumPadTouchGO != null)
        {
            var dpt   = drumPadTouchGO.GetComponent<DrumPadTouch>();
            var dptSO = new SerializedObject(dpt);
            if (leftPadGO   != null)
                dptSO.FindProperty("leftPad").objectReferenceValue =
                    leftPadGO.GetComponent<RectTransform>();
            if (centerPadGO != null)
                dptSO.FindProperty("centerPad").objectReferenceValue =
                    centerPadGO.GetComponent<RectTransform>();
            if (rightPadGO  != null)
                dptSO.FindProperty("rightPad").objectReferenceValue =
                    rightPadGO.GetComponent<RectTransform>();
            dptSO.ApplyModifiedProperties();
            Debug.Log("[DungeonSceneWiring] DrumPadTouch wired.");
        }

        // ── 11. StartButton OnClick ───────────────────────────────────────
        if (startButtonGO != null)
        {
            var btn = startButtonGO.GetComponent<Button>();
            if (btn != null)
            {
                // SetMode("Dungeon")
                UnityEventTools.AddStringPersistentListener(
                    btn.onClick,
                    gmComp.SetMode,
                    "Dungeon");

                // StartGame()
                UnityEventTools.AddVoidPersistentListener(
                    btn.onClick,
                    gmComp.StartGame);

                EditorUtility.SetDirty(btn);
                Debug.Log("[DungeonSceneWiring] StartButton OnClick wired.");
            }
        }

        // ── Save ──────────────────────────────────────────────────────────
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[DungeonSceneWiring] ✓ All wiring complete.");
    }
}
