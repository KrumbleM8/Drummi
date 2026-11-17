using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using KrumbleHut.Drummi.Dungeons;
using KrumbleHut.Drummi.Flick;

public static class WireFlickEnemy
{
    public static string Execute()
    {
        var sweep = Object.FindObjectOfType<FlickTargetSweep>(true);
        if (sweep == null) return "ERROR: no FlickTargetSweep in scene";
        var visual = sweep.GetComponentInChildren<FlickTargetVisual>(true);
        if (visual == null) return "ERROR: no FlickTargetVisual child";

        var visualSr = visual.GetComponent<SpriteRenderer>();
        int baseOrder = visualSr != null ? visualSr.sortingOrder : 0;
        string sortingLayer = visualSr != null ? visualSr.sortingLayerName : "Default";

        Sprite square = AssetDatabase.GetBuiltinExtraResource<Sprite>("Sprites/Square.png");
        if (square == null) square = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        if (square == null) return "ERROR: no builtin square sprite found";

        // Idempotent: rebuild the bar if it already exists.
        Transform parent = sweep.transform;
        var existing = parent.Find("EnemyHealthBar");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        var barRoot = new GameObject("EnemyHealthBar").transform;
        barRoot.SetParent(parent, false);
        barRoot.localPosition = new Vector3(0f, 1.4f, 0f);

        var back = new GameObject("BarBack").transform;
        back.SetParent(barRoot, false);
        back.localScale = new Vector3(1.6f, 0.18f, 1f);
        var backSr = back.gameObject.AddComponent<SpriteRenderer>();
        backSr.sprite = square;
        backSr.color = new Color(0.12f, 0.12f, 0.12f, 0.9f);
        backSr.sortingLayerName = sortingLayer;
        backSr.sortingOrder = baseOrder + 1;

        // Left-edge pivot: scaling its X shrinks the fill from the right.
        var fillPivot = new GameObject("FillPivot").transform;
        fillPivot.SetParent(barRoot, false);
        fillPivot.localPosition = new Vector3(-0.8f, 0f, 0f);

        var fill = new GameObject("BarFill").transform;
        fill.SetParent(fillPivot, false);
        fill.localPosition = new Vector3(0.8f, 0f, 0f);
        fill.localScale = new Vector3(1.6f, 0.14f, 1f);
        var fillSr = fill.gameObject.AddComponent<SpriteRenderer>();
        fillSr.sprite = square;
        fillSr.color = new Color(0.9f, 0.2f, 0.2f, 1f);
        fillSr.sortingLayerName = sortingLayer;
        fillSr.sortingOrder = baseOrder + 2;

        var enemy = sweep.GetComponent<FlickEnemy>();
        if (enemy == null) enemy = sweep.gameObject.AddComponent<FlickEnemy>();
        var so = new SerializedObject(enemy);
        so.FindProperty("visual").objectReferenceValue = visual;
        so.FindProperty("barRoot").objectReferenceValue = barRoot;
        so.FindProperty("fillPivot").objectReferenceValue = fillPivot;
        so.ApplyModifiedPropertiesWithoutUndo();

        var mode = Object.FindObjectOfType<FlickModeController>(true);
        if (mode == null) return "ERROR: no FlickModeController in scene";
        var mso = new SerializedObject(mode);
        var prop = mso.FindProperty("flickEnemy");
        if (prop == null) return "ERROR: flickEnemy field not found on FlickModeController";
        prop.objectReferenceValue = enemy;
        mso.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkAllScenesDirty();
        bool saved = EditorSceneManager.SaveOpenScenes();
        string msg = $"OK — health bar built under '{parent.name}', FlickEnemy wired " +
                     $"(sorting layer '{sortingLayer}', base order {baseOrder}), scene saved={saved}";
        Debug.Log("[WireFlickEnemy] " + msg);
        return msg;
    }
}
