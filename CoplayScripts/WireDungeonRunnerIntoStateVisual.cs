using UnityEditor;
using UnityEngine;
using KrumbleHut.Drummi.Dungeons;

/// <summary>
/// One-shot: wires DrumPadStateVisual.dungeonRunner in the open Dungeon scene
/// so the pads show full visuals while no run is active (main menu).
/// </summary>
public class WireDungeonRunnerIntoStateVisual
{
    public static string Execute()
    {
        var visual = Object.FindFirstObjectByType<DrumPadStateVisual>(FindObjectsInactive.Include);
        if (visual == null) return "ERROR: DrumPadStateVisual not found in scene.";

        var runner = Object.FindFirstObjectByType<DungeonRunner>(FindObjectsInactive.Include);
        if (runner == null) return "ERROR: DungeonRunner not found in scene.";

        var so = new SerializedObject(visual);
        var prop = so.FindProperty("dungeonRunner");
        if (prop == null) return "ERROR: dungeonRunner property not found (script not compiled?).";
        prop.objectReferenceValue = runner;
        so.ApplyModifiedPropertiesWithoutUndo();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(visual.gameObject.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        return $"OK: wired dungeonRunner={runner.name} on {visual.gameObject.name}, scene saved.";
    }
}
