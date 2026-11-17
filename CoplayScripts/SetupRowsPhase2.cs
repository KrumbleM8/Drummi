using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using KrumbleHut.Drummi.Dungeons;
using KrumbleHut.Drummi.FlashCards;

public class SetupRowsPhase2
{
    // Run with the FlashCardsMenu scene open: creates the prefab from Sq0, wires
    // the view, deletes authored squares, sets ladder rows.
    public static void Execute()
    {
        var view = Object.FindFirstObjectByType<FlashCardView>(FindObjectsInactive.Include);
        if (view == null) { Debug.Log("[Rows2] FlashCardView not found"); return; }

        var so       = new SerializedObject(view);
        var cardRoot = so.FindProperty("cardRoot").objectReferenceValue as RectTransform;
        if (cardRoot == null) { Debug.Log("[Rows2] cardRoot not wired"); return; }

        // ── 1. Prefab from Sq0 ──
        var sq0 = cardRoot.Find("Sq0");
        if (sq0 == null) { Debug.Log("[Rows2] Sq0 not found under cardRoot"); return; }

        var clone = Object.Instantiate(sq0.gameObject);
        clone.name = "FlashCardSquare";
        var rt     = (RectTransform)clone.transform;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;

        var widget    = clone.AddComponent<FlashCardSquareWidget>();
        widget.root   = rt;
        widget.back   = clone.GetComponent<Image>();
        var glyphTr   = clone.transform.Find("Glyph");
        var cursorTr  = clone.transform.Find("Cursor");
        widget.glyph  = glyphTr  != null ? glyphTr.GetComponent<Image>()  : null;
        widget.cursor = cursorTr != null ? cursorTr.GetComponent<Image>() : null;
        if (widget.back == null || widget.glyph == null || widget.cursor == null)
        {
            Debug.Log($"[Rows2] ABORT — missing refs: back={widget.back} glyph={widget.glyph} cursor={widget.cursor}");
            Object.DestroyImmediate(clone);
            return;
        }

        var prefab = PrefabUtility.SaveAsPrefabAsset(clone, "Assets/Prefabs/FlashCardSquare.prefab");
        Object.DestroyImmediate(clone);
        if (prefab == null) { Debug.Log("[Rows2] Prefab save FAILED"); return; }
        Debug.Log("[Rows2] Saved Assets/Prefabs/FlashCardSquare.prefab");

        // ── 2. Wire view.squarePrefab ──
        so.FindProperty("squarePrefab").objectReferenceValue = prefab.GetComponent<FlashCardSquareWidget>();
        so.ApplyModifiedProperties();
        Debug.Log("[Rows2] Wired squarePrefab on FlashCardsMenu view");

        // ── 3. Delete authored squares ──
        DeleteAuthoredSquares(cardRoot);

        // ── 4. Ladder rows ──
        var mode = Object.FindFirstObjectByType<FlashCardsModeController>(FindObjectsInactive.Include);
        if (mode != null)
        {
            var mso   = new SerializedObject(mode);
            var steps = mso.FindProperty("escalationSteps");
            int[] rows = { 2, 2, 3, 3, 5 };
            Vector2Int[] ranges = { new(3, 4), new(3, 5), new(6, 9), new(7, 10), new(10, 14) };
            for (int i = 0; i < steps.arraySize; i++)
            {
                var el = steps.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("rows").intValue = i < rows.Length ? rows[i] : rows[rows.Length - 1];
                if (i < ranges.Length)
                    el.FindPropertyRelative("noteRange").vector2IntValue = ranges[i];
                Debug.Log($"[Rows2] step {i}: rows={(i < rows.Length ? rows[i] : rows[rows.Length - 1])} noteRange={(i < ranges.Length ? ranges[i] : default)}");
            }
            mso.ApplyModifiedProperties();
        }
        else Debug.Log("[Rows2] FlashCardsModeController not found — ladder rows NOT set");

        EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
        Debug.Log("[Rows2] FlashCardsMenu done — scene dirty.");
    }

    // Run AFTER Execute (prefab must exist), with Dungeon.unity open.
    public static void FixDungeon()
    {
        var view = Object.FindFirstObjectByType<FlashCardView>(FindObjectsInactive.Include);
        if (view == null) { Debug.Log("[Rows2] Dungeon FlashCardView not found"); return; }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/FlashCardSquare.prefab");
        if (prefab == null) { Debug.Log("[Rows2] Prefab missing — run Execute first"); return; }

        var so       = new SerializedObject(view);
        var cardRoot = so.FindProperty("cardRoot").objectReferenceValue as RectTransform;
        if (cardRoot == null) { Debug.Log("[Rows2] Dungeon cardRoot not wired"); return; }

        so.FindProperty("squarePrefab").objectReferenceValue = prefab.GetComponent<FlashCardSquareWidget>();
        so.ApplyModifiedProperties();
        DeleteAuthoredSquares(cardRoot);

        EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
        Debug.Log("[Rows2] Dungeon done — scene dirty.");
    }

    private static void DeleteAuthoredSquares(RectTransform cardRoot)
    {
        var doomed = new List<GameObject>();
        foreach (Transform child in cardRoot)
        {
            string n = child.name;
            if (n.Length >= 3 && n.StartsWith("Sq") && char.IsDigit(n[2]))
                doomed.Add(child.gameObject);
        }
        foreach (var go in doomed) Object.DestroyImmediate(go);
        Debug.Log($"[Rows2] Deleted {doomed.Count} authored squares");
    }
}
