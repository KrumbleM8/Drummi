using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using KrumbleHut.Drummi.FlashCards;

/// <summary>
/// One-shot merge: moves the FlashCards gameplay roots into FlashCardsMenu,
/// consolidates ScreenTransition/AudioListener/EventSystem, wires the
/// FlashCardsMenuController swap lists, and rewires the results Menu button.
/// The old FlashCards.unity is NOT saved (closed without save) so it stays
/// intact on disk for the Legacy move.
/// </summary>
public static class MergeFlashCardsIntoMenu
{
    public static void Execute()
    {
        var menuScene = EditorSceneManager.OpenScene("Assets/Scenes/FlashCardsMenu.unity", OpenSceneMode.Single);
        var gameScene = EditorSceneManager.OpenScene("Assets/Scenes/FlashCards.unity", OpenSceneMode.Additive);

        // ── 1. Move gameplay roots (leave EventSystem behind) ────────────────
        string[] rootsToMove =
        {
            "Main Camera", "GameClock", "TimingCoordinator", "Metronome",
            "AudioManager", "FlashCardsCanvas", "FlashCardSystems",
            "GameManager", "OverlayUtilCanvas"
        };

        var gameRoots = gameScene.GetRootGameObjects().ToDictionary(go => go.name, go => go);
        var moved = new Dictionary<string, GameObject>();
        foreach (string name in rootsToMove)
        {
            if (!gameRoots.TryGetValue(name, out GameObject go))
            {
                Debug.LogError($"[Merge] ABORT: root '{name}' not found in FlashCards scene. Roots present: {string.Join(", ", gameRoots.Keys)}");
                EditorSceneManager.CloseScene(gameScene, true);
                return;
            }
            SceneManager.MoveGameObjectToScene(go, menuScene);
            moved[name] = go;
        }
        Debug.Log($"[Merge] Moved {moved.Count} roots into FlashCardsMenu.");

        // Close old scene WITHOUT saving — asset on disk stays intact.
        EditorSceneManager.CloseScene(gameScene, true);

        // ── 2. Cameras + AudioListener ────────────────────────────────────────
        GameObject gameplayCamera = moved["Main Camera"];
        gameplayCamera.name = "GameplayCamera";

        GameObject menuCamera = menuScene.GetRootGameObjects().First(go => go.name == "Main Camera" && go != gameplayCamera);

        foreach (GameObject cam in new[] { gameplayCamera, menuCamera })
        {
            var listener = cam.GetComponent<AudioListener>();
            if (listener != null) Object.DestroyImmediate(listener);
        }
        GameObject gameManager = moved["GameManager"];
        if (gameManager.GetComponent<AudioListener>() == null)
            gameManager.AddComponent<AudioListener>();
        Debug.Log("[Merge] AudioListener consolidated onto GameManager; cameras cleaned.");

        // ── 3. Delete menu TransitionPanel, 4. hide BackButton ───────────────
        GameObject overlayCanvas = menuScene.GetRootGameObjects().FirstOrDefault(go => go.name == "OverlayCanvas");
        if (overlayCanvas == null) { Debug.LogError("[Merge] ABORT: OverlayCanvas not found."); return; }

        Transform transitionPanel = overlayCanvas.transform.Find("TransitionPanel");
        if (transitionPanel == null) { Debug.LogError("[Merge] ABORT: OverlayCanvas/TransitionPanel not found."); return; }
        Object.DestroyImmediate(transitionPanel.gameObject);

        Transform backButton = overlayCanvas.transform.Find("BackButton");
        if (backButton == null) { Debug.LogError("[Merge] ABORT: OverlayCanvas/BackButton not found."); return; }
        backButton.gameObject.SetActive(false);
        Debug.Log("[Merge] TransitionPanel deleted; BackButton deactivated.");

        // ── 5. Initial active states ──────────────────────────────────────────
        moved["FlashCardsCanvas"].SetActive(false);
        moved["FlashCardSystems"].SetActive(false);
        gameplayCamera.SetActive(false);

        // ── 6. Wire FlashCardsMenuController ─────────────────────────────────
        var menuController = Object.FindObjectsByType<FlashCardsMenuController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                   .FirstOrDefault(c => c.gameObject.scene == menuScene);
        if (menuController == null) { Debug.LogError("[Merge] ABORT: FlashCardsMenuController not found."); return; }

        var transitions = Object.FindObjectsByType<ScreenTransition>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                .Where(t => t.gameObject.scene == menuScene).ToArray();
        if (transitions.Length != 1)
        {
            Debug.LogError($"[Merge] ABORT: expected exactly 1 ScreenTransition after panel delete, found {transitions.Length}: {string.Join(", ", transitions.Select(t => t.gameObject.name))}");
            return;
        }
        ScreenTransition transitionScreen = transitions[0];

        var modeController = moved["FlashCardSystems"].GetComponent<FlashCardsModeController>();
        if (modeController == null) { Debug.LogError("[Merge] ABORT: FlashCardsModeController missing on FlashCardSystems."); return; }

        string[] menuObjectNames = { "Landscape", "MenuSign_Normal", "MenuSign_DoubleTime", "MenuSign_Variant3" };
        var menuObjects = new List<GameObject> { menuCamera };
        foreach (string name in menuObjectNames)
        {
            GameObject go = menuScene.GetRootGameObjects().FirstOrDefault(r => r.name == name);
            if (go == null) { Debug.LogError($"[Merge] ABORT: menu root '{name}' not found."); return; }
            menuObjects.Add(go);
        }
        menuObjects.Add(overlayCanvas);

        var gameplayObjects = new List<GameObject> { moved["FlashCardsCanvas"], moved["FlashCardSystems"], gameplayCamera };

        var so = new SerializedObject(menuController);
        so.FindProperty("screenTransitioner").objectReferenceValue = transitionScreen;
        so.FindProperty("modeController").objectReferenceValue = modeController;

        SerializedProperty menuList = so.FindProperty("menuObjects");
        menuList.arraySize = menuObjects.Count;
        for (int i = 0; i < menuObjects.Count; i++)
            menuList.GetArrayElementAtIndex(i).objectReferenceValue = menuObjects[i];

        SerializedProperty gameplayList = so.FindProperty("gameplayObjects");
        gameplayList.arraySize = gameplayObjects.Count;
        for (int i = 0; i < gameplayObjects.Count; i++)
            gameplayList.GetArrayElementAtIndex(i).objectReferenceValue = gameplayObjects[i];

        so.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log($"[Merge] MenuController wired: transition={transitionScreen.gameObject.name}, menuObjects={menuObjects.Count}, gameplayObjects={gameplayObjects.Count}.");

        // ── 7. Rewire results MenuButton ──────────────────────────────────────
        Transform menuButtonT = moved["FlashCardsCanvas"].transform.Find("ScorePage/MenuButton");
        if (menuButtonT == null) { Debug.LogError("[Merge] ABORT: FlashCardsCanvas/ScorePage/MenuButton not found."); return; }
        var menuButton = menuButtonT.GetComponent<Button>();

        int count = menuButton.onClick.GetPersistentEventCount();
        for (int i = 0; i < count; i++)
            Debug.Log($"[Merge] MenuButton listener {i}: {menuButton.onClick.GetPersistentTarget(i)?.GetType().Name}.{menuButton.onClick.GetPersistentMethodName(i)}");

        if (count != 2 || menuButton.onClick.GetPersistentMethodName(1) != "ShowPageLeft")
        {
            Debug.LogError("[Merge] ABORT: MenuButton listeners not as expected (want [ResetGameValues, ShowPageLeft]). Scene NOT saved.");
            return;
        }

        UnityEventTools.RemovePersistentListener(menuButton.onClick, 1);
        UnityEventTools.AddVoidPersistentListener(menuButton.onClick, new UnityAction(menuController.ReturnToMenuFromResults));
        Debug.Log("[Merge] MenuButton rewired: ShowPageLeft removed, ReturnToMenuFromResults added.");

        // ── 8. Save ───────────────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(menuScene);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[Merge] DONE — FlashCardsMenu saved with merged gameplay content.");
    }
}
