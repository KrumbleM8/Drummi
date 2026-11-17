using System.Text;
using KrumbleHut.Drummi.Dungeons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WireFlashCardScene
{
    public static string Execute()
    {
        var sb = new StringBuilder();
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Dungeon.unity")
        {
            scene = EditorSceneManager.OpenScene("Assets/Scenes/Dungeon.unity", OpenSceneMode.Single);
            sb.AppendLine("Opened Dungeon scene.");
        }
        else sb.AppendLine("Dungeon scene already active.");

        // Source of truth for shared refs: the existing C&R controller instance.
        var car = Object.FindFirstObjectByType<CallAndResponseController>(FindObjectsInactive.Include);
        if (car == null) return "FAIL: CallAndResponseController not found in Dungeon scene.";
        var carSo = new SerializedObject(car);
        var metronome  = carSo.FindProperty("metronome").objectReferenceValue;
        var modeCtrl   = carSo.FindProperty("modeController").objectReferenceValue;
        var reader     = carSo.FindProperty("inputReader").objectReferenceValue;
        var evaluator  = carSo.FindProperty("evaluator").objectReferenceValue;
        sb.AppendLine($"C&R refs: metronome={Name(metronome)} modeController={Name(modeCtrl)} " +
                      $"inputReader={Name(reader)} evaluator={Name(evaluator)}");
        if (metronome == null || modeCtrl == null || reader == null || evaluator == null)
            return sb + "FAIL: one or more C&R source refs are null — aborting, nothing written.";

        // Create (or reuse) the FlashCardController object as a sibling of the C&R object.
        var existing = Object.FindFirstObjectByType<FlashCardController>(FindObjectsInactive.Include);
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
            sb.AppendLine("Reusing existing FlashCardController object.");
        }
        else
        {
            go = new GameObject("FlashCardController");
            go.transform.SetParent(car.transform.parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Create FlashCardController");
            sb.AppendLine($"Created FlashCardController under parent '{Name(car.transform.parent)}'.");
        }

        var fc = go.GetComponent<FlashCardController>() ?? go.AddComponent<FlashCardController>();
        var fcSo = new SerializedObject(fc);
        fcSo.FindProperty("metronome").objectReferenceValue      = metronome;
        fcSo.FindProperty("modeController").objectReferenceValue = modeCtrl;
        fcSo.FindProperty("inputReader").objectReferenceValue    = reader;
        fcSo.FindProperty("evaluator").objectReferenceValue      = evaluator;
        fcSo.ApplyModifiedProperties();

        var tr = go.GetComponent<FlashCardTestRunner>() ?? go.AddComponent<FlashCardTestRunner>();
        var trSo = new SerializedObject(tr);
        trSo.FindProperty("controller").objectReferenceValue  = fc;
        trSo.FindProperty("inputReader").objectReferenceValue = reader;
        trSo.FindProperty("runScenarios").boolValue           = true;
        trSo.ApplyModifiedProperties();

        // Point RoomController at the new controller.
        var roomCtrl = Object.FindFirstObjectByType<RoomController>(FindObjectsInactive.Include);
        if (roomCtrl == null) return sb + "FAIL: RoomController not found.";
        var rcSo = new SerializedObject(roomCtrl);
        var prop = rcSo.FindProperty("flashCardController");
        if (prop == null) return sb + "FAIL: RoomController.flashCardController field not found.";
        prop.objectReferenceValue = fc;
        rcSo.ApplyModifiedProperties();
        sb.AppendLine("RoomController.flashCardController assigned.");

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveOpenScenes();
        sb.AppendLine($"SaveOpenScenes: {saved}");
        return sb.ToString();
    }

    private static string Name(Object o) => o != null ? o.name : "NULL";
}
