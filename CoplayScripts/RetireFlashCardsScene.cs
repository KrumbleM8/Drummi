using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Moves the old FlashCards.unity to Legacy and rebuilds EditorBuildSettings
/// with FlashCardsMenu at index 1 (Bootstrap loads buildIndex + 1 on startup).
/// </summary>
public static class RetireFlashCardsScene
{
    public static void Execute()
    {
        string error = AssetDatabase.MoveAsset("Assets/Scenes/FlashCards.unity", "Assets/Scenes/Legacy/FlashCards.unity");
        if (!string.IsNullOrEmpty(error))
        {
            Debug.LogError($"[Retire] ABORT: MoveAsset failed: {error}");
            return;
        }
        Debug.Log("[Retire] FlashCards.unity moved to Assets/Scenes/Legacy/.");

        string[] order =
        {
            "Assets/Scenes/Bootstrap.unity",
            "Assets/Scenes/FlashCardsMenu.unity",
            "Assets/Scenes/New_Home.unity",
            "Assets/Scenes/Bongo.unity",
            "Assets/Scenes/Dungeon.unity",
            "Assets/Scenes/Legacy/Arcade.unity",
            "Assets/Scenes/Garden.unity",
            "Assets/Scenes/Flick.unity",
        };

        foreach (string path in order)
        {
            if (AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets) == string.Empty)
            {
                Debug.LogError($"[Retire] ABORT: scene not found: {path}. Build settings unchanged.");
                return;
            }
        }

        EditorBuildSettings.scenes = order.Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
        AssetDatabase.SaveAssets();

        Debug.Log("[Retire] Build settings: " + string.Join(" | ",
            EditorBuildSettings.scenes.Select((s, i) => $"{i}:{s.path}")));
    }
}
