using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using KrumbleHut.Drummi.FlashCards;

public class SetLadderV2
{
    // Shipping values for the 1-row-base + expansion ladder (matches code defaults).
    public static void Execute()
    {
        var mode = Object.FindFirstObjectByType<FlashCardsModeController>(FindObjectsInactive.Include);
        if (mode == null) { Debug.Log("[LadderV2] FlashCardsModeController not found"); return; }

        var so    = new SerializedObject(mode);
        var steps = so.FindProperty("escalationSteps");
        int[]        rows        = { 1, 1, 1, 1, 1 };
        int[]        expandRows  = { 2, 2, 2, 3, 5 };
        float[]      expandCh    = { 0.20f, 0.30f, 0.35f, 0.30f, 0.25f };
        float[]      dtCh        = { 0f, 0f, 0.15f, 0.25f, 0.35f };
        Vector2Int[] ranges      = { new(2, 3), new(2, 4), new(3, 4), new(3, 4), new(3, 4) };

        for (int i = 0; i < steps.arraySize; i++)
        {
            int k = Mathf.Min(i, rows.Length - 1);
            var el = steps.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("rows").intValue               = rows[k];
            el.FindPropertyRelative("expandRows").intValue         = expandRows[k];
            el.FindPropertyRelative("expandChance").floatValue     = expandCh[k];
            el.FindPropertyRelative("doubleTimeChance").floatValue = dtCh[k];
            el.FindPropertyRelative("noteRange").vector2IntValue   = ranges[k];
            Debug.Log($"[LadderV2] step {i}: rows={rows[k]} expandRows={expandRows[k]} expandCh={expandCh[k]:F2} dt={dtCh[k]:F2} notes={ranges[k]}");
        }
        so.FindProperty("expandEveryNCards").intValue = 4;
        so.FindProperty("expandTrigger").enumValueIndex = 0; // RandomChance
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(mode.gameObject.scene);
        Debug.Log("[LadderV2] Shipping ladder set — scene dirty.");
    }

    // Temp: step 0 expands often so verification sees 1-row and 2-row cards quickly.
    public static void TestBoost()
    {
        var mode = Object.FindFirstObjectByType<FlashCardsModeController>(FindObjectsInactive.Include);
        if (mode == null) { Debug.Log("[LadderV2] FlashCardsModeController not found"); return; }
        var so = new SerializedObject(mode);
        var el = so.FindProperty("escalationSteps").GetArrayElementAtIndex(0);
        el.FindPropertyRelative("expandChance").floatValue = 0.6f;
        so.ApplyModifiedProperties();
        Debug.Log("[LadderV2] step 0 expandChance=0.60 (TEMP)");
    }

    // Restore step 0 to the shipping chance.
    public static void TestRestore()
    {
        var mode = Object.FindFirstObjectByType<FlashCardsModeController>(FindObjectsInactive.Include);
        if (mode == null) { Debug.Log("[LadderV2] FlashCardsModeController not found"); return; }
        var so = new SerializedObject(mode);
        var el = so.FindProperty("escalationSteps").GetArrayElementAtIndex(0);
        el.FindPropertyRelative("expandChance").floatValue = 0.2f;
        so.ApplyModifiedProperties();
        Debug.Log("[LadderV2] step 0 expandChance=0.20 (shipping)");
    }
}
