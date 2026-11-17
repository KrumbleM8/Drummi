using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// EDITOR ONLY - creates the TEMPORARY Phase 3 verification floor:
/// [Combat (4 bars), FlashCard A (4 bars = 2 cards, authored notes 3-5),
///  CR ({2,4}), FlashCard B (6 bars = 3 cards, defaults, BGM/BPM override 2@110)]
/// Assets go under Assets/ScriptableObjects/Phase3Test/ so DeletePhase3TestAssets
/// can remove the whole folder after verification.
/// </summary>
public static class CreatePhase3TestAssets
{
    public static string Execute()
    {
        string dir = "Assets/ScriptableObjects/Phase3Test";
        var sb = new StringBuilder();
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Phase3Test");

        // Combat room
        var combat = ScriptableObject.CreateInstance<RoomDefinition>();
        AssetDatabase.CreateAsset(combat, dir + "/Room_P3_Combat.asset");
        var so1 = new SerializedObject(combat);
        so1.FindProperty("roomName").stringValue        = "P3 Combat";
        so1.FindProperty("roomType").enumValueIndex     = 0;
        so1.FindProperty("inputLayout").enumValueIndex  = 0;
        so1.FindProperty("roomLengthBars").intValue     = 4;
        so1.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(combat);
        sb.AppendLine("Created Room_P3_Combat.");

        // FlashCard A
        var flashA = ScriptableObject.CreateInstance<RoomDefinition>();
        AssetDatabase.CreateAsset(flashA, dir + "/Room_P3_FlashA.asset");
        var so2 = new SerializedObject(flashA);
        so2.FindProperty("roomName").stringValue        = "P3 Flash A";
        so2.FindProperty("roomType").enumValueIndex     = 4;
        so2.FindProperty("inputLayout").enumValueIndex  = 1;
        so2.FindProperty("roomLengthBars").intValue     = 4;
        so2.FindProperty("flashCardNoteRange").vector2IntValue = new Vector2Int(3, 5);
        so2.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(flashA);
        sb.AppendLine("Created Room_P3_FlashA.");

        // CR room
        var cr = ScriptableObject.CreateInstance<RoomDefinition>();
        AssetDatabase.CreateAsset(cr, dir + "/Room_P3_CR.asset");
        var so3 = new SerializedObject(cr);
        so3.FindProperty("roomName").stringValue        = "P3 CallResponse";
        so3.FindProperty("roomType").enumValueIndex     = 2;
        so3.FindProperty("inputLayout").enumValueIndex  = 1;
        var lengths = so3.FindProperty("callResponsePhraseLengths");
        lengths.ClearArray();
        lengths.InsertArrayElementAtIndex(0);
        lengths.GetArrayElementAtIndex(0).intValue = 2;
        lengths.InsertArrayElementAtIndex(1);
        lengths.GetArrayElementAtIndex(1).intValue = 4;
        so3.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(cr);
        sb.AppendLine("Created Room_P3_CR.");

        // FlashCard B
        var flashB = ScriptableObject.CreateInstance<RoomDefinition>();
        AssetDatabase.CreateAsset(flashB, dir + "/Room_P3_FlashB.asset");
        var so4 = new SerializedObject(flashB);
        so4.FindProperty("roomName").stringValue        = "P3 Flash B";
        so4.FindProperty("roomType").enumValueIndex     = 4;
        so4.FindProperty("inputLayout").enumValueIndex  = 1;
        so4.FindProperty("roomLengthBars").intValue     = 6;
        so4.FindProperty("bgmTrackOverride").intValue   = 2;
        so4.FindProperty("bpmOverride").intValue        = 110;
        so4.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(flashB);
        sb.AppendLine("Created Room_P3_FlashB.");

        // Floor
        var floor = ScriptableObject.CreateInstance<DungeonFloorDefinition>();
        AssetDatabase.CreateAsset(floor, dir + "/Floor_Phase3Test.asset");
        var fso = new SerializedObject(floor);
        fso.FindProperty("floorName").stringValue   = "Phase 3 Test Floor";
        fso.FindProperty("floorIndex").intValue     = 98;
        fso.FindProperty("bgmTrackIndex").intValue  = 1;
        fso.FindProperty("bpm").intValue            = 105;
        var rooms = fso.FindProperty("rooms");
        rooms.ClearArray();
        rooms.InsertArrayElementAtIndex(0);
        rooms.GetArrayElementAtIndex(0).objectReferenceValue = combat;
        rooms.InsertArrayElementAtIndex(1);
        rooms.GetArrayElementAtIndex(1).objectReferenceValue = flashA;
        rooms.InsertArrayElementAtIndex(2);
        rooms.GetArrayElementAtIndex(2).objectReferenceValue = cr;
        rooms.InsertArrayElementAtIndex(3);
        rooms.GetArrayElementAtIndex(3).objectReferenceValue = flashB;
        fso.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(floor);
        sb.AppendLine("Created Floor_Phase3Test [Combat, FlashA, CR, FlashB].");

        AssetDatabase.SaveAssets();
        return sb.ToString();
    }
}
