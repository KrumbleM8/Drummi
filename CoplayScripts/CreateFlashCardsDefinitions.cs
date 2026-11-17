using UnityEditor;
using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

public static class CreateFlashCardsDefinitions
{
    public static string Execute()
    {
        const string folder = "Assets/ScriptableObjects/FlashCards";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "FlashCards");

        Create(folder, "FlashCards Normal",      "Flash Cards",  1f, "FlashCardsHS");
        Create(folder, "FlashCards Double Time", "Double Time",  2f, "FlashCardsDoubleHS");
        Create(folder, "FlashCards Variant 3",   "Variant 3",    1f, "FlashCardsVariant3HS");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return "Created 3 FlashCardsModeDefinition assets in " + folder;
    }

    private static void Create(string folder, string assetName, string display, float mult, string hsKey)
    {
        string path = folder + "/" + assetName + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<FlashCardsModeDefinition>(path);
        if (existing != null)
        {
            existing.displayName     = display;
            existing.tempoMultiplier = mult;
            existing.highScoreKey    = hsKey;
            EditorUtility.SetDirty(existing);
            return;
        }

        var def = ScriptableObject.CreateInstance<FlashCardsModeDefinition>();
        def.displayName     = display;
        def.tempoMultiplier = mult;
        def.highScoreKey    = hsKey;
        AssetDatabase.CreateAsset(def, path);
    }
}
