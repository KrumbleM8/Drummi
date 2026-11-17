using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

/// <summary>
/// Wires FlashCardsModeController.scorePage to FlashCardsCanvas/ScorePage in
/// the merged FlashCardsMenu scene (fix: results page lingering behind
/// GameplayUI on restarts).
/// </summary>
public static class WireScorePageRef
{
    public static void Execute()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/FlashCardsMenu.unity", OpenSceneMode.Single);

        GameObject canvas = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "FlashCardsCanvas");
        if (canvas == null) { Debug.LogError("[Wire] ABORT: FlashCardsCanvas not found."); return; }

        Transform scorePage = canvas.transform.Find("ScorePage");
        if (scorePage == null) { Debug.LogError("[Wire] ABORT: FlashCardsCanvas/ScorePage not found."); return; }

        var modeController = Object.FindObjectsByType<FlashCardsModeController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                   .FirstOrDefault(c => c.gameObject.scene == scene);
        if (modeController == null) { Debug.LogError("[Wire] ABORT: FlashCardsModeController not found."); return; }

        var so = new SerializedObject(modeController);
        SerializedProperty prop = so.FindProperty("scorePage");
        if (prop == null) { Debug.LogError("[Wire] ABORT: scorePage property not found — script not compiled?"); return; }
        prop.objectReferenceValue = scorePage.gameObject;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[Wire] DONE — scorePage wired to FlashCardsCanvas/ScorePage and scene saved.");
    }
}
