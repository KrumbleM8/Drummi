using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

/// <summary>Sets PlanetSpinner.horizonFlipAngle to 20 in the open scene and saves.</summary>
public static class SetHorizonFlip
{
    public static void Execute()
    {
        GameObject pivot = GameObject.Find("PlanetPivot");
        if (pivot == null) { Debug.Log("FAILED: PlanetPivot not found"); return; }
        var so = new SerializedObject(pivot.GetComponent<PlanetSpinner>());
        so.FindProperty("horizonFlipAngle").floatValue = 20f;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkAllScenesDirty();
        bool saved = EditorSceneManager.SaveOpenScenes();
        Debug.Log("SetHorizonFlip done. Saved=" + saved);
    }
}
