using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

public static class MakeSnakePrimarySign
{
    // Edit-mode: swaps the mode definitions between the FRONT menu sign
    // (FlashCardsMenuController.nodes[0], focused on Start) and the sign
    // currently holding the Snake definition — Snake becomes the primary
    // gamemode. Sign transforms/spinner order untouched; labels update from
    // displayName at Awake. Saves the scene.
    public static void Execute()
    {
        if (Application.isPlaying)
        {
            Debug.Log("[MakeSnakePrimarySign] Exit play mode first.");
            return;
        }

        var menu = Object.FindFirstObjectByType<FlashCardsMenuController>(FindObjectsInactive.Include);
        if (menu == null) { Debug.Log("[MakeSnakePrimarySign] No FlashCardsMenuController found."); return; }

        var menuSo   = new SerializedObject(menu);
        var nodesProp = menuSo.FindProperty("nodes");
        if (nodesProp == null || nodesProp.arraySize == 0)
        { Debug.Log("[MakeSnakePrimarySign] nodes list missing/empty."); return; }

        var frontNode = nodesProp.GetArrayElementAtIndex(0).objectReferenceValue as MenuSignNode;
        if (frontNode == null) { Debug.Log("[MakeSnakePrimarySign] nodes[0] is null."); return; }

        MenuSignNode snakeNode = null;
        foreach (var node in Object.FindObjectsByType<MenuSignNode>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (node.Definition != null && node.Definition.displayName == "Snake") { snakeNode = node; break; }
        if (snakeNode == null) { Debug.Log("[MakeSnakePrimarySign] No sign holds the Snake definition."); return; }
        if (snakeNode == frontNode) { Debug.Log("[MakeSnakePrimarySign] Snake is already the front sign."); return; }

        var frontSo = new SerializedObject(frontNode);
        var snakeSo = new SerializedObject(snakeNode);
        var frontDefProp = frontSo.FindProperty("definition");
        var snakeDefProp = snakeSo.FindProperty("definition");
        Object frontDef = frontDefProp.objectReferenceValue;
        Object snakeDef = snakeDefProp.objectReferenceValue;

        frontDefProp.objectReferenceValue = snakeDef;
        snakeDefProp.objectReferenceValue = frontDef;
        frontSo.ApplyModifiedProperties();
        snakeSo.ApplyModifiedProperties();

        bool saved = EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[MakeSnakePrimarySign] Swapped: front sign '{frontNode.name}' now '{(snakeDef as FlashCardsModeDefinition)?.displayName}', " +
                  $"sign '{snakeNode.name}' now '{(frontDef as FlashCardsModeDefinition)?.displayName}'. Scene saved={saved}");
    }
}
