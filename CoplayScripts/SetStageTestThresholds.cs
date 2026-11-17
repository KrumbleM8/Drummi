using UnityEditor;
using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

/// <summary>
/// TEST ONLY — Execute(): drops the ladder's minCardsCleared thresholds to
/// 0/1/2/4/6/8/10 so stage-ups arrive fast. Restore(): real values
/// 0/4/10/16/22/28/35. Neither saves the scene — the disk copy keeps the
/// real thresholds throughout.
/// </summary>
public static class SetStageTestThresholds
{
    public static void Execute() => Apply(new[] { 0, 1, 2, 4, 6, 8, 10 }, "TEST");

    public static void Restore() => Apply(new[] { 0, 4, 10, 16, 22, 28, 35 }, "REAL");

    private static void Apply(int[] values, string label)
    {
        var mode = Object.FindFirstObjectByType<FlashCardsModeController>(FindObjectsInactive.Include);
        if (mode == null) { Debug.Log("[StageTest] ABORT: FlashCardsModeController not found."); return; }

        var so = new SerializedObject(mode);
        var steps = so.FindProperty("escalationSteps");
        if (steps == null || steps.arraySize != values.Length)
        {
            Debug.Log($"[StageTest] ABORT: expected {values.Length} steps, found {(steps == null ? -1 : steps.arraySize)}.");
            return;
        }

        for (int i = 0; i < values.Length; i++)
            steps.GetArrayElementAtIndex(i).FindPropertyRelative("minCardsCleared").intValue = values[i];
        so.ApplyModifiedProperties();
        Debug.Log($"[StageTest] {label} thresholds applied: {string.Join("/", values)} (scene NOT saved).");
    }
}
