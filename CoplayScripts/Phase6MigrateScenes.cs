using System.Text;
using KrumbleHut.Drummi.Dungeons;
using KrumbleHut.Drummi.FlashCards;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// PHASE 6 migration:
///   - Both scenes (Dungeon, FlashCards): wire sixteenthQuadSprite + tripletSprite
///     on FlashCardView (the removed downbeatEighthSprite entry drops out of the
///     serialized data automatically on save).
///   - FlashCards scene: rewrite the serialized escalation ladder to the approved
///     Phase 6 steps (0:Q|R 3-4, 4:+E 3-5, 10:+O 4-6, 16:+Triplet 4-6,
///     22:+SixteenthQuad 4-6) — old ints 23/31 carried the retired bit 4.
///   - Deletes Assets/Images/Notation/DownbeatEighth.png.
/// Ends with the Dungeon scene open.
/// </summary>
public static class Phase6MigrateScenes
{
    public static string Execute()
    {
        var sb   = new StringBuilder();
        var quad = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Notation/SixteenthQuad.png");
        var trip = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Notation/Triplet.png");
        if (quad == null || trip == null) return "FAIL: new notation sprites missing — run GenerateNotationSprites first.";

        foreach (string path in new[] { "Assets/Scenes/FlashCards.unity", "Assets/Scenes/Dungeon.unity" })
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            var view = Object.FindFirstObjectByType<FlashCardView>(FindObjectsInactive.Include);
            if (view == null) { sb.AppendLine($"{path}: NO FlashCardView — skipped."); continue; }

            var so = new SerializedObject(view);
            so.FindProperty("sixteenthQuadSprite").objectReferenceValue = quad;
            so.FindProperty("tripletSprite").objectReferenceValue       = trip;
            so.ApplyModifiedPropertiesWithoutUndo();
            sb.AppendLine($"{path}: glyphs wired.");

            if (path.Contains("FlashCards"))
            {
                var mode = Object.FindFirstObjectByType<FlashCardsModeController>(FindObjectsInactive.Include);
                if (mode == null) { sb.AppendLine("  NO FlashCardsModeController!"); }
                else
                {
                    // (minCleared, vocab, min, max) — approved Phase 6 ladder.
                    var steps = new (int cleared, int vocab, int min, int max)[]
                    {
                        (0,  (int)(FlashCardVocab.Quarter | FlashCardVocab.Rest), 3, 4),
                        (4,  (int)(FlashCardVocab.Quarter | FlashCardVocab.EighthPair | FlashCardVocab.Rest), 3, 5),
                        (10, (int)(FlashCardVocab.Quarter | FlashCardVocab.EighthPair | FlashCardVocab.OffbeatEighth | FlashCardVocab.Rest), 4, 6),
                        (16, (int)(FlashCardVocab.Quarter | FlashCardVocab.EighthPair | FlashCardVocab.OffbeatEighth | FlashCardVocab.Triplet | FlashCardVocab.Rest), 4, 6),
                        (22, (int)(FlashCardVocab.Quarter | FlashCardVocab.EighthPair | FlashCardVocab.OffbeatEighth | FlashCardVocab.Triplet | FlashCardVocab.SixteenthQuad | FlashCardVocab.Rest), 4, 6),
                    };

                    var mso   = new SerializedObject(mode);
                    var array = mso.FindProperty("escalationSteps");
                    array.arraySize = steps.Length;
                    for (int i = 0; i < steps.Length; i++)
                    {
                        var el = array.GetArrayElementAtIndex(i);
                        el.FindPropertyRelative("minCardsCleared").intValue = steps[i].cleared;
                        el.FindPropertyRelative("vocab").intValue           = steps[i].vocab;
                        el.FindPropertyRelative("noteRange").vector2IntValue = new Vector2Int(steps[i].min, steps[i].max);
                    }
                    mso.ApplyModifiedPropertiesWithoutUndo();
                    sb.AppendLine($"  ladder rewritten: {steps.Length} steps, vocab ints " +
                                  $"{string.Join("/", System.Array.ConvertAll(steps, s => s.vocab.ToString()))}.");
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveOpenScenes();
            sb.AppendLine($"  saved: {saved}");
        }

        bool deleted = AssetDatabase.DeleteAsset("Assets/Images/Notation/DownbeatEighth.png");
        sb.AppendLine($"DownbeatEighth.png deleted: {deleted}");
        return sb.ToString();
    }
}
