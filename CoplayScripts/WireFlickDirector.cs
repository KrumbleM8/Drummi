using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KrumbleHut.Drummi.Dungeons;
using KrumbleHut.Drummi.Flick;

public static class WireFlickDirector
{
    public static void Execute()
    {
        var mode        = Object.FindObjectOfType<FlickModeController>(true);
        var beatManager = Object.FindObjectOfType<FlickBeatManager>(true);
        var reader      = Object.FindObjectOfType<FlickInputReader>(true);
        var sweep       = Object.FindObjectOfType<FlickTargetSweep>(true);
        var ball        = Object.FindObjectOfType<FlickBallController>(true);
        var visual      = Object.FindObjectOfType<FlickTargetVisual>(true);
        var enemy       = Object.FindObjectOfType<FlickEnemy>(true);

        if (mode == null || beatManager == null || reader == null || sweep == null ||
            ball == null || visual == null || enemy == null)
        {
            Debug.LogError($"[WireFlickDirector] Missing pieces: mode={mode} bm={beatManager} " +
                           $"reader={reader} sweep={sweep} ball={ball} visual={visual} enemy={enemy}");
            return;
        }

        var director = mode.gameObject.GetComponent<FlickEncounterDirector>();
        if (director == null) director = mode.gameObject.AddComponent<FlickEncounterDirector>();

        var so = new SerializedObject(director);
        so.FindProperty("beatManager").objectReferenceValue  = beatManager;
        so.FindProperty("flickReader").objectReferenceValue  = reader;
        so.FindProperty("targetSweep").objectReferenceValue  = sweep;
        so.FindProperty("ball").objectReferenceValue         = ball;
        so.FindProperty("targetVisual").objectReferenceValue = visual;
        so.FindProperty("flickEnemy").objectReferenceValue   = enemy;
        so.ApplyModifiedPropertiesWithoutUndo();

        var soMode = new SerializedObject(mode);
        var dirProp = soMode.FindProperty("director");
        if (dirProp == null) { Debug.LogError("[WireFlickDirector] 'director' field not found on mode"); return; }
        dirProp.objectReferenceValue = director;
        soMode.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(mode.gameObject.scene);
        bool saved = EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[WireFlickDirector] Wired director on '{mode.gameObject.name}' — saved={saved}");
    }
}
