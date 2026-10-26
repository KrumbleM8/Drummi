using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KrumbleHut.Drummi.FlashCards;
using KrumbleHut.Drummi.Dungeons;

/// <summary>
/// EDIT-MODE, Home.unity open. Builds a "how to play" PreviewPanel under every
/// MenuSign_* listed in FlashCardsMenuController.nodes: a world-space Canvas +
/// CanvasGroup + MenuSignPreview with uGUI-only content driven by the
/// SignPreviewAnimation matching the sign's definition (Bongo / Garden scene
/// launch, Snake column layout, Classic grid — Double Time = Classic at speed
/// 2). Wires MenuSignNode.preview and saves. Idempotent: the panel transform
/// is re-applied, Content/Backdrop are rebuilt, wrong animation types removed.
/// Verify afterwards: grep -c "m_Name: PreviewPanel" Assets/Scenes/Home.unity == 5.
/// </summary>
public static class AddSignPreviews
{
    // Panel placement in the sign root's local space (board spans y 1.5–3.1).
    private const float   PanelBottomY = 3.42f;  // bottom edge of every panel (landscape centre = 4.3 as before)
    private const float   PanelZ     = -0.05f;
    private const float   PanelTilt  = 10f;      // lean back toward the pitched menu camera
    private const float   PanelScale = 0.008f;   // same as SignCanvas
    private static readonly Vector2 PanelSize         = new Vector2(320f, 220f);
    // 412:815 like the phone screen. The portrait replica LIES ON THE GROUND in front of
    // the signpost: its bottom edge rests on the globe surface PortraitBaseZ units toward
    // the camera (sign-local −z) and it leans back PortraitTilt° from vertical like a
    // tablet propped in the grass. Tuned for the FOCUSED sign (scaled 1.15 about the
    // sign base), where the base lands ~3 units out (screen bottom) and the top edge sits
    // just under the post base. ZTest-Always material keeps it visible over the globe.
    private static readonly Vector2 PortraitPanelSize = new Vector2(120f, 237.4f);
    private const float   PortraitTilt   = 62f;
    private const float   PortraitBaseZ  = -2.61f;   // unscaled; ×1.15 = 3.0 when focused
    private const float   GlobeRadius    = 10f;

    // Bongo replica: built in the game's own canvas units (800 wide, 1583 tall at 412:815,
    // 158.3 units per world unit, origin at screen centre) then scaled into the portrait panel.
    private const float BongoCanvasW = 800f, BongoCanvasH = 1583f;
    private static readonly Color BongoSky     = new Color(0.368f, 0.860f, 0.908f, 1f);
    private static readonly Color GardenGreen  = new Color(0.42f, 0.62f, 0.30f, 1f);    // under the grass texture
    private static readonly Color FlashCardsBg = new Color(0.96f, 0.94f, 0.90f, 1f);    // GameplayCamera clear colour (cream)

    // Palette (FlashCardSquare.prefab / DrumGridCell.cs).
    private static readonly Color CardBack   = new Color(0.90f, 0.87f, 0.82f, 1f);
    private static readonly Color GlyphInk   = new Color(0.15f, 0.13f, 0.11f, 1f);
    private static readonly Color CursorGold = new Color(0.93f, 0.79f, 0.44f, 1f);
    private static readonly Color Paper      = new Color(0.97f, 0.94f, 0.86f, 0.92f);
    private static readonly Color RingColor  = new Color(1f, 1f, 1f, 0f);
    private static readonly Color GridInactive = new Color(0.2f, 0.2f, 0.25f, 1f);

    private const string NotationDir = "Assets/Images/Notation/";

    private class Sprites
    {
        public Sprite Quarter, QuarterRest, EighthPair, Knob, Background;
        // Bongo replica
        public Sprite Circle, Tree, Gradient, XButton, BongoPair, Indicator, IndicatorOverlay, Star;
        public Sprite CustPlayNeutral, CustRed, CustGreen, CustListen, CustHappy;
        public Texture2D CloudTex;
        // Garden replica
        public Texture2D GrassTex;
    }

    private static Sprite LoadSub(string path, string name)
    {
        foreach (Object o in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
            if (o is Sprite sp && (name == null || sp.name == name)) return sp;
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private const string NoZTestMaterialPath = "Assets/Materials/UIPreviewZTestAlways.mat";

    private static Material EnsureNoZTestMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(NoZTestMaterialPath);
        if (mat != null) return mat;
        Shader sh = Shader.Find("UI/DefaultZTestAlways");
        if (sh == null) return null;
        mat = new Material(sh);
        AssetDatabase.CreateAsset(mat, NoZTestMaterialPath);
        AssetDatabase.SaveAssets();
        return mat;
    }

    private static Sprite LoadCircle()
    {
        var sp = AssetDatabase.LoadAssetAtPath<Sprite>("Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/v2/Circle.png");
        if (sp == null) sp = AssetDatabase.LoadAssetAtPath<Sprite>("Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/Circle.png");
        if (sp == null) sp = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        return sp;
    }

    public static string Execute()
    {
        var sb = new StringBuilder();
        if (EditorApplication.isPlaying) return "In play mode - abort.";
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Home") return "Active scene is " + scene.name + " - open Home first.";

        GameObject camGo  = GameObject.Find("Main Camera");
        GameObject flowGo = GameObject.Find("MenuFlow");
        if (camGo == null || flowGo == null)
            return "Missing: cam=" + (camGo != null) + " flow=" + (flowGo != null);
        Camera cam = camGo.GetComponent<Camera>();
        var flow   = flowGo.GetComponent<FlashCardsMenuController>();
        if (flow == null) return "MenuFlow has no FlashCardsMenuController.";

        var sprites = new Sprites
        {
            Quarter     = AssetDatabase.LoadAssetAtPath<Sprite>(NotationDir + "QuarterNote.png"),
            QuarterRest = AssetDatabase.LoadAssetAtPath<Sprite>(NotationDir + "QuarterRest.png"),
            EighthPair  = AssetDatabase.LoadAssetAtPath<Sprite>(NotationDir + "EighthPair.png"),
            Knob        = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            Background  = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            Circle      = LoadCircle(),
            Tree        = LoadSub("Assets/Images/PLACEHOLDER GENERATED/Tree.png", null),
            Gradient    = LoadSub("Assets/Images/GradientShadow.png", null),
            XButton     = LoadSub("Assets/Images/X button.png", null),
            BongoPair   = LoadSub("Assets/Images/Bongo.png", "Bongo_0"),
            Indicator   = LoadSub("Assets/Images/Indicator.png", null),
            IndicatorOverlay = LoadSub("Assets/Images/IndicatorOverlay.png", null),
            Star        = LoadSub("Assets/Images/Star2 1.png", null),
            CustPlayNeutral = LoadSub("Assets/Images/PlayNeutralCust.png", "PlayNeutralCust_0"),
            CustRed     = LoadSub("Assets/Images/RedCust.png", "RedCust_0"),
            CustGreen   = LoadSub("Assets/Images/GreenCust.png", "GreenCust_0"),
            CustListen  = LoadSub("Assets/Images/ListenCust.png", "ListenCust_0"),
            CustHappy   = LoadSub("Assets/Images/HappyCust.png", "HappyCust_0"),
            CloudTex    = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Images/CloudScroll.png"),
            GrassTex    = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Images/PLACEHOLDER GENERATED/ChatGPT Image Nov 14, 2025, 08_55_46 PM.png"),
        };
        sb.AppendLine("sprites: Q=" + (sprites.Quarter != null) + " R=" + (sprites.QuarterRest != null)
                      + " E=" + (sprites.EighthPair != null) + " knob=" + (sprites.Knob != null)
                      + " bg=" + (sprites.Background != null)
                      + " | bongo: circle=" + (sprites.Circle != null) + " tree=" + (sprites.Tree != null)
                      + " grad=" + (sprites.Gradient != null) + " x=" + (sprites.XButton != null)
                      + " pair=" + (sprites.BongoPair != null) + " ind=" + (sprites.Indicator != null)
                      + " ovl=" + (sprites.IndicatorOverlay != null) + " star=" + (sprites.Star != null)
                      + " cust=" + (sprites.CustPlayNeutral != null) + "/" + (sprites.CustRed != null) + "/"
                      + (sprites.CustGreen != null) + "/" + (sprites.CustListen != null) + "/" + (sprites.CustHappy != null)
                      + " cloud=" + (sprites.CloudTex != null));

        var fso = new SerializedObject(flow);
        var nodesProp = fso.FindProperty("nodes");
        var nodes = new System.Collections.Generic.List<MenuSignNode>();
        for (int i = 0; i < nodesProp.arraySize; i++)
        {
            var n = nodesProp.GetArrayElementAtIndex(i).objectReferenceValue as MenuSignNode;
            if (n != null) nodes.Add(n);
        }
        if (nodes.Count == 0) return "nodes list is empty.";

        foreach (var node in nodes)
            BuildPanel(node, cam, sprites, sb);

        // -- Save (prefab-instance pass) -------------------------------------------
        foreach (var node in nodes)
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(node);
            EditorUtility.SetDirty(node);
            Transform panel = node.transform.Find("PreviewPanel");
            if (panel == null) continue;
            foreach (var c in panel.GetComponentsInChildren<Component>(true))
                if (c != null) EditorUtility.SetDirty(c);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("SaveOpenScenes=" + saved);
        Debug.Log("[AddSignPreviews]\n" + sb);
        return sb.ToString();
    }

    // ── Panel ─────────────────────────────────────────────────────────────────

    private static void BuildPanel(MenuSignNode node, Camera cam, Sprites s, StringBuilder sb)
    {
        var nso = new SerializedObject(node);
        var def = nso.FindProperty("definition").objectReferenceValue as MenuSignDefinition;

        // Find-or-create the panel root.
        Transform panelT = node.transform.Find("PreviewPanel");
        GameObject panel;
        if (panelT == null)
        {
            panel = new GameObject("PreviewPanel", typeof(RectTransform));
            panel.transform.SetParent(node.transform, false);
        }
        else panel = panelT.gameObject;

        // Pick the animation by definition (decides the panel shape too).
        System.Type animType = typeof(ClassicPreviewAnimation);
        float speed = 1f;
        string why = "default";
        if (def is SceneLaunchDefinition sl)
        {
            if (sl.sceneName == "Bongo")  { animType = typeof(BongoPreviewAnimation);  why = "scene Bongo"; }
            if (sl.sceneName == "Garden") { animType = typeof(GardenPreviewAnimation); why = "scene Garden"; }
        }
        else if (def is FlashCardsModeDefinition fc)
        {
            if (fc.layout == FlashCardLayout.ColumnScroll) { animType = typeof(SnakePreviewAnimation); why = "ColumnScroll"; }
            else { speed = Mathf.Max(1f, fc.tempoMultiplier); why = "Grid x" + speed; }
        }

        // Every preview is a miniature of its real portrait gameplay screen (412×815 aspect)
        // lying on the ground in front of the post. (The landscape branch is kept for a
        // quick fallback but unused.)
        bool portrait = true;
        Vector2 panelSize = portrait ? PortraitPanelSize : PanelSize;
        float tilt = portrait ? PortraitTilt : PanelTilt;
        float halfH = panelSize.y * PanelScale * 0.5f;
        Vector3 panelPos;
        if (portrait)
        {
            // Bottom edge on the sphere surface at PortraitBaseZ; centre is half a height
            // up the leaning plane (lean back = top toward +z).
            float baseY = Mathf.Sqrt(GlobeRadius * GlobeRadius - PortraitBaseZ * PortraitBaseZ) - GlobeRadius;
            float rad = tilt * Mathf.Deg2Rad;
            panelPos = new Vector3(0f, baseY + halfH * Mathf.Cos(rad), PortraitBaseZ + halfH * Mathf.Sin(rad));
        }
        else panelPos = new Vector3(0f, PanelBottomY + halfH, PanelZ); // landscape card above the board

        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = panelSize;
        rt.localPosition = panelPos;
        rt.localRotation = Quaternion.Euler(tilt, 0f, 0f);
        rt.localScale    = Vector3.one * PanelScale;

        // Clip like a screen: the replica's scenery overhangs the frame on purpose.
        var mask = panel.GetComponent<RectMask2D>();
        if (portrait && mask == null) panel.AddComponent<RectMask2D>();
        if (!portrait && mask != null) Object.DestroyImmediate(mask);

        // Explicit null checks: Unity's fake-null components defeat '??'.
        var canvas = panel.GetComponent<Canvas>();
        if (canvas == null) canvas = panel.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.WorldSpace;
        canvas.worldCamera  = cam;
        canvas.sortingOrder = 11; // PlanetSpinner's rim cache re-sorts at runtime

        var raycaster = panel.GetComponent<GraphicRaycaster>();
        if (raycaster != null) Object.DestroyImmediate(raycaster); // must never catch pointer input

        var group = panel.GetComponent<CanvasGroup>();
        if (group == null) group = panel.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        var preview = panel.GetComponent<MenuSignPreview>();
        if (preview == null) preview = panel.AddComponent<MenuSignPreview>();

        // Rebuild content deterministically.
        Transform old = panel.transform.Find("Backdrop");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        old = panel.transform.Find("Content");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        // Backdrop: each replica's camera clear colour (paper card only for the landscape fallback).
        Color backdropColor = animType == typeof(BongoPreviewAnimation)  ? BongoSky
                            : animType == typeof(GardenPreviewAnimation) ? GardenGreen
                            : FlashCardsBg;
        Image backdrop = portrait
            ? MakeImage(panel.transform, "Backdrop", null, backdropColor, Vector2.zero, panelSize)
            : MakeImage(panel.transform, "Backdrop", s.Background, Paper, Vector2.zero, panelSize, sliced: true);
        var brt = backdrop.rectTransform;
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;
        backdrop.maskable = portrait;

        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(panel.transform, false);
        var crt = content.GetComponent<RectTransform>();
        crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one; crt.offsetMin = crt.offsetMax = Vector2.zero;

        foreach (var existing in panel.GetComponents<SignPreviewAnimation>())
            if (existing.GetType() != animType) Object.DestroyImmediate(existing);
        var anim = panel.GetComponent(animType) as SignPreviewAnimation;
        if (anim == null) anim = panel.AddComponent(animType) as SignPreviewAnimation;

        var aso = new SerializedObject(anim);
        aso.FindProperty("speed").floatValue = speed;
        if      (anim is ClassicPreviewAnimation) BuildClassic(content.transform, s, aso);
        else if (anim is BongoPreviewAnimation)   BuildBongo(content.transform, s, aso);
        else if (anim is SnakePreviewAnimation)   BuildSnake(content.transform, s, aso);
        else if (anim is GardenPreviewAnimation)  BuildGarden(content.transform, s, aso);
        aso.ApplyModifiedPropertiesWithoutUndo();

        if (portrait)
        {
            // Clip to the screen rect, and draw regardless of depth: the panel lies on the
            // globe and would otherwise be depth-clipped by the opaque sphere mesh.
            foreach (var g in panel.GetComponentsInChildren<MaskableGraphic>(true)) g.maskable = true;
            Material noZ = EnsureNoZTestMaterial();
            if (noZ != null)
            {
                foreach (var g in panel.GetComponentsInChildren<Graphic>(true))
                    if (!(g is TMP_Text)) g.material = noZ; // TMP keeps its own SDF material
            }
            else
            {
                Debug.LogWarning("[AddSignPreviews] UI/DefaultZTestAlways shader not found — panel may be depth-clipped by the globe. Refresh assets and re-run.");
            }
        }

        var pso = new SerializedObject(preview);
        pso.FindProperty("group").objectReferenceValue  = group;
        pso.FindProperty("canvas").objectReferenceValue = canvas;
        pso.FindProperty("previewAnimation").objectReferenceValue = anim;
        pso.ApplyModifiedPropertiesWithoutUndo();

        nso.FindProperty("preview").objectReferenceValue = preview;
        nso.ApplyModifiedPropertiesWithoutUndo();

        sb.AppendLine(node.name + ": def=" + (def != null ? def.name : "null") + " -> " + animType.Name + " (" + why + ") panel@" + rt.localPosition);
    }

    // ── Content builders ──────────────────────────────────────────────────────

    // ── FlashCards replicas (Home.unity GameplayUI, canvas 800×1583, origin centre) ──

    /// <summary>
    /// Shared FlashCards screen: cream background (backdrop), the Knob-ellipse
    /// drum pad, HealthBar, Streak/Score texts, the Pause "II" button, the X
    /// icon and a pool of 10 shard squares. Wires drum/shards on the animation.
    /// Returns the Screen rect; content must be added BEFORE the shards/HUD so
    /// they draw on top — so this builds the HUD last via the callback.
    /// </summary>
    private static RectTransform BuildFlashCardsScreen(Transform root, Sprites s, SerializedObject aso, System.Action<RectTransform> content)
    {
        RectTransform screen = MakeScreen(root);

        // Drum pad: 420×300 ellipse, bottom-centre at y_bl 40 → centre y_c −601.5.
        Image drum = MakeImage(screen, "Drum", s.Knob, new Color(0.85f, 0.68f, 0.42f, 1f), new Vector2(0f, -601.5f), new Vector2(420f, 300f));
        drum.rectTransform.pivot = new Vector2(0.5f, 0f);
        drum.rectTransform.anchoredPosition = new Vector2(0f, -751.5f); // pivot at the bottom edge like the game

        content(screen);

        // Shard pool (drawn above the cards).
        var shardRts = new Object[10]; var shardImgs = new Object[10];
        for (int i = 0; i < 10; i++)
        {
            Image sh = MakeImage(screen, "Shard" + i, null, new Color(1f, 0.85f, 0.3f, 0f), Vector2.zero, new Vector2(31f, 31f));
            shardRts[i] = sh.rectTransform; shardImgs[i] = sh;
        }

        // HUD.
        Image hbBg = MakeImage(screen, "HealthBar", null, new Color(0.08f, 0.08f, 0.10f, 0.65f), new Vector2(-190f, 717.5f), new Vector2(300f, 28f));
        Image hbFill = MakeImage(hbBg.transform, "Fill", null, new Color(0.85f, 0.25f, 0.25f, 1f), Vector2.zero, new Vector2(294f * 0.8f, 22f));
        hbFill.rectTransform.anchorMin = hbFill.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        hbFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        hbFill.rectTransform.anchoredPosition = new Vector2(3f, 0f);
        Image streak = MakeImage(screen, "StreakText", null, new Color(0f, 0f, 0f, 0f), new Vector2(-240f, 681.5f), new Vector2(320f, 50f));
        var streakLabel = MakeLabel(streak.transform, "Label", "Streak: 0  Best: 0", 24f, new Color(0.15f, 0.13f, 0.11f, 1f));
        streakLabel.alignment = TextAlignmentOptions.Left;
        Image score = MakeImage(screen, "ScoreText", null, new Color(0f, 0f, 0f, 0f), new Vector2(300f, 741.5f), new Vector2(220f, 70f));
        var scoreLabel = MakeLabel(score.transform, "Label", "0", 44f, new Color(0.15f, 0.13f, 0.11f, 1f));
        scoreLabel.alignment = TextAlignmentOptions.Right;
        Image pause = MakeImage(screen, "PauseButton", s.Background, new Color(0.93f, 0.79f, 0.44f, 1f), new Vector2(0f, 736.5f), new Vector2(70f, 70f), sliced: true);
        MakeLabel(pause.transform, "Label", "II", 34f, Color.black);
        MakeImage(screen, "SettingsIcon", s.XButton, Color.white, new Vector2(329f, 719.5f), new Vector2(110f, 110f));

        aso.FindProperty("drum").objectReferenceValue = drum.rectTransform;
        SetArray(aso.FindProperty("shards"), shardRts);
        SetArray(aso.FindProperty("shardImages"), shardImgs);
        return screen;
    }

    /// <summary>One 140 px FlashCard square: plain back, 96 px glyph, full-size cursor overlay.</summary>
    private static Image MakeSquare(Transform parent, string name, Sprite glyph, bool rest, Vector2 pos, float size, out Image glyphImg, out Image cursorImg)
    {
        Image back = MakeImage(parent, name, null, rest ? new Color(0.62f, 0.60f, 0.68f) : new Color(0.96f, 0.93f, 0.82f), pos, new Vector2(size, size));
        glyphImg = MakeImage(back.transform, "Glyph", glyph, rest ? new Color(0.15f, 0.15f, 0.2f, 0.8f) : Color.black, Vector2.zero, new Vector2(size * 96f / 140f, size * 96f / 140f));
        glyphImg.preserveAspect = true;
        var grt = glyphImg.rectTransform; // keep the glyph 69 % of the square as the square resizes
        grt.anchorMin = new Vector2(0.155f, 0.155f); grt.anchorMax = new Vector2(0.845f, 0.845f); grt.offsetMin = grt.offsetMax = Vector2.zero;
        cursorImg = MakeImage(back.transform, "Cursor", null, new Color(1f, 1f, 1f, 0f), Vector2.zero, Vector2.zero);
        var crt = cursorImg.rectTransform;
        crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one; crt.offsetMin = crt.offsetMax = Vector2.zero;
        return back;
    }

    private static void BuildClassic(Transform root, Sprites s, SerializedObject aso)
    {
        bool[] rests = { false, true, false, false, true, false, true, true };   // Q R Q Q / R Q R R
        var squares = new Object[8]; var backs = new Object[8]; var glyphs = new Object[8]; var cursors = new Object[8];
        BuildFlashCardsScreen(root, s, aso, screen =>
        {
            // Card panel: 660×380 centred at y_bl 1223 → y_c 431.5.
            MakeImage(screen, "FlashCardPanel", null, new Color(1f, 0.99f, 0.96f, 0.97f), new Vector2(0f, 431.5f), new Vector2(660f, 380f));
            float[] colX = { -228f, -76f, 76f, 228f };
            float[] rowY = { 513.5f, 357.5f };
            for (int i = 0; i < 8; i++)
            {
                int c = i % 4, r = i / 4;
                Image back = MakeSquare(screen, "Sq" + i, rests[i] ? s.QuarterRest : s.Quarter, rests[i], new Vector2(colX[c], rowY[r]), 140f, out Image g, out Image cur);
                squares[i] = back.rectTransform; backs[i] = back; glyphs[i] = g; cursors[i] = cur;
            }
        });
        SetArray(aso.FindProperty("squares"), squares);
        SetArray(aso.FindProperty("backs"), backs);
        SetArray(aso.FindProperty("glyphs"), glyphs);
        SetArray(aso.FindProperty("cursors"), cursors);
        var rp = aso.FindProperty("rests");
        rp.arraySize = rests.Length;
        for (int i = 0; i < rests.Length; i++) rp.GetArrayElementAtIndex(i).boolValue = rests[i];
    }

    /// <summary>
    /// Miniature of the real Bongo gameplay screen. Everything is authored in
    /// the game's canvas units (800 wide, origin at screen centre, 158.3 units
    /// per world unit at 412:815) on a "Screen" rect that is scaled to fit the
    /// portrait panel; positions come from Bongo.unity (see the scene-graph
    /// notes in the session) and the gameplay screenshots.
    /// </summary>
    private static void BuildBongo(Transform root, Sprites s, SerializedObject aso)
    {
        RectTransform screen = MakeScreen(root);

        // ── Sky layer: sun + scrolling clouds (world sprites at z behind everything) ──
        MakeImage(screen, "Sun", s.Circle, new Color(1f, 0.90f, 0f, 1f), new Vector2(-394f, 739f), new Vector2(317f, 317f));
        var cloudGo = new GameObject("Clouds", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        cloudGo.transform.SetParent(screen, false);
        var cloudRt = cloudGo.GetComponent<RectTransform>();
        cloudRt.anchorMin = cloudRt.anchorMax = cloudRt.pivot = new Vector2(0.5f, 0.5f);
        cloudRt.anchoredPosition = new Vector2(0f, 624f);
        cloudRt.sizeDelta = new Vector2(2112f, 456f);
        var clouds = cloudGo.GetComponent<RawImage>();
        clouds.texture = s.CloudTex;
        clouds.uvRect = new Rect(0f, 0f, 0.926f, 1f);
        clouds.raycastTarget = false;

        // ── Hills (Circle-sprite ellipses, back → front) ──────────────────────
        Color backGreen  = new Color(0.088f, 0.496f, 0f, 1f);
        Color midGreen   = new Color(0.134f, 0.753f, 0f, 1f);
        Color frontGreen = new Color(0.408f, 0.867f, 0.306f, 1f);
        var hills = new Object[5];
        hills[0] = MakeImage(screen, "BackHill",   s.Circle, backGreen,  new Vector2(30f, -600f),   new Vector2(2600f, 1400f)).rectTransform;
        hills[1] = MakeImage(screen, "MidHillA",   s.Circle, midGreen,   new Vector2(-200f, -800f), new Vector2(1800f, 1290f)).rectTransform;
        hills[2] = MakeImage(screen, "MidHillB",   s.Circle, midGreen,   new Vector2(330f, -700f),  new Vector2(1500f, 1290f)).rectTransform;
        hills[3] = MakeImage(screen, "FrontHillA", s.Circle, frontGreen, new Vector2(-60f, -1000f), new Vector2(1900f, 1440f)).rectTransform;
        hills[4] = MakeImage(screen, "FrontHillB", s.Circle, frontGreen, new Vector2(250f, -1050f), new Vector2(1700f, 1300f)).rectTransform;
        float[] hillBounce = { 0.326f, -0.18f, -0.18f, -0.05f, -0.05f };

        // Trees (eyeballed from gameplay captures).
        Image treeL = MakeImage(screen, "TreeL", s.Tree, Color.white, new Vector2(-328f, -257f), new Vector2(260f, 404f));
        treeL.preserveAspect = true;
        Image treeR = MakeImage(screen, "TreeR", s.Tree, Color.white, new Vector2(279f, -11f), new Vector2(245f, 290f));
        treeR.preserveAspect = true;

        // ── Custard + ground shadow (world sprites) ───────────────────────────
        MakeImage(screen, "CustardShadow", s.Circle, new Color(0f, 0f, 0f, 0.686f), new Vector2(0f, 176f), new Vector2(480f, 157f));
        float upp = 0.4215f * 158.3f / 100f; // canvas units per sprite pixel
        Sprite cust0 = s.CustPlayNeutral;
        Vector2 custSize = cust0 != null ? cust0.rect.size * upp : new Vector2(506f, 594f);
        Image custard = MakeImage(screen, "Custard", cust0, Color.white, new Vector2(0f, 423f), custSize);

        // ── Call flashes (BeatFlashLayer, 270 px, overlay child) ──────────────
        var callRts = new Object[3]; var callImgs = new Object[3]; var callOvls = new Object[3];
        for (int i = 0; i < 3; i++)
        {
            Image f = MakeImage(screen, "CallFlash" + i, s.Indicator, new Color(1f, 1f, 1f, 0f), new Vector2(i % 2 == 0 ? -135f : 135f, -93f), new Vector2(270f, 270f));
            Image o = MakeImage(f.transform, "Overlay", s.IndicatorOverlay, new Color(1f, 1f, 1f, 0f), Vector2.zero, new Vector2(270f, 270f));
            callRts[i] = f.rectTransform; callImgs[i] = f; callOvls[i] = o;
        }

        // ── Bottom UI: gradient band, drums (shadow + image around the BongoAnimator pivot) ──
        Image grad = MakeImage(screen, "ShadowBand", s.Gradient, Color.white, Vector2.zero, Vector2.zero);
        var grt = grad.rectTransform;
        grt.anchorMin = new Vector2(0f, 0f); grt.anchorMax = new Vector2(1f, 0f); grt.pivot = new Vector2(0.5f, 0f);
        grt.anchoredPosition = Vector2.zero; grt.sizeDelta = new Vector2(0f, 447f);

        var bongos = new GameObject("Bongos", typeof(RectTransform)).GetComponent<RectTransform>();
        bongos.SetParent(screen, false);
        bongos.anchorMin = bongos.anchorMax = bongos.pivot = new Vector2(0.5f, 0.5f);
        bongos.anchoredPosition = new Vector2(6f, -703f);   // Bongos rect pivot (406, 88 from bottom-left)
        bongos.sizeDelta = Vector2.zero;
        var bongoVisual = new GameObject("Visual", typeof(RectTransform)).GetComponent<RectTransform>();
        bongoVisual.SetParent(bongos, false);
        bongoVisual.anchorMin = bongoVisual.anchorMax = bongoVisual.pivot = new Vector2(0.5f, 0.5f);
        bongoVisual.anchoredPosition = new Vector2(22.8f, 91f);
        bongoVisual.sizeDelta = Vector2.zero;
        Image bongoShadow = MakeImage(bongoVisual, "BongoBackShadow", s.BongoPair, new Color(0f, 0f, 0f, 0.5f), Vector2.zero, new Vector2(719f, 404f));
        Image bongoImage  = MakeImage(bongoVisual, "BongoImage",      s.BongoPair, new Color(1f, 1f, 1f, 0.5f), Vector2.zero, new Vector2(719f, 404f));

        // Player flashes: same 270 px disc + overlay at the call positions (FlashPlayer = call style) + perfect star 140 above.
        var playRts = new Object[3]; var playImgs = new Object[3]; var playOvls = new Object[3];
        for (int i = 0; i < 3; i++)
        {
            Image f = MakeImage(screen, "PlayerFlash" + i, s.Indicator, new Color(1f, 1f, 1f, 0f), new Vector2(i % 2 == 0 ? -135f : 135f, -93f), new Vector2(270f, 270f));
            Image o = MakeImage(f.transform, "Overlay", s.IndicatorOverlay, new Color(1f, 1f, 1f, 0f), Vector2.zero, new Vector2(270f, 270f));
            playRts[i] = f.rectTransform; playImgs[i] = f; playOvls[i] = o;
        }
        Image star = MakeImage(screen, "PerfectStar", s.Star, new Color(1f, 0.784f, 0f, 0f), new Vector2(-135f, 47f), new Vector2(100f, 100f));

        // Settings X stays visible top-right during play.
        Image x = MakeImage(screen, "SettingsIcon", s.XButton, Color.white, new Vector2(329f, 719f), new Vector2(110f, 110f));

        aso.FindProperty("custard").objectReferenceValue      = custard.rectTransform;
        aso.FindProperty("custardImage").objectReferenceValue = custard;
        aso.FindProperty("spritePlayNeutral").objectReferenceValue = s.CustPlayNeutral;
        aso.FindProperty("spriteRed").objectReferenceValue    = s.CustRed;
        aso.FindProperty("spriteGreen").objectReferenceValue  = s.CustGreen;
        aso.FindProperty("spriteListen").objectReferenceValue = s.CustListen;
        aso.FindProperty("spriteHappy").objectReferenceValue  = s.CustHappy;
        aso.FindProperty("custardUnitsPerPixel").floatValue   = upp;
        aso.FindProperty("bongos").objectReferenceValue       = bongos;
        aso.FindProperty("bongoVisual").objectReferenceValue  = bongoVisual;
        aso.FindProperty("bongoImage").objectReferenceValue   = bongoImage;
        aso.FindProperty("bongoShadow").objectReferenceValue  = bongoShadow;
        SetArray(aso.FindProperty("hills"), hills);
        var hb = aso.FindProperty("hillBounce");
        hb.arraySize = hillBounce.Length;
        for (int i = 0; i < hillBounce.Length; i++) hb.GetArrayElementAtIndex(i).floatValue = hillBounce[i];
        aso.FindProperty("clouds").objectReferenceValue       = clouds;
        SetArray(aso.FindProperty("callFlashes"), callRts);
        SetArray(aso.FindProperty("callFlashImages"), callImgs);
        SetArray(aso.FindProperty("callOverlayImages"), callOvls);
        SetArray(aso.FindProperty("playerFlashes"), playRts);
        SetArray(aso.FindProperty("playerFlashImages"), playImgs);
        SetArray(aso.FindProperty("playerOverlayImages"), playOvls);
        aso.FindProperty("star").objectReferenceValue         = star.rectTransform;
        aso.FindProperty("starImage").objectReferenceValue    = star;
    }

    private static void BuildSnake(Transform root, Sprites s, SerializedObject aso)
    {
        Sprite[] pattern = { s.Quarter, s.QuarterRest, s.Quarter, s.Quarter, s.QuarterRest, s.Quarter, s.QuarterRest, s.Quarter };
        var squares = new Object[8]; var backs = new Object[8]; var glyphs = new Object[8];
        BuildFlashCardsScreen(root, s, aso, screen =>
        {
            // Lane: x ±120, from y_bl 560 up past the top (overflow 200) → centre y_c 380, height 1223.
            MakeImage(screen, "Lane", null, new Color(1f, 0.99f, 0.96f, 0.97f), new Vector2(0f, 380f), new Vector2(240f, 1223f));
            for (int j = 0; j < 8; j++)
            {
                bool rest = pattern[j] != null && pattern[j].name.Contains("Rest");
                Image back = MakeSquare(screen, "Block" + j, pattern[j], rest, new Vector2(0f, -91.5f + 150f * j), j == 0 ? 175f : 119f, out Image g, out Image cur);
                Object.DestroyImmediate(cur.gameObject); // no cursor in the stream
                squares[j] = back.rectTransform; backs[j] = back; glyphs[j] = g;
            }
        });
        SetArray(aso.FindProperty("squares"), squares);
        SetArray(aso.FindProperty("backs"), backs);
        SetArray(aso.FindProperty("glyphs"), glyphs);
        SetArray(aso.FindProperty("pattern"), pattern.Cast<Object>().ToArray());
        aso.FindProperty("hitZoneY").floatValue  = -91.5f;
        aso.FindProperty("pitch").floatValue     = 150f;
        aso.FindProperty("trailSize").floatValue = 119f;
        aso.FindProperty("hitSize").floatValue   = 175f;
    }

    /// <summary>
    /// Miniature of Garden.unity (numbers from the scene: ortho size 5, canvas
    /// 800 wide → 158.25 canvas units per world unit, origin at screen centre).
    /// </summary>
    private static void BuildGarden(Transform root, Sprites s, SerializedObject aso)
    {
        RectTransform screen = MakeScreen(root);

        // Grass quad: 17×17 world, texture tiled 3×3 → one tile ≈ 897 canvas units.
        var grassGo = new GameObject("Grass", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        grassGo.transform.SetParent(screen, false);
        var grt = grassGo.GetComponent<RectTransform>();
        grt.anchorMin = Vector2.zero; grt.anchorMax = Vector2.one; grt.offsetMin = grt.offsetMax = Vector2.zero;
        var grass = grassGo.GetComponent<RawImage>();
        grass.texture = s.GrassTex;
        grass.uvRect = new Rect(0.05f, 0.30f, BongoCanvasW / 897f, BongoCanvasH / 897f);
        grass.raycastTarget = false;

        Color plotBrown = new Color(0.461f, 0.321f, 0.236f, 1f);
        Color stripBlue = new Color(0f, 0.511f, 1f, 1f);
        // Second plot: mostly off-screen right, only a sliver shows (inert in the game).
        MakeImage(screen, "Plot1", null, plotBrown, new Vector2(632.9f, 474.8f), new Vector2(566.9f, 566.9f));
        MakeImage(screen, "Plot1Strip", null, stripBlue, new Vector2(632.9f, 479.3f), new Vector2(566.9f, 90.8f));
        MakeImage(screen, "Plot", null, plotBrown, new Vector2(-110.8f, 474.8f), new Vector2(566.9f, 566.9f));
        MakeImage(screen, "Strip", null, stripBlue, new Vector2(-110.8f, 479.3f), new Vector2(566.9f, 90.8f));

        float[] colX = { -310.3f, -251.9f, -195.8f, -138.6f, -82.4f, -23.5f, 33.2f, 89.9f };
        float[] rowY = { 701.0f, 589.3f, 380.6f, 268.9f };
        // 0 none, 1 kick, 2 snare, 3 hihat, 4 clap — row-major 4×8 (the reference-capture pattern).
        int[] pattern =
        {
            1, 0, 1, 0, 1, 0, 1, 0,
            0, 0, 2, 0, 0, 0, 2, 0,
            3, 3, 3, 3, 3, 3, 3, 3,
            0, 0, 0, 4, 0, 0, 0, 4,
        };
        var cells = new Object[32];
        for (int i = 0; i < 32; i++)
        {
            int c = i % 8, r = i / 8;
            Image cell = MakeImage(screen, "Cell" + r + "_" + c, s.Circle, GridInactive, new Vector2(colX[c], rowY[r]), new Vector2(42.5f, 42.5f));
            cells[i] = cell;
        }

        // Play "Button" (white UISprite, 24 pt label) and the X icon.
        Image button = MakeImage(screen, "Button", s.Background, Color.white, new Vector2(238f, -660f), new Vector2(188.4f, 179.1f), sliced: true);
        MakeLabel(button.transform, "Label", "Button", 24f, new Color(0.196f, 0.196f, 0.196f, 1f));
        MakeImage(screen, "SettingsIcon", s.XButton, Color.white, new Vector2(329f, 719f), new Vector2(110f, 110f));

        SetArray(aso.FindProperty("cells"), cells);
        var pp = aso.FindProperty("basePattern");
        pp.arraySize = 32;
        for (int i = 0; i < 32; i++) pp.GetArrayElementAtIndex(i).intValue = pattern[i];
        aso.FindProperty("toggleCell").intValue = 3; // row 0, column 3 (empty in the base pattern)
    }

    /// <summary>The game's 800-wide portrait canvas, scaled into the panel.</summary>
    private static RectTransform MakeScreen(Transform root)
    {
        float k = PortraitPanelSize.x / BongoCanvasW;
        var screen = new GameObject("Screen", typeof(RectTransform)).GetComponent<RectTransform>();
        screen.SetParent(root, false);
        screen.anchorMin = screen.anchorMax = screen.pivot = new Vector2(0.5f, 0.5f);
        screen.sizeDelta = new Vector2(BongoCanvasW, BongoCanvasH);
        screen.anchoredPosition = Vector2.zero;
        screen.localScale = new Vector3(k, k, 1f);
        return screen;
    }

    private static TextMeshProUGUI MakeLabel(Transform parent, string name, string text, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Image MakeImage(Transform parent, string name, Sprite sprite, Color color, Vector2 pos, Vector2 size, bool sliced = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        img.maskable = false;
        if (sliced && sprite != null) { img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f; }
        go.GetComponent<CanvasRenderer>().cullTransparentMesh = true;
        return img;
    }

    private static void SetAlpha(Graphic g, float a) { var c = g.color; c.a = a; g.color = c; }

    private static void SetArray(SerializedProperty arr, Object[] values)
    {
        if (arr == null) { Debug.LogWarning("[AddSignPreviews] missing array property"); return; }
        arr.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            arr.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
}
