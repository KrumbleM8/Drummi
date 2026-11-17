using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using KrumbleHut.Drummi.Dungeons;

public static class SetupTutorialFloor
{
    public static string Execute()
    {
        var log = new System.Text.StringBuilder();

        // ── 1. TutorialRoom asset ────────────────────────────────────────────
        const string roomPath = "Assets/ScriptableObjects/Rooms/TutorialRoom.asset";
        var room = AssetDatabase.LoadAssetAtPath<RoomDefinition>(roomPath);
        if (room == null)
        {
            room = ScriptableObject.CreateInstance<RoomDefinition>();
            AssetDatabase.CreateAsset(room, roomPath);
            log.AppendLine($"Created {roomPath}");
        }
        else log.AppendLine($"Reusing {roomPath}");

        var roomSo = new SerializedObject(room);
        roomSo.FindProperty("roomName").stringValue = "Tutorial Room";
        roomSo.FindProperty("tier").enumValueIndex = 0;               // Standard
        roomSo.FindProperty("roomType").enumValueIndex = 0;           // CombatEncounter
        roomSo.FindProperty("inputLayout").enumValueIndex = 0;        // ThreePad
        var durations = roomSo.FindProperty("patternDurations");
        durations.arraySize = 1;
        durations.GetArrayElementAtIndex(0).floatValue = 1f;          // whole beats only
        roomSo.FindProperty("roomLengthBars").intValue = 4;           // short room
        roomSo.FindProperty("bgmTrackOverride").intValue = -1;
        roomSo.FindProperty("bpmOverride").intValue = -1;
        roomSo.ApplyModifiedPropertiesWithoutUndo();

        // ── 2. Floor0 asset (next to Floor1) ─────────────────────────────────
        string floor1Path = AssetDatabase.FindAssets("t:DungeonFloorDefinition")
            .Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == "Floor1");
        if (floor1Path == null) return "ERROR: Floor1 asset not found.";
        var floor1 = AssetDatabase.LoadAssetAtPath<DungeonFloorDefinition>(floor1Path);

        string floor0Path = Path.GetDirectoryName(floor1Path).Replace('\\', '/') + "/Floor0.asset";
        var floor0 = AssetDatabase.LoadAssetAtPath<DungeonFloorDefinition>(floor0Path);
        if (floor0 == null)
        {
            floor0 = ScriptableObject.CreateInstance<DungeonFloorDefinition>();
            AssetDatabase.CreateAsset(floor0, floor0Path);
            log.AppendLine($"Created {floor0Path}");
        }
        else log.AppendLine($"Reusing {floor0Path}");

        var floor1So = new SerializedObject(floor1);
        var bgSprite = floor1So.FindProperty("backgroundSprite").objectReferenceValue;

        var floor0So = new SerializedObject(floor0);
        floor0So.FindProperty("floorName").stringValue = "Tutorial";
        floor0So.FindProperty("floorIndex").intValue = 0;
        floor0So.FindProperty("isTutorialFloor").boolValue = true;
        floor0So.FindProperty("bgmTrackIndex").intValue = 1;          // Delve Bit_105 (slowest bar-exact clip)
        floor0So.FindProperty("bpm").intValue = 105;
        floor0So.FindProperty("backgroundSprite").objectReferenceValue = bgSprite;
        var rooms = floor0So.FindProperty("rooms");
        rooms.arraySize = 2;
        rooms.GetArrayElementAtIndex(0).objectReferenceValue = room;
        rooms.GetArrayElementAtIndex(1).objectReferenceValue = room;
        floor0So.ApplyModifiedPropertiesWithoutUndo();

        // ── 3. Insert Floor0 at index 0 of DungeonRunner.floors ─────────────
        var runner = Object.FindFirstObjectByType<DungeonRunner>(FindObjectsInactive.Include);
        if (runner == null) return "ERROR: DungeonRunner not found in scene.";

        var runnerSo = new SerializedObject(runner);
        var floorsProp = runnerSo.FindProperty("floors");
        bool alreadyInserted = floorsProp.arraySize > 0
            && floorsProp.GetArrayElementAtIndex(0).objectReferenceValue == floor0;
        if (!alreadyInserted)
        {
            floorsProp.InsertArrayElementAtIndex(0);
            floorsProp.GetArrayElementAtIndex(0).objectReferenceValue = floor0;
            log.AppendLine($"Inserted Floor0 at floors[0] (list size now {floorsProp.arraySize})");
        }
        else log.AppendLine("Floor0 already at floors[0]");
        runnerSo.ApplyModifiedPropertiesWithoutUndo();

        // ── 4. "Play tutorial" toggle next to StartButton (1) ────────────────
        var startButton = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(t => t.name == "StartButton (1)" && t.gameObject.scene.IsValid());
        if (startButton == null) return "ERROR: 'StartButton (1)' not found in scene.\n" + log;

        var page = startButton.parent;
        log.AppendLine($"Start page: {GetPath(page)}");

        var toggleTf = page.Find("PlayTutorialToggle");
        GameObject toggleGo;
        Toggle toggle;
        TextMeshProUGUI label;
        if (toggleTf == null)
        {
            toggleGo = new GameObject("PlayTutorialToggle", typeof(RectTransform), typeof(Toggle));
            toggleGo.transform.SetParent(page, false);

            // Position: same anchors as the start button, offset below it.
            var btnRect = startButton.GetComponent<RectTransform>();
            var rect = toggleGo.GetComponent<RectTransform>();
            rect.anchorMin = btnRect.anchorMin;
            rect.anchorMax = btnRect.anchorMax;
            rect.pivot     = btnRect.pivot;
            rect.anchoredPosition = btnRect.anchoredPosition - new Vector2(0f, btnRect.rect.height * 0.5f + 60f);
            rect.sizeDelta = new Vector2(420f, 70f);

            // Background box (left side).
            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(toggleGo.transform, false);
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0.5f);
            bgRect.anchorMax = new Vector2(0f, 0.5f);
            bgRect.pivot     = new Vector2(0f, 0.5f);
            bgRect.anchoredPosition = new Vector2(10f, 0f);
            bgRect.sizeDelta = new Vector2(54f, 54f);
            var bgImage = bgGo.GetComponent<Image>();
            bgImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            bgImage.type = Image.Type.Sliced;

            // Checkmark.
            var checkGo = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            checkGo.transform.SetParent(bgGo.transform, false);
            var checkRect = checkGo.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.5f, 0.5f);
            checkRect.anchorMax = new Vector2(0.5f, 0.5f);
            checkRect.anchoredPosition = Vector2.zero;
            checkRect.sizeDelta = new Vector2(44f, 44f);
            var checkImage = checkGo.GetComponent<Image>();
            checkImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");
            checkImage.color = new Color(0.2f, 0.2f, 0.2f);

            // Label.
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(toggleGo.transform, false);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(78f, 0f);
            labelRect.offsetMax = Vector2.zero;
            label = labelGo.GetComponent<TextMeshProUGUI>();
            label.text = "Play tutorial";
            label.fontSize = 38f;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.color = Color.white;
            label.raycastTarget = false;

            toggle = toggleGo.GetComponent<Toggle>();
            toggle.targetGraphic = bgImage;
            toggle.graphic = checkImage;
            toggle.isOn = false;
            log.AppendLine("Created PlayTutorialToggle");
        }
        else
        {
            toggleGo = toggleTf.gameObject;
            toggle = toggleGo.GetComponent<Toggle>();
            label = toggleGo.GetComponentInChildren<TextMeshProUGUI>(true);
            log.AppendLine("Reusing PlayTutorialToggle");
        }

        // ── 5. TutorialPromptController on the page, wired ───────────────────
        var prompt = page.GetComponent<TutorialPromptController>();
        if (prompt == null) prompt = page.gameObject.AddComponent<TutorialPromptController>();

        var promptSo = new SerializedObject(prompt);
        promptSo.FindProperty("dungeonRunner").objectReferenceValue = runner;
        promptSo.FindProperty("toggleRoot").objectReferenceValue = toggleGo;
        promptSo.FindProperty("replayTutorialToggle").objectReferenceValue = toggle;
        promptSo.ApplyModifiedPropertiesWithoutUndo();

        // Persistent listener: Toggle.onValueChanged → prompt.OnReplayToggleChanged(bool)
        bool hasListener = false;
        for (int i = 0; i < toggle.onValueChanged.GetPersistentEventCount(); i++)
        {
            if (toggle.onValueChanged.GetPersistentTarget(i) == prompt
                && toggle.onValueChanged.GetPersistentMethodName(i) == "OnReplayToggleChanged")
            { hasListener = true; break; }
        }
        if (!hasListener)
        {
            UnityEventTools.AddPersistentListener(toggle.onValueChanged, prompt.OnReplayToggleChanged);
            log.AppendLine("Added onValueChanged persistent listener");
        }

        EditorUtility.SetDirty(toggle);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(runner.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();

        log.AppendLine("Scene + assets saved.");
        return log.ToString();
    }

    private static string GetPath(Transform t)
    {
        return string.Join("/", t.GetComponentsInParent<Transform>(true).Reverse().Select(x => x.name));
    }
}
