using UnityEngine;
using UnityEditor;
using KrumbleHut.Drummi.FlashCards;

// Temp verification helpers: force / restore escalation step 0's doubleTimeChance.
public class SetDtChance
{
    public static void Execute() => Set(1f);          // force every card DT
    public static void Half()    => Set(0.5f);        // mixed run
    public static void Restore() => Set(0f);          // shipping value

    private static void Set(float v)
    {
        var mode = Object.FindFirstObjectByType<FlashCardsModeController>(FindObjectsInactive.Include);
        if (mode == null) { Debug.Log("[DtChance] FlashCardsModeController not found"); return; }
        var so    = new SerializedObject(mode);
        var steps = so.FindProperty("escalationSteps");
        if (steps.arraySize == 0) { Debug.Log("[DtChance] no steps"); return; }
        steps.GetArrayElementAtIndex(0).FindPropertyRelative("doubleTimeChance").floatValue = v;
        so.ApplyModifiedProperties();
        Debug.Log($"[DtChance] step 0 doubleTimeChance={v:F2}");
    }
}
