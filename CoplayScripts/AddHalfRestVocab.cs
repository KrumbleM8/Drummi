using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class AddHalfRestVocab
{
    public static void Execute()
    {
        MonoBehaviour mode = null;
        MonoBehaviour[] all = Object.FindObjectsOfType<MonoBehaviour>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].GetType().Name == "FlashCardsModeController")
            {
                mode = all[i];
                break;
            }
        }
        if (mode == null) { Debug.Log("[HalfRest] FlashCardsModeController not found in open scene."); return; }

        SerializedObject so = new SerializedObject(mode);
        SerializedProperty steps = so.FindProperty("escalationSteps");
        if (steps == null) { Debug.Log("[HalfRest] escalationSteps property not found."); return; }

        int changed = 0;
        for (int i = 0; i < steps.arraySize; i++)
        {
            SerializedProperty step = steps.GetArrayElementAtIndex(i);
            int min = step.FindPropertyRelative("minBlocksCleared").intValue;
            SerializedProperty vocab = step.FindPropertyRelative("vocab");
            if (min >= 64 && (vocab.intValue & 128) == 0)
            {
                vocab.intValue = vocab.intValue | 128; // FlashCardVocab.HalfRest
                changed = changed + 1;
            }
        }
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(mode);
        bool saved = EditorSceneManager.SaveOpenScenes();
        Debug.Log("[HalfRest] Updated " + changed + " ladder steps with HalfRest vocab. SaveOpenScenes=" + saved);
    }
}
