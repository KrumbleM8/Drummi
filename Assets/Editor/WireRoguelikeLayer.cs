using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One-shot editor script: wires the full Roguelike layer into the Dungeon scene.
///
/// What it does:
///   1. Creates "RoguelikeLayer" GameObject with DungeonRunner + RoomController.
///   2. Wires all serialized fields on both components.
///   3. Creates Canvas/GameplayUI/DoorsUI with LeftDoor + RightDoor buttons.
///   4. Wires door buttons to RoomController.ChooseDirection(0/1).
///   5. Rewires IntroMenu/Trigger EventTrigger: StartGame → StartRun.
///   6. Wires GameManager.dungeonRunner.
///   7. Adds DungeonGameOverHandler to GameManager if absent.
/// </summary>
public static class WireRoguelikeLayer
{
    [MenuItem("Drummi/Wire Roguelike Layer")]
    public static void Execute()
    {
        // ── 1. Find required existing objects ─────────────────────────────────
        var dungeonMode        = GameObject.Find("DungeonMode");
        var gameManagerGO      = GameObject.Find("GameManager");
        var menuCanvasManager  = GameObject.Find("MenuCanvas_Manager");
        var backgroundGO       = GameObject.Find("Background");
        var drumpPadTouch      = GameObject.Find("DrumPadTouch");

        // Screen transition lives under TransitionOverlayCanvas/TransitionScreen
        var transitionGO = GameObject.Find("TransitionScreen");

        // Canvas/GameplayUI
        var canvasGO = GameObject.Find("Canvas");
        Transform gameplayUI = canvasGO != null ? canvasGO.transform.Find("GameplayUI") : null;

        if (dungeonMode == null)   { Debug.LogError("[WireRoguelikeLayer] DungeonMode not found");      return; }
        if (gameManagerGO == null) { Debug.LogError("[WireRoguelikeLayer] GameManager not found");      return; }
        if (transitionGO == null)  { Debug.LogError("[WireRoguelikeLayer] TransitionScreen not found"); return; }
        if (gameplayUI == null)    { Debug.LogError("[WireRoguelikeLayer] Canvas/GameplayUI not found"); return; }

        // ── 2. Load floor ScriptableObjects ───────────────────────────────────
        var floor1 = AssetDatabase.LoadAssetAtPath<DungeonFloorDefinition>("Assets/ScriptableObjects/Floors/Floor1.asset");
        var floor2 = AssetDatabase.LoadAssetAtPath<DungeonFloorDefinition>("Assets/ScriptableObjects/Floors/Floor2.asset");
        var floor3 = AssetDatabase.LoadAssetAtPath<DungeonFloorDefinition>("Assets/ScriptableObjects/Floors/Floor3.asset");
        if (floor1 == null || floor2 == null || floor3 == null)
        {
            Debug.LogError("[WireRoguelikeLayer] One or more Floor assets not found");
            return;
        }

        // ── 3. Create or find RoguelikeLayer ──────────────────────────────────
        var roguelikeLayerGO = GameObject.Find("RoguelikeLayer");
        if (roguelikeLayerGO == null)
        {
            roguelikeLayerGO = new GameObject("RoguelikeLayer");
            Undo.RegisterCreatedObjectUndo(roguelikeLayerGO, "Create RoguelikeLayer");
        }

        // ── 4. Add / wire RoomController ──────────────────────────────────────
        var roomController = roguelikeLayerGO.GetComponent<RoomController>()
                           ?? Undo.AddComponent<RoomController>(roguelikeLayerGO);

        var rcSo = new SerializedObject(roomController);
        rcSo.FindProperty("modeController").objectReferenceValue =
            dungeonMode.GetComponent<DungeonModeController>();
        rcSo.FindProperty("screenTransition").objectReferenceValue =
            transitionGO.GetComponent<ScreenTransition>();
        rcSo.FindProperty("playerHealth").objectReferenceValue =
            dungeonMode.GetComponent<DungeonHealth>();
        if (backgroundGO != null)
            rcSo.FindProperty("backgroundRenderer").objectReferenceValue =
                backgroundGO.GetComponent<SpriteRenderer>();
        // doorsUI wired after creation in step 7
        rcSo.ApplyModifiedProperties();

        // ── 5. Add / wire DungeonRunner ───────────────────────────────────────
        var dungeonRunner = roguelikeLayerGO.GetComponent<DungeonRunner>()
                          ?? Undo.AddComponent<DungeonRunner>(roguelikeLayerGO);

        var drSo = new SerializedObject(dungeonRunner);
        drSo.FindProperty("roomController").objectReferenceValue = roomController;

        var floorsProp = drSo.FindProperty("floors");
        floorsProp.arraySize = 3;
        floorsProp.GetArrayElementAtIndex(0).objectReferenceValue = floor1;
        floorsProp.GetArrayElementAtIndex(1).objectReferenceValue = floor2;
        floorsProp.GetArrayElementAtIndex(2).objectReferenceValue = floor3;

        if (menuCanvasManager != null)
            drSo.FindProperty("uiMenuManager").objectReferenceValue =
                menuCanvasManager.GetComponent<UIMenuManager>();

        drSo.ApplyModifiedProperties();

        // ── 6. Wire GameManager.dungeonRunner ─────────────────────────────────
        var gmSo = new SerializedObject(gameManagerGO.GetComponent<GameManager>());
        gmSo.FindProperty("dungeonRunner").objectReferenceValue = dungeonRunner;
        gmSo.ApplyModifiedProperties();

        // ── 7. Add DungeonGameOverHandler to GameManager if absent ────────────
        if (gameManagerGO.GetComponent<DungeonGameOverHandler>() == null)
        {
            var handler = Undo.AddComponent<DungeonGameOverHandler>(gameManagerGO);
            var handlerSo = new SerializedObject(handler);

            handlerSo.FindProperty("health").objectReferenceValue =
                dungeonMode.GetComponent<DungeonHealth>();
            handlerSo.FindProperty("menuManager").objectReferenceValue =
                menuCanvasManager?.GetComponent<UIMenuManager>();
            handlerSo.FindProperty("runner").objectReferenceValue = dungeonRunner;
            handlerSo.ApplyModifiedProperties();
        }

        // ── 8. Create DoorsUI ─────────────────────────────────────────────────
        var doorsUITransform = gameplayUI.Find("DoorsUI");
        GameObject doorsUIGO;

        if (doorsUITransform == null)
        {
            doorsUIGO = new GameObject("DoorsUI");
            Undo.RegisterCreatedObjectUndo(doorsUIGO, "Create DoorsUI");
            doorsUIGO.transform.SetParent(gameplayUI, false);

            var rt = doorsUIGO.AddComponent<RectTransform>();
            rt.anchorMin        = Vector2.zero;
            rt.anchorMax        = Vector2.one;
            rt.offsetMin        = Vector2.zero;
            rt.offsetMax        = Vector2.zero;

            // Start hidden — only shown in DirectionChoice rooms
            doorsUIGO.SetActive(false);
        }
        else
        {
            doorsUIGO = doorsUITransform.gameObject;
        }

        // Create LeftDoor button
        CreateDoorButton(doorsUIGO.transform, "LeftDoor",
            new Vector2(-200f, 0f), new Vector2(250f, 120f),
            roomController, 0);

        // Create RightDoor button
        CreateDoorButton(doorsUIGO.transform, "RightDoor",
            new Vector2(200f, 0f), new Vector2(250f, 120f),
            roomController, 1);

        // Wire doorsUI back into RoomController
        rcSo.Update();
        rcSo.FindProperty("doorsUI").objectReferenceValue = doorsUIGO;
        rcSo.ApplyModifiedProperties();

        // ── 9. Rewire IntroMenu/Trigger: StartGame → StartRun ─────────────────
        var introMenuTriggerGO = GameObject.Find("Trigger");
        // Find by path more precisely
        if (menuCanvasManager != null)
        {
            var introMenu = menuCanvasManager.transform.Find("IntroMenu");
            if (introMenu != null)
                introMenuTriggerGO = introMenu.Find("Trigger")?.gameObject;
        }

        if (introMenuTriggerGO != null)
        {
            var eventTrigger = introMenuTriggerGO.GetComponent<EventTrigger>();
            if (eventTrigger != null)
            {
                var etSo = new SerializedObject(eventTrigger);
                var delegates = etSo.FindProperty("m_Delegates");

                for (int i = 0; i < delegates.arraySize; i++)
                {
                    var entry    = delegates.GetArrayElementAtIndex(i);
                    var callback = entry.FindPropertyRelative("callback");
                    var calls    = callback.FindPropertyRelative("m_PersistentCalls")
                                           .FindPropertyRelative("m_Calls");

                    for (int j = 0; j < calls.arraySize; j++)
                    {
                        var call       = calls.GetArrayElementAtIndex(j);
                        var methodName = call.FindPropertyRelative("m_MethodName");
                        var target     = call.FindPropertyRelative("m_Target");

                        if (methodName.stringValue == "StartGame")
                        {
                            methodName.stringValue = "StartRun";
                            target.objectReferenceValue = dungeonRunner;
                            Debug.Log("[WireRoguelikeLayer] Rewired Trigger: StartGame → StartRun");
                        }
                    }
                }
                etSo.ApplyModifiedProperties();
            }
        }
        else
        {
            Debug.LogWarning("[WireRoguelikeLayer] IntroMenu/Trigger not found — rewire manually.");
        }

        // ── 10. Save scene ────────────────────────────────────────────────────
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[WireRoguelikeLayer] ✓ Roguelike layer wired successfully.");
    }

    private static void CreateDoorButton(Transform parent, string name,
        Vector2 anchoredPos, Vector2 size, RoomController roomController, int directionIndex)
    {
        // Skip if already exists
        if (parent.Find(name) != null) return;

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = size;

        go.AddComponent<CanvasRenderer>();
        var img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.2f, 0.85f);

        var btn = go.AddComponent<Button>();

        // Label
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        var labelRt = labelGO.AddComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;
        var txt = labelGO.AddComponent<Text>();
        txt.text      = directionIndex == 0 ? "← Left" : "Right →";
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize  = 36;
        txt.color     = Color.white;

        // Wire onClick → RoomController.ChooseDirection(directionIndex)
        var btnSo = new SerializedObject(btn);
        var onClick = btnSo.FindProperty("m_OnClick");
        var calls   = onClick.FindPropertyRelative("m_PersistentCalls")
                             .FindPropertyRelative("m_Calls");

        calls.arraySize = 1;
        var call = calls.GetArrayElementAtIndex(0);
        call.FindPropertyRelative("m_Target").objectReferenceValue      = roomController;
        call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue =
            typeof(RoomController).AssemblyQualifiedName;
        call.FindPropertyRelative("m_MethodName").stringValue           = "ChooseDirection";
        call.FindPropertyRelative("m_Mode").intValue                    = 4; // PersistentListenerMode.Int
        call.FindPropertyRelative("m_CallState").intValue               = 2; // UnityEventCallState.RuntimeOnly

        var args = call.FindPropertyRelative("m_Arguments");
        args.FindPropertyRelative("m_IntArgument").intValue = directionIndex;

        btnSo.ApplyModifiedProperties();
    }
}
