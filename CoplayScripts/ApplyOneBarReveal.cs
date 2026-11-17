using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using KrumbleHut.Drummi.Dungeons;
using KrumbleHut.Drummi.FlashCards;

public class ApplyOneBarReveal
{
    public static void Execute()
    {
        var ctrl = Object.FindFirstObjectByType<FlashCardController>(FindObjectsInactive.Include);
        if (ctrl == null) { Debug.Log("[OneBar] FlashCardController not found"); return; }
        var cso = new SerializedObject(ctrl);
        cso.FindProperty("revealBars").intValue = 1;
        cso.FindProperty("compressReveal").boolValue = true;
        cso.ApplyModifiedProperties();
        Debug.Log("[OneBar] revealBars=1 compressReveal=true");

        var mode = Object.FindFirstObjectByType<FlashCardsModeController>(FindObjectsInactive.Include);
        if (mode == null) { Debug.Log("[OneBar] FlashCardsModeController not found"); return; }
        var mso   = new SerializedObject(mode);
        var steps = mso.FindProperty("escalationSteps");
        for (int i = 0; i < steps.arraySize; i++)
            steps.GetArrayElementAtIndex(i).FindPropertyRelative("doubleTimeChance").floatValue = 0f;
        mso.ApplyModifiedProperties();
        Debug.Log($"[OneBar] doubleTimeChance=0 on all {steps.arraySize} steps");

        EditorSceneManager.MarkSceneDirty(ctrl.gameObject.scene);
        Debug.Log("[OneBar] Done — scene dirty.");
    }
}
