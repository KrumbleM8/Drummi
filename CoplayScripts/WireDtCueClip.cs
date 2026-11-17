using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using KrumbleHut.Drummi.Dungeons;

public class WireDtCueClip
{
    public static void Execute()
    {
        var view = Object.FindFirstObjectByType<FlashCardView>(FindObjectsInactive.Include);
        if (view == null) { Debug.Log("[DtCue] FlashCardView not found"); return; }

        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Pickup11.wav");
        if (clip == null) { Debug.Log("[DtCue] Pickup11.wav not found"); return; }

        var so = new SerializedObject(view);
        var p  = so.FindProperty("doubleTimeCueClip");
        if (p == null) { Debug.Log("[DtCue] doubleTimeCueClip property not found"); return; }
        p.objectReferenceValue = clip;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
        Debug.Log("[DtCue] Wired Pickup11.wav to FlashCardView.doubleTimeCueClip");
    }
}
