using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using UnityEngine.InputSystem.UI;

/// <summary>
/// One-shot editor script that finishes scaffolding the Dungeon scene:
///   - Creates a proper Slider (with full sub-hierarchy) inside Canvas/GameplayUI
///   - Creates the IndicatorParent RectTransform inside the slider
///   - Lays out InputPads (LeftPad, CenterPad, RightPad) across the screen
///   - Lays out GameplayUI and ResponseFill relative to the slider
///   - Positions the StartButton in the centre of the screen
///   - Creates SpawnIndicator and InputIndicator prefabs in Assets/Prefabs/Dungeon/
///   - Adds DrumPadTouch to a dedicated DrumPadTouch GameObject
/// Run via: DungeonSceneBuilder.Execute() in the editor.
/// </summary>
public class DungeonSceneBuilder
{
    public static void Execute()
    {
        var canvas = GameObject.Find("Canvas");
        if (canvas == null) { Debug.LogError("[DungeonSceneBuilder] Canvas not found!"); return; }

        var gameplayUI = GameObject.Find("Canvas/GameplayUI");
        if (gameplayUI == null) { Debug.LogError("[DungeonSceneBuilder] GameplayUI not found!"); return; }

        // ── 1. Stretch GameplayUI to fill canvas ──────────────────────────
        StretchToFill(gameplayUI.GetComponent<RectTransform>());
        var guiImage = gameplayUI.GetComponent<Image>();
        if (guiImage != null) guiImage.color = new Color(0, 0, 0, 0); // transparent

        // ── 2. Build Slider using Unity DefaultControls ───────────────────
        var sliderRoot = CreateSlider(gameplayUI);

        // ── 3. IndicatorParent — sits over the fill area of the slider ────
        var indicatorParent = new GameObject("IndicatorParent", typeof(RectTransform));
        indicatorParent.transform.SetParent(sliderRoot.transform, false);
        var ipRT = indicatorParent.GetComponent<RectTransform>();
        // Stretch to match the slider's fill area (inset handles)
        ipRT.anchorMin = new Vector2(0f, 0f);
        ipRT.anchorMax = new Vector2(1f, 1f);
        ipRT.offsetMin = new Vector2(10f, -15f);
        ipRT.offsetMax = new Vector2(-10f, 15f);

        // ── 4. ResponseFill — semi-transparent overlay on the slider ──────
        var responseFill = GameObject.Find("Canvas/GameplayUI/ResponseFill");
        if (responseFill != null)
        {
            var rfRT = responseFill.GetComponent<RectTransform>();
            rfRT.anchorMin = new Vector2(0f, 0.5f);
            rfRT.anchorMax = new Vector2(0f, 0.5f);
            rfRT.pivot     = new Vector2(0f, 0.5f);
            rfRT.anchoredPosition = new Vector2(10f, 0f);
            rfRT.sizeDelta = new Vector2(0f, 20f); // width driven by fillAmount * slider width

            var rfImg = responseFill.GetComponent<Image>();
            if (rfImg != null)
            {
                rfImg.type      = Image.Type.Filled;
                rfImg.fillMethod = Image.FillMethod.Horizontal;
                rfImg.fillAmount = 0f;
                rfImg.color      = new Color(1f, 1f, 1f, 0.35f);
            }
            responseFill.SetActive(false);

            // Position ResponseFill to match the slider
            CopySliderPosition(rfRT, sliderRoot.GetComponent<RectTransform>());
        }

        // ── 5. ScoreText ──────────────────────────────────────────────────
        var scoreText = GameObject.Find("Canvas/GameplayUI/ScoreText");
        if (scoreText != null)
        {
            var stRT = scoreText.GetComponent<RectTransform>();
            stRT.anchorMin        = new Vector2(0.5f, 1f);
            stRT.anchorMax        = new Vector2(0.5f, 1f);
            stRT.pivot            = new Vector2(0.5f, 1f);
            stRT.anchoredPosition = new Vector2(0f, -20f);
            stRT.sizeDelta        = new Vector2(200f, 60f);

            var tmp = scoreText.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text      = "0";
                tmp.fontSize  = 48f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color     = Color.white;
            }
        }

        // ── 6. InputPads — three equal columns covering the full screen ───
        LayoutInputPads();

        // ── 7. StartButton — centre of screen ─────────────────────────────
        LayoutStartButton();

        // ── 8. DrumPadTouch GameObject ────────────────────────────────────
        CreateDrumPadTouchGO();

        // ── 9. Indicator prefabs ──────────────────────────────────────────
        CreateIndicatorPrefabs();

        // ── 10. Save ──────────────────────────────────────────────────────
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[DungeonSceneBuilder] Scene scaffolding complete.");
    }

    // ── Slider ────────────────────────────────────────────────────────────

    private static GameObject CreateSlider(GameObject parent)
    {
        var resources = new DefaultControls.Resources();
        var sliderGO = DefaultControls.CreateSlider(resources);
        sliderGO.name = "BarSlider";
        sliderGO.transform.SetParent(parent.transform, false);

        var rt = sliderGO.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.05f, 0.4f);
        rt.anchorMax        = new Vector2(0.95f, 0.6f);
        rt.offsetMin        = Vector2.zero;
        rt.offsetMax        = Vector2.zero;

        var slider = sliderGO.GetComponent<Slider>();
        slider.minValue  = 0f;
        slider.maxValue  = 1f;
        slider.value     = 0f;
        slider.wholeNumbers = false;

        // Color the fill green
        var fillArea = sliderGO.transform.Find("Fill Area/Fill");
        if (fillArea != null)
        {
            var img = fillArea.GetComponent<Image>();
            if (img != null) img.color = new Color(0.2f, 0.8f, 0.2f, 0.6f);
        }

        // Make handle slightly larger and white
        var handle = sliderGO.transform.Find("Handle Slide Area/Handle");
        if (handle != null)
        {
            var hRT = handle.GetComponent<RectTransform>();
            hRT.sizeDelta = new Vector2(20f, 20f);
        }

        return sliderGO;
    }

    // ── Layout helpers ────────────────────────────────────────────────────

    private static void StretchToFill(RectTransform rt)
    {
        rt.anchorMin  = Vector2.zero;
        rt.anchorMax  = Vector2.one;
        rt.offsetMin  = Vector2.zero;
        rt.offsetMax  = Vector2.zero;
    }

    private static void CopySliderPosition(RectTransform target, RectTransform source)
    {
        target.anchorMin        = source.anchorMin;
        target.anchorMax        = source.anchorMax;
        target.offsetMin        = source.offsetMin;
        target.offsetMax        = source.offsetMax;
        target.pivot            = source.pivot;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta        = source.sizeDelta;
    }

    private static void LayoutInputPads()
    {
        // InputPads panel — stretch to fill canvas, fully transparent
        var inputPadsGO = GameObject.Find("Canvas/InputPads");
        if (inputPadsGO == null) return;

        StretchToFill(inputPadsGO.GetComponent<RectTransform>());
        var panelImg = inputPadsGO.GetComponent<Image>();
        if (panelImg != null) panelImg.color = new Color(0, 0, 0, 0);

        // Left pad — left third
        SetPadAnchors(inputPadsGO, "LeftPad",   0f,      0f,      1f / 3f, 1f, new Color(0.3f, 0.5f, 1f, 0.05f));
        // Center pad — middle third
        SetPadAnchors(inputPadsGO, "CenterPad", 1f / 3f, 0f,      2f / 3f, 1f, new Color(1f, 0.85f, 0.1f, 0.05f));
        // Right pad — right third
        SetPadAnchors(inputPadsGO, "RightPad",  2f / 3f, 0f,      1f,      1f, new Color(1f, 0.3f, 0.3f, 0.05f));
    }

    private static void SetPadAnchors(GameObject parent, string name,
        float xMin, float yMin, float xMax, float yMax, Color tint)
    {
        var t = parent.transform.Find(name);
        if (t == null) return;

        var rt        = t.GetComponent<RectTransform>();
        rt.anchorMin  = new Vector2(xMin, yMin);
        rt.anchorMax  = new Vector2(xMax, yMax);
        rt.offsetMin  = Vector2.zero;
        rt.offsetMax  = Vector2.zero;

        var img = t.GetComponent<Image>();
        if (img != null) img.color = tint;
    }

    private static void LayoutStartButton()
    {
        var btnGO = GameObject.Find("Canvas/StartButton");
        if (btnGO == null) return;

        var rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(200f, 60f);

        // Label
        var label = btnGO.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) label.text = "START";
    }

    // ── DrumPadTouch ──────────────────────────────────────────────────────

    private static void CreateDrumPadTouchGO()
    {
        if (GameObject.Find("DrumPadTouch") != null) return;

        var go = new GameObject("DrumPadTouch");
        go.AddComponent<DrumPadTouch>();
        Debug.Log("[DungeonSceneBuilder] DrumPadTouch GameObject created.");
    }

    // ── Indicator Prefabs ─────────────────────────────────────────────────

    private static void CreateIndicatorPrefabs()
    {
        System.IO.Directory.CreateDirectory(
            Application.dataPath + "/Prefabs/Dungeon");

        CreateIndicatorPrefab(
            "SpawnIndicator",
            new Color(0.2f, 0.8f, 0.2f),   // default green; DungeonVisualController overrides per type
            20f,
            "Assets/Prefabs/Dungeon/SpawnIndicator.prefab");

        CreateIndicatorPrefab(
            "InputIndicator",
            Color.white,
            18f,
            "Assets/Prefabs/Dungeon/InputIndicator.prefab",
            addEffect: true);

        AssetDatabase.Refresh();
        Debug.Log("[DungeonSceneBuilder] Indicator prefabs created in Assets/Prefabs/Dungeon/");
    }

    private static void CreateIndicatorPrefab(
        string name, Color color, float size, string path, bool addEffect = false)
    {
        var go  = new GameObject(name);
        var img = go.AddComponent<Image>();
        img.color = color;

        var rt       = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);

        if (addEffect)
            go.AddComponent<UIQuickSpawnEffect>();

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }

    // ── EventSystem fix ───────────────────────────────────────────────────

    /// <summary>
    /// Replaces the legacy StandaloneInputModule on the EventSystem with
    /// InputSystemUIInputModule so it works with the New Input System.
    /// </summary>
    public static void FixEventSystem()
    {
        var esGO = GameObject.Find("EventSystem");
        if (esGO == null)
        {
            Debug.LogError("[DungeonSceneBuilder] EventSystem not found.");
            return;
        }

        // Remove old module
        var legacy = esGO.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        if (legacy != null)
        {
            Object.DestroyImmediate(legacy);
            Debug.Log("[DungeonSceneBuilder] Removed StandaloneInputModule.");
        }

        // Add new module if not already present
        if (esGO.GetComponent<InputSystemUIInputModule>() == null)
        {
            esGO.AddComponent<InputSystemUIInputModule>();
            Debug.Log("[DungeonSceneBuilder] Added InputSystemUIInputModule.");
        }
        else
        {
            Debug.Log("[DungeonSceneBuilder] InputSystemUIInputModule already present.");
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }
}
