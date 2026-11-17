using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class FixGameplayUIRaycast
{
    public static void Execute()
    {
        // ── Dungeon.unity: GameplayUI root Image is a full-screen alpha-0 raycast
        // blocker that eats FlickInputReader's IsPointerOverGameObject filter.
        // Pads never noticed (DrumPadTouch bypasses the EventSystem). Interactive
        // children (pause button etc.) keep their own raycastTargets.
        GameObject gameplayUI = null;
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            var t = root.name == "DungeonCanvas" ? root.transform.Find("GameplayUI")
                                                 : root.transform.Find("DungeonCanvas/GameplayUI");
            if (t == null && root.name == "GameplayUI") t = root.transform;
            if (t != null) { gameplayUI = t.gameObject; break; }
        }
        if (gameplayUI == null) { Debug.LogError("[FixRaycast] GameplayUI not found — is Dungeon.unity open?"); return; }

        var img = gameplayUI.GetComponent<Image>();
        if (img == null) { Debug.LogError("[FixRaycast] No Image on GameplayUI"); return; }
        img.raycastTarget = false;
        EditorUtility.SetDirty(img);

        foreach (var g in gameplayUI.GetComponentsInChildren<Graphic>(true))
            if (g.raycastTarget)
                Debug.Log($"[FixRaycast] child raycastTarget kept: '{g.transform.parent.name}/{g.name}' ({g.GetType().Name})");

        // ── FlickRoom.asset: bake the new lead-in default explicitly.
        var room = AssetDatabase.LoadAssetAtPath<RoomDefinition>("Assets/ScriptableObjects/Rooms/FlickRoom.asset");
        var so = new SerializedObject(room);
        so.FindProperty("flickLeadInBars").intValue = 1;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();

        EditorSceneManager.MarkSceneDirty(gameplayUI.scene);
        bool saved = EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[FixRaycast] GameplayUI.raycastTarget=false, FlickRoom leadIn=1 — saved={saved}");
    }
}
