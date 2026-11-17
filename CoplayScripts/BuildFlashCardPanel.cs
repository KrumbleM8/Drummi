using System.Text;
using KrumbleHut.Drummi.Dungeons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the FlashCardPanel hierarchy under DungeonCanvas/GameplayUI (2x4 manual
/// layout, GardenPlot-style), adds FlashCardView to the FlashCardController object,
/// wires all refs + notation sprites, and saves the scene.
/// Idempotent: destroys a pre-existing FlashCardPanel before rebuilding.
/// </summary>
public static class BuildFlashCardPanel
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

        var gameplayUI = GameObject.Find("DungeonCanvas/GameplayUI");
        if (gameplayUI == null)
        {
            // GameplayUI may be inactive — find via the canvas.
            var canvas = GameObject.Find("DungeonCanvas");
            if (canvas != null)
            {
                var t = canvas.transform.Find("GameplayUI");
                if (t != null) gameplayUI = t.gameObject;
            }
        }
        if (gameplayUI == null) return "FAIL: DungeonCanvas/GameplayUI not found.";

        var controller = Object.FindFirstObjectByType<FlashCardController>(FindObjectsInactive.Include);
        if (controller == null) return "FAIL: FlashCardController not found in scene.";

        var noteSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Notation/QuarterNote.png");
        var restSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Notation/QuarterRest.png");
        if (noteSprite == null || restSprite == null) return "FAIL: notation sprites not found.";

        // ── Rebuild panel ────────────────────────────────────────────────────
        var old = gameplayUI.transform.Find("FlashCardPanel");
        if (old != null) { Object.DestroyImmediate(old.gameObject); sb.AppendLine("Old FlashCardPanel destroyed."); }

        var panel = CreateRect("FlashCardPanel", gameplayUI.transform);
        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 1f);
        panelRt.pivot = new Vector2(0.5f, 1f);
        panelRt.anchoredPosition = new Vector2(0f, -200f);
        panelRt.sizeDelta = new Vector2(660f, 380f);

        // Border (behind background, slightly larger — the "your turn" glow).
        var border = CreateRect("CardBorder", panel.transform);
        Stretch(border, -12f);
        var borderImg = border.AddComponent<Image>();
        borderImg.color = new Color(0f, 0f, 0f, 0.35f);
        borderImg.raycastTarget = false;

        var bg = CreateRect("CardBackground", panel.transform);
        Stretch(bg, 0f);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.10f, 0.08f, 0.14f, 0.92f);
        bgImg.raycastTarget = false;

        // ── Squares: 2 rows x 4 cols, reading order ─────────────────────────
        float[] colX = { -228f, -76f, 76f, 228f };
        float[] rowY = { -108f, -264f };
        var squareRoots = new GameObject[8];

        for (int i = 0; i < 8; i++)
        {
            int row = i / 4, col = i % 4;
            var sq = CreateRect($"Sq{i}", panel.transform);
            var rt = sq.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(colX[col], rowY[row]);
            rt.sizeDelta = new Vector2(140f, 140f);
            var backImg = sq.AddComponent<Image>();
            backImg.color = new Color(0.16f, 0.13f, 0.22f, 1f);
            backImg.raycastTarget = false;

            var glyph = CreateRect("Glyph", sq.transform);
            var grt = glyph.GetComponent<RectTransform>();
            grt.anchorMin = grt.anchorMax = new Vector2(0.5f, 0.5f);
            grt.pivot = new Vector2(0.5f, 0.5f);
            grt.anchoredPosition = Vector2.zero;
            grt.sizeDelta = new Vector2(96f, 96f);
            var glyphImg = glyph.AddComponent<Image>();
            glyphImg.enabled = false;
            glyphImg.raycastTarget = false;
            glyphImg.preserveAspect = true;

            var cursor = CreateRect("Cursor", sq.transform);
            Stretch(cursor, 0f);
            var cursorImg = cursor.AddComponent<Image>();
            cursorImg.enabled = false;
            cursorImg.raycastTarget = false;

            squareRoots[i] = sq;
        }

        panel.SetActive(false); // view activates it at reveal start

        // ── FlashCardView on the controller object ──────────────────────────
        var view = controller.gameObject.GetComponent<FlashCardView>();
        if (view == null) view = controller.gameObject.AddComponent<FlashCardView>();

        var so = new SerializedObject(view);
        so.FindProperty("controller").objectReferenceValue = controller;
        so.FindProperty("cardRoot").objectReferenceValue   = panelRt;
        so.FindProperty("cardBorder").objectReferenceValue = borderImg;
        so.FindProperty("noteSprite").objectReferenceValue = noteSprite;
        so.FindProperty("restSprite").objectReferenceValue = restSprite;
        var squaresProp = so.FindProperty("squares");
        squaresProp.arraySize = 8;
        for (int i = 0; i < 8; i++)
        {
            var el = squaresProp.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("root").objectReferenceValue   = squareRoots[i].GetComponent<RectTransform>();
            el.FindPropertyRelative("back").objectReferenceValue   = squareRoots[i].GetComponent<Image>();
            el.FindPropertyRelative("glyph").objectReferenceValue  = squareRoots[i].transform.Find("Glyph").GetComponent<Image>();
            el.FindPropertyRelative("cursor").objectReferenceValue = squareRoots[i].transform.Find("Cursor").GetComponent<Image>();
        }
        so.ApplyModifiedProperties();
        sb.AppendLine("FlashCardView added and wired (8 squares, border, sprites).");

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveOpenScenes();
        sb.AppendLine($"SaveOpenScenes: {saved}");
        return sb.ToString();
    }

    private static GameObject CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        return go;
    }

    private static void Stretch(GameObject go, float expand)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-expand, -expand);
        rt.offsetMax = new Vector2(expand, expand);
    }
}
