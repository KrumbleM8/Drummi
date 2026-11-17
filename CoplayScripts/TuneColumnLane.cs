using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KrumbleHut.Drummi.Dungeons;

public static class TuneColumnLane
{
    // Edit-mode tuning: raises the column lane so the hit zone sits further
    // above the pad, serialized onto the SCENE's FlashCardView instance
    // (script defaults never reach existing instances), then saves the scene.
    public static void Execute()
    {
        if (Application.isPlaying)
        {
            Debug.Log("[TuneColumnLane] Exit play mode first — play-mode edits don't persist.");
            return;
        }

        var view = Object.FindFirstObjectByType<FlashCardView>(FindObjectsInactive.Include);
        if (view == null)
        {
            Debug.Log("[TuneColumnLane] No FlashCardView in the open scene.");
            return;
        }

        var so = new SerializedObject(view);
        var prop = so.FindProperty("columnLaneBottomY");
        if (prop == null)
        {
            Debug.Log("[TuneColumnLane] columnLaneBottomY property not found.");
            return;
        }
        float old = prop.floatValue;
        prop.floatValue = 560f;
        so.ApplyModifiedProperties();

        bool saved = EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[TuneColumnLane] columnLaneBottomY {old} -> 560 on '{view.gameObject.name}', scene saved={saved}");
    }
}
