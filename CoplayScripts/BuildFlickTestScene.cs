using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KrumbleHut.Drummi.Dungeons;

public static class BuildFlickTestScene
{
    public static void Execute()
    {
        // Fresh scene with default Main Camera + Directional Light.
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // GameClock (scene-local singleton).
        new GameObject("GameClock", typeof(GameClock));

        // Metronome with audible tick.
        var metronomeGO = new GameObject("Metronome");
        var speaker = metronomeGO.AddComponent<AudioSource>();
        speaker.playOnAwake = false;
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/click.wav");
        if (clip == null) Debug.LogError("[Coplay] click.wav not found!");
        speaker.clip = clip;
        var metronome = metronomeGO.AddComponent<Metronome>();
        metronome.bpm = 120.0;
        metronome.playTick = true;
        metronome.speaker = speaker;

        // Flick reader + test driver.
        var readerGO = new GameObject("FlickInputReader");
        var reader = readerGO.AddComponent<FlickInputReader>();

        var driverGO = new GameObject("FlickTestDriver");
        var driver = driverGO.AddComponent<FlickTestDriver>();

        // Wire the driver's serialized private refs.
        var so = new SerializedObject(driver);
        so.FindProperty("metronome").objectReferenceValue = metronome;
        so.FindProperty("flickReader").objectReferenceValue = reader;
        so.ApplyModifiedPropertiesWithoutUndo();

        bool saved = EditorSceneManager.SaveScene(scene, "Assets/Scenes/FlickTest.unity");
        Debug.Log($"[Coplay] FlickTest scene saved: {saved}");
    }
}
