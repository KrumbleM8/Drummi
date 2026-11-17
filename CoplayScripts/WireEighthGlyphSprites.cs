using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KrumbleHut.Drummi.Dungeons;

/// <summary>
/// Phase 5c scene wiring: assigns the three new eighth-glyph placeholder sprites
/// to the FlashCardView in the open Dungeon scene, then saves via
/// EditorSceneManager.SaveOpenScenes().
/// </summary>
public static class WireEighthGlyphSprites
{
    public static string Execute()
    {
        var view = Object.FindFirstObjectByType<FlashCardView>(FindObjectsInactive.Include);
        if (view == null) return "FAIL: no FlashCardView found in open scene";

        var pair = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Notation/EighthPair.png");
        var down = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Notation/DownbeatEighth.png");
        var off  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Notation/OffbeatEighth.png");
        if (pair == null || down == null || off == null)
            return $"FAIL: sprites missing (pair={pair != null}, down={down != null}, off={off != null})";

        var so = new SerializedObject(view);
        so.FindProperty("eighthPairSprite").objectReferenceValue     = pair;
        so.FindProperty("downbeatEighthSprite").objectReferenceValue = down;
        so.FindProperty("offbeatEighthSprite").objectReferenceValue  = off;
        so.ApplyModifiedProperties();

        EditorSceneManager.SaveOpenScenes();
        return $"OK: wired 3 sprites on '{view.gameObject.name}' and saved open scenes";
    }
}
