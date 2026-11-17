using UnityEngine;
using UnityEditor;
using KrumbleHut.Drummi.FlashCards;

// Temp verification helpers for Phase 2: force step 0's rows / dtChance.
public class TestRows
{
    public static void Execute() => Set(3, 0f, new Vector2Int(6, 9));    // 3 rows
    public static void Rows5()   => Set(5, 0f, new Vector2Int(10, 14));  // 5 rows
    public static void Combo()   => Set(3, 1f, new Vector2Int(6, 9));    // 3 rows + all DT
    public static void Restore() => Set(2, 0f, new Vector2Int(3, 4));    // shipping values

    private static void Set(int rows, float dt, Vector2Int range)
    {
        var mode = Object.FindFirstObjectByType<FlashCardsModeController>(FindObjectsInactive.Include);
        if (mode == null) { Debug.Log("[TestRows] FlashCardsModeController not found"); return; }
        var so    = new SerializedObject(mode);
        var step0 = so.FindProperty("escalationSteps").GetArrayElementAtIndex(0);
        step0.FindPropertyRelative("rows").intValue = rows;
        step0.FindPropertyRelative("doubleTimeChance").floatValue = dt;
        step0.FindPropertyRelative("noteRange").vector2IntValue = range;
        so.ApplyModifiedProperties();
        Debug.Log($"[TestRows] step 0: rows={rows} dtChance={dt:F2} noteRange={range}");
    }
}
