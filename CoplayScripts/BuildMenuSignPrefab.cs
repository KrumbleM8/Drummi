using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KrumbleHut.Drummi.FlashCards;

/// <summary>
/// Builds Assets/Prefabs/MenuSign.prefab — one signpost for the FlashCardsMenu
/// signpost scene: wooden post + board sprites, an authored CameraAnchor child,
/// and a world-space canvas Button (sign face) with a TMP label.
/// Definition is left null on the prefab; assigned per scene instance.
/// </summary>
public static class BuildMenuSignPrefab
{
    public static string Execute()
    {
        var log = new List<string>();

        void SetRef(Object obj, string prop, Object value)
        {
            var so = new SerializedObject(obj);
            var p = so.FindProperty(prop);
            if (p == null) { log.Add($"MISSING PROP {prop} on {obj.GetType().Name}"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        Sprite rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        Sprite stick   = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Wooden Stick.png");
        if (stick == null) { stick = rounded; log.Add("Wooden Stick sprite not found — using builtin placeholder"); }

        // Same unlit sprite material the Flick scene build used — the 2D renderer's
        // default lit material renders black without a global Light2D.
        string matPath = AssetDatabase.GUIDToAssetPath("a97c105638bdf8b4a8650670310a4cd3");
        Material spriteMat = string.IsNullOrEmpty(matPath) ? null : AssetDatabase.LoadAssetAtPath<Material>(matPath);
        log.Add($"Sprite mat: {(spriteMat != null ? matPath : "default (guid unresolved)")}");

        // Scale a sprite to a target world size regardless of its PPU.
        Vector3 FitScale(Sprite s, float worldW, float worldH)
        {
            Vector2 size = s.bounds.size;
            return new Vector3(worldW / size.x, worldH / size.y, 1f);
        }

        var root = new GameObject("MenuSign");
        try
        {
            var node = root.AddComponent<MenuSignNode>();

            // ── Post ──────────────────────────────────────────────────────────
            var postGO = new GameObject("Post");
            postGO.transform.SetParent(root.transform, false);
            postGO.transform.localPosition = new Vector3(0f, 1.1f, 0.02f);
            postGO.transform.localScale = FitScale(stick, 0.35f, 2.2f);
            var postSr = postGO.AddComponent<SpriteRenderer>();
            postSr.sprite = stick;
            postSr.color = new Color(0.55f, 0.38f, 0.22f, 1f);
            postSr.sortingOrder = 10;
            if (spriteMat != null) postSr.sharedMaterial = spriteMat;

            // ── Board (visual frame behind the button) ────────────────────────
            var boardGO = new GameObject("Board");
            boardGO.transform.SetParent(root.transform, false);
            boardGO.transform.localPosition = new Vector3(0f, 2.3f, 0f);
            boardGO.transform.localScale = FitScale(rounded, 2.6f, 1.6f);
            var boardSr = boardGO.AddComponent<SpriteRenderer>();
            boardSr.sprite = rounded;
            boardSr.color = new Color(0.45f, 0.30f, 0.17f, 1f);
            boardSr.sortingOrder = 11;
            if (spriteMat != null) boardSr.sharedMaterial = spriteMat;

            // ── Camera anchor (authored per instance; this is the default) ────
            var anchorGO = new GameObject("CameraAnchor");
            anchorGO.transform.SetParent(root.transform, false);
            anchorGO.transform.localPosition = new Vector3(0f, 0.9f, -4.2f);
            anchorGO.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);

            // ── World-space canvas on the sign face ───────────────────────────
            var canvasGO = new GameObject("SignCanvas", typeof(RectTransform));
            var canvasRt = canvasGO.GetComponent<RectTransform>();
            canvasRt.SetParent(root.transform, false);
            canvasRt.localPosition = new Vector3(0f, 2.3f, -0.01f);
            canvasRt.sizeDelta = new Vector2(300f, 180f);
            canvasRt.localScale = Vector3.one * 0.008f; // 2.4 x 1.44 world units
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;   // worldCamera set per scene instance
            canvas.sortingOrder = 12;
            canvasGO.AddComponent<GraphicRaycaster>();

            // ── Button + label ────────────────────────────────────────────────
            var btnGO = new GameObject("SignButton", typeof(RectTransform));
            var btnRt = btnGO.GetComponent<RectTransform>();
            btnRt.SetParent(canvasRt, false);
            btnRt.anchorMin = Vector2.zero; btnRt.anchorMax = Vector2.one;
            btnRt.offsetMin = new Vector2(12f, 12f); btnRt.offsetMax = new Vector2(-12f, -12f);
            var btnImg = btnGO.AddComponent<Image>();
            btnImg.sprite = rounded;
            btnImg.type = Image.Type.Sliced;
            btnImg.color = new Color(0.93f, 0.85f, 0.66f, 1f);
            var btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = btnImg;

            var labelGO = new GameObject("Label", typeof(RectTransform));
            var labelRt = labelGO.GetComponent<RectTransform>();
            labelRt.SetParent(btnRt, false);
            labelRt.anchorMin = Vector2.zero; labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(8f, 8f); labelRt.offsetMax = new Vector2(-8f, -8f);
            var label = labelGO.AddComponent<TextMeshProUGUI>();
            label.text = "Sign";
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 20f;
            label.fontSizeMax = 56f;
            label.color = new Color(0.25f, 0.15f, 0.08f, 1f);
            label.raycastTarget = false;

            // ── Wire MenuSignNode ─────────────────────────────────────────────
            SetRef(node, "cameraAnchor", anchorGO.transform);
            SetRef(node, "signButton", btn);
            SetRef(node, "label", label);

            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/MenuSign.prefab");
            log.Add("Saved Assets/Prefabs/MenuSign.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        return string.Join("\n", log);
    }
}
