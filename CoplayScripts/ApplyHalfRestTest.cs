using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KrumbleHut.Drummi.Dungeons;
using KrumbleHut.Drummi.FlashCards;

/// <summary>
/// TEST OVERRIDE for playtesting the HalfRest card from a fresh run: adds
/// HalfRest to EVERY ladder step's vocab and sets halfRestChance = 1 (every
/// card that rolls a Rest gets one). Run RevertHalfRestTest before committing.
/// </summary>
public static class ApplyHalfRestTest
{
    public static void Execute()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "FlashCardsMenu") { Debug.Log("[HalfRestTest] ABORT: scene is " + scene.name); return; }

        var mode = Object.FindFirstObjectByType<FlashCardsModeController>(FindObjectsInactive.Include);
        var card = Object.FindFirstObjectByType<FlashCardController>(FindObjectsInactive.Include);
        if (mode == null || card == null) { Debug.Log("[HalfRestTest] ABORT: controllers not found."); return; }

        var soMode = new SerializedObject(mode);
        var steps  = soMode.FindProperty("escalationSteps");
        int changed = 0;
        for (int i = 0; i < steps.arraySize; i++)
        {
            var vocab = steps.GetArrayElementAtIndex(i).FindPropertyRelative("vocab");
            if ((vocab.intValue & 128) == 0)
            {
                vocab.intValue = vocab.intValue | 128;
                changed = changed + 1;
            }
        }
        soMode.ApplyModifiedProperties();
        EditorUtility.SetDirty(mode);

        var soCard = new SerializedObject(card);
        soCard.FindProperty("halfRestChance").floatValue = 1f;
        soCard.ApplyModifiedProperties();
        EditorUtility.SetDirty(card);

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveOpenScenes();
        Debug.Log("[HalfRestTest] APPLIED: HalfRest added to " + changed +
                  " ladder steps, halfRestChance=1. Saved=" + saved + ". Revert before committing!");
    }
}
