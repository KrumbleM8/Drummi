using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

public class WireRoomsClearedText
{
    public static void Execute()
    {
        // ── Locate the ResultsScreen (may be inactive, so search all objects) ──
        GameObject resultsGO = null;
        foreach (var ss in Resources.FindObjectsOfTypeAll<ScoreScreen>())
        {
            // Skip assets, only keep scene objects
            if (UnityEditor.AssetDatabase.Contains(ss.gameObject)) continue;
            resultsGO = ss.gameObject;
            break;
        }
        if (resultsGO == null)
        {
            Debug.LogError("[WireRoomsClearedText] Could not find a ScoreScreen component in the scene");
            return;
        }

        // ── Find or create the RoomsClearedText child ──────────────────────
        var existing = resultsGO.transform.Find("RoomsClearedText");
        GameObject go = existing != null ? existing.gameObject : null;

        if (go == null)
        {
            go = new GameObject("RoomsClearedText");
            go.transform.SetParent(resultsGO.transform, false);
        }

        // Remove legacy UI.Text if present
        var legacyText = go.GetComponent<Text>();
        if (legacyText != null)
            Object.DestroyImmediate(legacyText);

        // ── RectTransform — mirror PerfectHitsText layout ──────────────────
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(100f, -83f);   // 50px below PerfectHitsText
        rt.sizeDelta        = new Vector2(200f, 50f);

        // ── TextMeshProUGUI ────────────────────────────────────────────────
        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp == null) tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text              = "Rooms Cleared: 0";
        tmp.color             = Color.white;
        tmp.enableAutoSizing  = true;
        tmp.fontSizeMin       = 18f;
        tmp.fontSizeMax       = 72f;
        tmp.alignment         = TextAlignmentOptions.Center;

        // Match font asset from PerfectHitsText
        var fontAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (fontAsset != null)
            tmp.font = fontAsset;
        else
            Debug.LogWarning("[WireRoomsClearedText] LiberationSans SDF not found — assign font manually");

        // Match UI layer
        go.layer = LayerMask.NameToLayer("UI");

        // ── Wire to ScoreScreen.roomsClearedText ───────────────────────────
        var scoreScreen = resultsGO.GetComponent<ScoreScreen>();
        if (scoreScreen == null)
        {
            Debug.LogError("[WireRoomsClearedText] ScoreScreen component not found on ResultsScreen");
        }
        else
        {
            var so   = new SerializedObject(scoreScreen);
            var prop = so.FindProperty("roomsClearedText");
            if (prop != null)
            {
                prop.objectReferenceValue = tmp;
                so.ApplyModifiedProperties();
                Debug.Log("[WireRoomsClearedText] roomsClearedText wired successfully");
            }
            else
            {
                Debug.LogError("[WireRoomsClearedText] Property 'roomsClearedText' not found on ScoreScreen — did the field compile?");
            }
        }

        // ── Mark scene dirty and save ──────────────────────────────────────
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[WireRoomsClearedText] Done.");
    }
}
