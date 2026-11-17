using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KrumbleHut.Drummi.Dungeons;
using KrumbleHut.Drummi.FlashCards;

/// <summary>
/// FULL DISABLE: removes HalfRest from EVERY ladder step and restores
/// halfRestChance = 0.35. The mechanic stays in code, inert until a step's
/// vocab re-adds FlashCardVocab.HalfRest.
/// </summary>
public static class RevertHalfRestTest
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
            if ((vocab.intValue & 128) != 0)
            {
                vocab.intValue = vocab.intValue - 128;
                changed = changed + 1;
            }
        }
        soMode.ApplyModifiedProperties();
        EditorUtility.SetDirty(mode);

        var soCard = new SerializedObject(card);
        soCard.FindProperty("halfRestChance").floatValue = 0.35f;
        soCard.ApplyModifiedProperties();
        EditorUtility.SetDirty(card);

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveOpenScenes();
        Debug.Log("[HalfRestTest] DISABLED: HalfRest removed from " + changed +
                  " ladder steps, halfRestChance=0.35. Saved=" + saved);
    }
}
