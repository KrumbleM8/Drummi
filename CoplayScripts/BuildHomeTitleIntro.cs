using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using KrumbleHut.Drummi.FlashCards;

/// <summary>
/// EDIT-MODE, Home.unity open: builds the title-intro rig —
///   OverlayCanvas/TitleBanner (CanvasGroup + blocker, TitleArt, PlayButton),
///   PlanetPivot/MountainOnGlobe (TrashMountain standing on the far side),
///   HomeIntroDirector on MenuFlow (all fields wired, explicit values),
///   PlanetSpinner.sortingCamera = Main Camera.
/// Idempotent: finds existing objects by name. Saves twice (prefab-instance
/// save gotcha) — git-status the scene afterwards.
/// </summary>
public static class BuildHomeTitleIntro
{
    // ── Tuning ──────────────────────────────────────────────────────────────
    const float TitleArtWidth      = 430f;   // ref px (canvas 412x815) — board ≈ 345 px wide; ropes still inside the screen edges
    const float TitleBoardTopPx    = 110f;   // board top this far below screen top (OverlayUtilCanvas X button occupies ~0-70 px top-right)
    const float TitleBoardTopFrac  = 1000f / 3500f; // where the board starts in the source PNG
    const float PlayButtonY        = 150f;   // ref px from the bottom edge to the button centre
    static readonly Vector2 PlayButtonSize = new(290f, 100f);

    const float MountainRingDeg    = 140f;   // peeks over the top of the planet at introRollAngle 130 (signs on the lower front); ≥70° behind Play so it never crests in the menu
    const float MountainLateralDeg = 0f;
    const float MountainDist       = 12f;    // centre-pivot sprite: base slightly buried
    static readonly Vector3 MountainScale = new(2.4f, 2f, 1f);

    const string TitlePngPath    = "Assets/Images/PLACEHOLDER GENERATED/DrummiTitleHang.png";
    const string MountainPngPath = "Assets/Images/TrashMountain.png";

    public static void Execute()
    {
        if (Application.isPlaying) { Debug.Log("[BuildHomeTitleIntro] FAIL: exit play mode first."); return; }
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Home") { Debug.Log("[BuildHomeTitleIntro] FAIL: active scene is '" + scene.name + "', expected Home."); return; }

        GameObject camGO     = GameObject.Find("Main Camera");
        GameObject pivotGO   = GameObject.Find("PlanetPivot");
        GameObject menuFlow  = GameObject.Find("MenuFlow");
        GameObject overlay   = GameObject.Find("OverlayCanvas");
        GameObject instrBtn  = GameObject.Find("OverlayCanvas/InstrumentButton");
        GameObject midLayer  = GameObject.Find("ParallaxMid");
        Transform  mountainMid = midLayer != null ? midLayer.transform.Find("MountainMid") : null;
        Transform  bushA     = pivotGO != null ? pivotGO.transform.Find("BushA") : null;

        if (camGO == null || pivotGO == null || menuFlow == null || overlay == null || instrBtn == null || mountainMid == null || bushA == null)
        {
            Debug.Log("[BuildHomeTitleIntro] FAIL: missing scene object. cam=" + (camGO != null) + " pivot=" + (pivotGO != null) +
                      " menuFlow=" + (menuFlow != null) + " overlay=" + (overlay != null) + " instr=" + (instrBtn != null) +
                      " mountainMid=" + (mountainMid != null) + " bushA=" + (bushA != null));
            return;
        }

        var camera  = camGO.GetComponent<Camera>();
        var spinner = pivotGO.GetComponent<PlanetSpinner>();
        var menuCtl = menuFlow.GetComponent<FlashCardsMenuController>();
        if (spinner == null || menuCtl == null) { Debug.Log("[BuildHomeTitleIntro] FAIL: PlanetSpinner/FlashCardsMenuController missing."); return; }

        // ── 0. Title art import: keep the board sharp (3500 px tall source) ──
        var importer = AssetImporter.GetAtPath(TitlePngPath) as TextureImporter;
        if (importer != null && importer.maxTextureSize < 4096)
        {
            importer.maxTextureSize = 4096;
            importer.SaveAndReimport();
            Debug.Log("[BuildHomeTitleIntro] DrummiTitleHang maxTextureSize -> 4096");
        }
        Sprite titleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TitlePngPath);
        Sprite mountainSprite = AssetDatabase.LoadAssetAtPath<Sprite>(MountainPngPath);
        if (mountainSprite == null)
            mountainSprite = AssetDatabase.LoadAllAssetsAtPath(MountainPngPath).OfType<Sprite>().FirstOrDefault();
        Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        if (titleSprite == null || mountainSprite == null) { Debug.Log("[BuildHomeTitleIntro] FAIL: sprite load title=" + (titleSprite != null) + " mountain=" + (mountainSprite != null)); return; }

        // ── 1. OverlayCanvas/TitleBanner ─────────────────────────────────────
        GameObject banner = FindOrCreate(overlay.transform, "TitleBanner", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        var bannerRT = (RectTransform)banner.transform;
        bannerRT.anchorMin = Vector2.zero; bannerRT.anchorMax = Vector2.one;
        bannerRT.offsetMin = Vector2.zero; bannerRT.offsetMax = Vector2.zero;
        bannerRT.localScale = Vector3.one;
        bannerRT.SetAsLastSibling();
        var blocker = banner.GetComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0f);
        blocker.raycastTarget = true; // eats sign/bush taps while the title shows
        var bannerCG = banner.GetComponent<CanvasGroup>();
        bannerCG.alpha = 1f; bannerCG.interactable = true; bannerCG.blocksRaycasts = true;

        GameObject titleArt = FindOrCreate(banner.transform, "TitleArt", typeof(RectTransform), typeof(Image));
        var artRT = (RectTransform)titleArt.transform;
        float artHeight = TitleArtWidth * ((float)titleSprite.texture.height / titleSprite.texture.width);
        artRT.anchorMin = artRT.anchorMax = new Vector2(0.5f, 1f);
        artRT.pivot = new Vector2(0.5f, 1f);
        artRT.sizeDelta = new Vector2(TitleArtWidth, artHeight);
        artRT.anchoredPosition = new Vector2(0f, artHeight * TitleBoardTopFrac - TitleBoardTopPx); // ropes run off the top
        artRT.localScale = Vector3.one;
        var artImg = titleArt.GetComponent<Image>();
        artImg.sprite = titleSprite; artImg.preserveAspect = true; artImg.raycastTarget = false;

        GameObject playGO = FindOrCreate(banner.transform, "PlayButton", typeof(RectTransform), typeof(Image), typeof(Button));
        var playRT = (RectTransform)playGO.transform;
        playRT.anchorMin = playRT.anchorMax = new Vector2(0.5f, 0f);
        playRT.pivot = new Vector2(0.5f, 0.5f);
        playRT.sizeDelta = PlayButtonSize;
        playRT.anchoredPosition = new Vector2(0f, PlayButtonY);
        playRT.localScale = Vector3.one;
        var playImg = playGO.GetComponent<Image>();
        playImg.sprite = uiSprite; playImg.type = Image.Type.Sliced; playImg.color = new Color(0.95f, 0.62f, 0.25f);
        var playBtn = playGO.GetComponent<Button>();
        playBtn.targetGraphic = playImg;

        GameObject labelGO = FindOrCreate(playGO.transform, "Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var labelRT = (RectTransform)labelGO.transform;
        labelRT.anchorMin = Vector2.zero; labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = new Vector2(8f, 6f); labelRT.offsetMax = new Vector2(-8f, -6f);
        labelRT.localScale = Vector3.one;
        var label = labelGO.GetComponent<TextMeshProUGUI>();
        label.text = "PLAY";
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true; label.fontSizeMin = 20f; label.fontSizeMax = 64f;
        label.fontStyle = FontStyles.Bold;
        label.color = new Color(0.25f, 0.13f, 0.05f);
        label.raycastTarget = false;

        // ── 2. PlanetPivot/MountainOnGlobe — on the sphere, opposite the signs ─
        GameObject mountain = FindOrCreate(pivotGO.transform, "MountainOnGlobe", typeof(SpriteRenderer));
        var mSR = mountain.GetComponent<SpriteRenderer>();
        mSR.sprite = mountainSprite;
        mSR.sharedMaterial = bushA.GetComponent<SpriteRenderer>().sharedMaterial;
        PlaceOnSphere(mountain.transform, MountainRingDeg, MountainLateralDeg, MountainDist, MountainScale);

        // ── 2b. PlanetIdleMotion on PlanetPivot (disabled; director enables) ──
        var idle = pivotGO.GetComponent<PlanetIdleMotion>();
        if (idle == null) idle = pivotGO.AddComponent<PlanetIdleMotion>();
        idle.enabled = false;
        var idleSO = new SerializedObject(idle);
        idleSO.FindProperty("spinner").objectReferenceValue = spinner;
        idleSO.FindProperty("floatAmplitude").floatValue = 0.8f;
        idleSO.FindProperty("floatPeriod").floatValue = 3.6f;
        idleSO.FindProperty("tiltDegrees").floatValue = 1.5f;
        idleSO.FindProperty("tiltPeriod").floatValue = 6.5f;
        idleSO.FindProperty("twistDegrees").floatValue = 1.2f;
        idleSO.FindProperty("twistPeriod").floatValue = 9f;
        idleSO.ApplyModifiedPropertiesWithoutUndo();

        // ── 3. HomeIntroDirector on MenuFlow ─────────────────────────────────
        var director = menuFlow.GetComponent<HomeIntroDirector>();
        if (director == null) director = menuFlow.AddComponent<HomeIntroDirector>();
        var so = new SerializedObject(director);
        Ref(so, "menuCamera", camera);
        Ref(so, "spinner", spinner);
        Ref(so, "menuController", menuCtl);
        Ref(so, "mountainMid", mountainMid);
        Ref(so, "titleBanner", bannerCG);
        Ref(so, "titleArt", artRT);
        Ref(so, "playButton", playBtn);
        var hud = so.FindProperty("hudHiddenDuringTitle");
        hud.arraySize = 1; hud.GetArrayElementAtIndex(0).objectReferenceValue = instrBtn;
        var frozen = so.FindProperty("parallaxFrozenDuringIntro");
        GameObject farLayer = GameObject.Find("ParallaxFar");
        frozen.arraySize = 2;
        frozen.GetArrayElementAtIndex(0).objectReferenceValue = farLayer != null ? farLayer.GetComponent<ParallaxRotationLayer>() : null;
        frozen.GetArrayElementAtIndex(1).objectReferenceValue = midLayer.GetComponent<ParallaxRotationLayer>();
        Ref(so, "idleMotion", idle);
        var backdrop = so.FindProperty("constantSizeBackdrop");
        Transform clouds = farLayer != null ? farLayer.transform.Find("Clouds") : null;
        Transform sun    = farLayer != null ? farLayer.transform.Find("Sun") : null;
        backdrop.arraySize = 2;
        backdrop.GetArrayElementAtIndex(0).objectReferenceValue = clouds;
        backdrop.GetArrayElementAtIndex(1).objectReferenceValue = sun;
        if (clouds == null || sun == null) Debug.Log("[BuildHomeTitleIntro] WARN: Clouds/Sun not found under ParallaxFar");
        so.FindProperty("introCameraPosition").vector3Value = new Vector3(0f, 18f, -64f);
        so.FindProperty("introCameraEuler").vector3Value = new Vector3(12f, -5f, 0f);
        so.FindProperty("introRollAngle").floatValue = 115f; // signs at ring -115/-80/-45: Bongo's board stays inside the far-view disc (at -130 its tip poked out below)
        so.FindProperty("extraRotations").intValue = 0;
        so.FindProperty("idleBlendOutFraction").floatValue = 0.35f;
        so.FindProperty("mountainHideOffsetY").floatValue = 14f;
        so.FindProperty("mountainRiseWhenCovered").boolValue = true;
        so.FindProperty("mountainRiseDelay").floatValue = 0f;
        so.FindProperty("mountainRiseDuration").floatValue = 1.2f;
        so.FindProperty("introDuration").floatValue = 3.5f;
        so.FindProperty("bannerFadeDuration").floatValue = 0.4f;
        so.FindProperty("bannerRisePixels").floatValue = 300f;
        so.FindProperty("zoomEase").intValue = (int)DG.Tweening.Ease.InOutSine;
        so.FindProperty("rollEase").intValue = (int)DG.Tweening.Ease.InOutSine;
        so.FindProperty("mountainEase").intValue = (int)DG.Tweening.Ease.OutSine;
        so.ApplyModifiedPropertiesWithoutUndo();

        // ── 4. Spinner: camera-derived horizon ───────────────────────────────
        var spinSO = new SerializedObject(spinner);
        spinSO.FindProperty("sortingCamera").objectReferenceValue = camGO.transform;
        spinSO.ApplyModifiedPropertiesWithoutUndo();

        // ── 5. Save (twice — see LockBackSigns gotcha) ───────────────────────
        foreach (Object o in new Object[] { banner, titleArt, playGO, labelGO, mountain, director, spinner, idle, menuFlow, pivotGO, overlay })
            EditorUtility.SetDirty(o);
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved1 = EditorSceneManager.SaveOpenScenes();
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved2 = EditorSceneManager.SaveOpenScenes();

        Debug.Log("[BuildHomeTitleIntro] done. TitleArt h=" + artHeight.ToString("F0") + " y=" + artRT.anchoredPosition.y.ToString("F0") +
                  " mountainPos=" + mountain.transform.localPosition + " saved=" + saved1 + "/" + saved2);
    }

    static GameObject FindOrCreate(Transform parent, string name, params System.Type[] components)
    {
        Transform t = parent.Find(name);
        GameObject go;
        if (t != null)
        {
            go = t.gameObject;
            foreach (var c in components) if (go.GetComponent(c) == null) go.AddComponent(c);
        }
        else
        {
            go = new GameObject(name, components);
            go.transform.SetParent(parent, false);
        }
        return go;
    }

    static void Ref(SerializedObject so, string prop, Object value)
    {
        var p = so.FindProperty(prop);
        if (p == null) { Debug.Log("[BuildHomeTitleIntro] WARN: property not found " + prop); return; }
        p.objectReferenceValue = value;
    }

    // Same math as ConvertToGlobe.PlaceOnSphere (ring a: 0 = focus top, + = over the far horizon; lateral b: + = right).
    static void PlaceOnSphere(Transform t, float ringDeg, float lateralDeg, float dist, Vector3 scale)
    {
        float a = ringDeg * Mathf.Deg2Rad;
        float b = lateralDeg * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(Mathf.Sin(b), Mathf.Cos(b) * Mathf.Cos(a), Mathf.Cos(b) * Mathf.Sin(a));
        t.localPosition = dir * dist;
        t.localRotation = Quaternion.Euler(ringDeg, 0f, 0f) * Quaternion.Euler(0f, 0f, -lateralDeg);
        t.localScale = scale;
    }
}
