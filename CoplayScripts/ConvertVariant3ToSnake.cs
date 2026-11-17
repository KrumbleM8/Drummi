using UnityEditor;
using UnityEngine;
using KrumbleHut.Drummi.Dungeons;
using KrumbleHut.Drummi.FlashCards;

public static class ConvertVariant3ToSnake
{
    public static void Execute()
    {
        const string oldPath = "Assets/ScriptableObjects/FlashCards/FlashCards Variant 3.asset";
        var def = AssetDatabase.LoadAssetAtPath<FlashCardsModeDefinition>(oldPath);
        if (def == null)
        {
            Debug.Log("[ConvertVariant3ToSnake] FAILED — asset not found at " + oldPath);
            return;
        }

        string renameError = AssetDatabase.RenameAsset(oldPath, "FlashCards Snake");
        if (!string.IsNullOrEmpty(renameError))
        {
            Debug.Log("[ConvertVariant3ToSnake] Rename error: " + renameError);
            return;
        }

        def.displayName     = "Snake";
        def.tempoMultiplier = 1f;
        def.highScoreKey    = "FlashCardsSnakeHS";
        def.layout          = FlashCardLayout.ColumnScroll;
        EditorUtility.SetDirty(def);
        AssetDatabase.SaveAssets();

        Debug.Log($"[ConvertVariant3ToSnake] Done — name={def.name}, displayName={def.displayName}, " +
                  $"hsKey={def.highScoreKey}, layout={def.layout}");
    }
}
