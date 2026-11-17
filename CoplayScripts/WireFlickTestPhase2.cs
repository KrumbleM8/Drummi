using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KrumbleHut.Drummi.Dungeons;

public static class WireFlickTestPhase2
{
    public static void Execute()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/FlickTest.unity", OpenSceneMode.Single);

        var metronome = Object.FindFirstObjectByType<Metronome>();
        var reader = Object.FindFirstObjectByType<FlickInputReader>();
        var driver = Object.FindFirstObjectByType<FlickTestDriver>();
        if (metronome == null || reader == null || driver == null)
        {
            Debug.LogError("[Coplay] Missing Phase 1 objects in FlickTest scene.");
            return;
        }

        // TimingCoordinator (scene-local singleton, like GameClock).
        var coordinator = Object.FindFirstObjectByType<TimingCoordinator>();
        if (coordinator == null)
            coordinator = new GameObject("TimingCoordinator").AddComponent<TimingCoordinator>();

        // DungeonHealth — no slider, defaults (maxHealth 100, damagePerMiss 10).
        var health = Object.FindFirstObjectByType<DungeonHealth>();
        if (health == null)
            health = new GameObject("DungeonHealth").AddComponent<DungeonHealth>();

        // DungeonEvaluator — window-fraction source only. Wire its metronome ref,
        // disable the two-strike score reset (Flash Cards pattern), sandbox the
        // high-score key even though this path never writes it.
        var evaluator = Object.FindFirstObjectByType<DungeonEvaluator>();
        if (evaluator == null)
            evaluator = new GameObject("DungeonEvaluator").AddComponent<DungeonEvaluator>();
        var evalSO = new SerializedObject(evaluator);
        evalSO.FindProperty("metronome").objectReferenceValue = metronome;
        evalSO.FindProperty("resetScoreOnRepeatedFailure").boolValue = false;
        evalSO.FindProperty("highScoreKey").stringValue = "FlickTestHS";
        evalSO.ApplyModifiedPropertiesWithoutUndo();

        // FlickBeatManager.
        var manager = Object.FindFirstObjectByType<FlickBeatManager>();
        if (manager == null)
            manager = new GameObject("FlickBeatManager").AddComponent<FlickBeatManager>();
        var mgrSO = new SerializedObject(manager);
        mgrSO.FindProperty("metronome").objectReferenceValue = metronome;
        mgrSO.FindProperty("evaluator").objectReferenceValue = evaluator;
        mgrSO.FindProperty("flickReader").objectReferenceValue = reader;
        mgrSO.FindProperty("health").objectReferenceValue = health;
        mgrSO.ApplyModifiedPropertiesWithoutUndo();

        // Driver — new refs added in Phase 2.
        var drvSO = new SerializedObject(driver);
        drvSO.FindProperty("metronome").objectReferenceValue = metronome;
        drvSO.FindProperty("flickReader").objectReferenceValue = reader;
        drvSO.FindProperty("flickBeatManager").objectReferenceValue = manager;
        drvSO.FindProperty("health").objectReferenceValue = health;
        drvSO.ApplyModifiedPropertiesWithoutUndo();

        // Reader gate starts false; FlickBeatManager.StartGameplay arms it.
        var readerSO = new SerializedObject(reader);
        readerSO.FindProperty("encounterActive").boolValue = false;
        readerSO.ApplyModifiedPropertiesWithoutUndo();

        bool saved = EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Coplay] FlickTest Phase 2 wiring saved: {saved}");
    }
}
