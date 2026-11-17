using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using KrumbleHut.Drummi.Dungeons;

public class SetCompressReveal
{
    public static void Execute()
    {
        var ctrl = Object.FindFirstObjectByType<FlashCardController>(FindObjectsInactive.Include);
        if (ctrl == null) { Debug.Log("[Compress] FlashCardController not found"); return; }
        var so = new SerializedObject(ctrl);
        so.FindProperty("compressReveal").boolValue = true;
        so.FindProperty("revealBars").intValue = 2;
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(ctrl.gameObject.scene);
        Debug.Log("[Compress] compressReveal=true revealBars=2 — scene dirty");
    }
}
