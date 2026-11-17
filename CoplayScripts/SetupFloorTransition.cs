using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KrumbleHut.Drummi.Dungeons;

public static class SetupFloorTransition
{
    public static string Execute()
    {
        // 1. Find RoomController and read its screenTransition reference so the
        //    FloorTransitionController is guaranteed to use the same panel instance.
        var roomController = Object.FindFirstObjectByType<RoomController>(FindObjectsInactive.Include);
        if (roomController == null) return "ERROR: RoomController not found in scene.";

        var rcSo = new SerializedObject(roomController);
        var stProp = rcSo.FindProperty("screenTransition");
        var screenTransition = stProp != null ? stProp.objectReferenceValue as ScreenTransition : null;
        if (screenTransition == null) return "ERROR: RoomController.screenTransition is not assigned.";

        var panel = screenTransition.transform;

        // 2. Create (or reuse) the title card as a child of the wipe panel.
        var existing = panel.Find("FloorTitleCard");
        GameObject cardGo;
        if (existing != null)
        {
            cardGo = existing.gameObject;
        }
        else
        {
            cardGo = new GameObject("FloorTitleCard", typeof(RectTransform));
            cardGo.transform.SetParent(panel, false);
        }

        var rect = cardGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot     = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(800f, 220f);
        cardGo.transform.SetAsLastSibling(); // render above the wipe image

        var tmp = cardGo.GetComponent<TextMeshProUGUI>();
        if (tmp == null) tmp = cardGo.AddComponent<TextMeshProUGUI>();
        tmp.text = "Floor 1";
        tmp.fontSize = 90f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        cardGo.SetActive(false);

        // 3. Add FloorTransitionController to the DungeonRunner GameObject and wire it.
        var runner = Object.FindFirstObjectByType<DungeonRunner>(FindObjectsInactive.Include);
        if (runner == null) return "ERROR: DungeonRunner not found in scene.";

        var ftc = runner.GetComponent<FloorTransitionController>();
        if (ftc == null) ftc = runner.gameObject.AddComponent<FloorTransitionController>();

        var ftcSo = new SerializedObject(ftc);
        ftcSo.FindProperty("screenTransition").objectReferenceValue = screenTransition;
        ftcSo.FindProperty("titleCardRoot").objectReferenceValue = cardGo;
        ftcSo.FindProperty("titleText").objectReferenceValue = tmp;
        ftcSo.ApplyModifiedPropertiesWithoutUndo();

        // 4. Wire DungeonRunner.floorTransition.
        var runnerSo = new SerializedObject(runner);
        var ftProp = runnerSo.FindProperty("floorTransition");
        if (ftProp == null) return "ERROR: DungeonRunner.floorTransition property not found (script not compiled?).";
        ftProp.objectReferenceValue = ftc;
        runnerSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(runner.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();

        return $"OK: card '{GetPath(cardGo.transform)}' created under panel '{GetPath(panel)}'; " +
               $"FloorTransitionController on '{runner.gameObject.name}' wired; scene saved.";
    }

    private static string GetPath(Transform t)
    {
        return string.Join("/", t.GetComponentsInParent<Transform>(true).Reverse().Select(x => x.name));
    }
}
