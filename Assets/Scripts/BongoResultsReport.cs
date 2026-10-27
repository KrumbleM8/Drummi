using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Bongo results "report card": a slightly crumpled sheet of paper that slides
/// in from off-screen to the centre of the Score page and carries the whole result —
/// score, grade stamp, bars played / passed, perfect hits, perfect phrases, high
/// score and an early-or-late timing verdict. While it is up, ScoreScreen hides its
/// older separate score / grade / icon / perfect-hits / high-score elements.
///
/// Visuals are a PLACEHOLDER. The sheet is a procedurally generated paper texture
/// (creases, grain, a wobbly torn edge) and the grade is a tilted inked letter in a
/// ring, until real art is dropped into <see cref="paperSprite"/>. The hierarchy
/// (paper image, stamp, title, rows) is built by <see cref="EnsureBuilt"/> — at
/// runtime on first use when nothing is wired, or in the editor via
/// "Drummi/Bongo/Bake Results Report Into Open Scene" for inspector tuning.
///
/// Flow: BeatEvaluator tallies a <see cref="BongoRunStats"/> per bar →
/// GameManager snapshots it at song end → ScoreScreen.ShowBongoReport → <see cref="Show"/>.
/// Non-Bongo modes pass null and the report stays hidden.
/// </summary>
[DisallowMultipleComponent]
public class BongoResultsReport : MonoBehaviour
{
    public const string DefaultObjectName = "BongoReport";

    /// <summary>The score-screen values that moved onto the paper (filled by ScoreScreen).</summary>
    public struct Headline
    {
        public int Score;
        /// <summary>Seconds to count the score up from 0. 0 = show immediately.</summary>
        public float ScoreCountDuration;
        public string Grade;
        public Color GradeColor;
        /// <summary>Text for the high-score row, e.g. "1200" or "NEW! 1450".</summary>
        public string HighScoreText;
        public bool IsNewHighScore;
    }

    #region Inspector — look
    [Header("Paper (placeholder until real art exists)")]
    [Tooltip("Drop the real crumpled-paper sprite here. Empty = a procedural placeholder is generated.")]
    [SerializeField] private Sprite paperSprite;
    [SerializeField] private Color paperTint = Color.white;
    [Tooltip("Design size of the sheet in page units — A4 proportions (1 : 1.414). The sheet is scaled " +
             "uniformly at runtime to fit between the input bongos and the rat's bongo (see Placement).")]
    [SerializeField] private Vector2 paperSize = new Vector2(480f, 680f);
    [Tooltip("Resting tilt, degrees. A few degrees off-square sells 'tossed on the table'.")]
    [SerializeField] private float tiltDegrees = -3f;

    [Header("Placement")]
    [Tooltip("Fit the sheet between the input bongos (bottom of the screen) and the rat's bongo. " +
             "Off = rest at Fallback Rest Position at design size.")]
    [SerializeField] private bool fitBetweenBongos = true;
    [Tooltip("Top of the input-bongos panel, measured in canvas units up from the bottom screen edge " +
             "(Bongos panel: anchored y 88 + half of 586).")]
    [SerializeField] private float inputBongosTopFromBottom = 381f;
    [Tooltip("Gap kept between the sheet's bottom edge and the input bongos.")]
    [SerializeField] private float bottomGap = 10f;
    [Tooltip("World-space y of the rat's bongo rim; the sheet's top edge stays at or below it so the " +
             "rat's bongo is never fully covered. CustardTheRat sprite spans ~0.8 to 4.6 world units.")]
    [SerializeField] private float ratBongoTopWorldY = 1.7f;
    [SerializeField] private float minScale = 0.55f;
    [SerializeField] private float maxScale = 1f;
    [Tooltip("Horizontal rest position of the sheet's centre.")]
    [SerializeField] private float restX = 0f;
    [Tooltip("Used when Fit Between Bongos is off, or no Canvas can be found.")]
    [SerializeField] private Vector2 fallbackRestPosition = new Vector2(0f, 40f);
    [Tooltip("Text-free margin at the bottom of the sheet so the Back button can overlap the paper " +
             "without covering any ink.")]
    [SerializeField] private float bottomTextMargin = 140f;

    [Header("Ink")]
    [SerializeField] private Color inkColor = new Color(0.22f, 0.16f, 0.11f, 1f);
    [SerializeField] private Color faintInkColor = new Color(0.22f, 0.16f, 0.11f, 0.55f);
    [Tooltip("Ink for the NEW! high-score row.")]
    [SerializeField] private Color highlightInkColor = new Color(0.72f, 0.18f, 0.12f, 1f);
    [Tooltip("Empty = copied from the page's other texts (ScoreScreen.perfectHitsText), else TMP default.")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private float titleFontSize = 36f;
    [SerializeField] private float scoreFontSize = 44f;
    [SerializeField] private float rowFontSize = 28f;
    [SerializeField] private float verdictFontSize = 30f;
    [SerializeField] private float detailFontSize = 20f;

    [Header("Grade stamp")]
    [SerializeField] private Vector2 stampOffsetFromTopRight = new Vector2(-78f, -70f);
    [SerializeField] private float stampSize = 120f;
    [SerializeField] private float stampTiltDegrees = 14f;
    [SerializeField] private float stampFontSize = 88f;
    [Range(0f, 1f)] [SerializeField] private float stampInkAlpha = 0.8f;

    [Header("Entrance")]
    [Tooltip("Where the sheet starts relative to its rest position. Default: well below the bottom of the screen.")]
    [SerializeField] private Vector2 enterOffset = new Vector2(0f, -1500f);
    [Tooltip("Seconds for the sheet to slide to the centre. 0 = snap.")]
    [SerializeField] private float enterDuration = 0.6f;
    [SerializeField] private float enterExtraTilt = 10f;
    #endregion

    #region Inspector — built parts
    [Header("Built parts (filled by EnsureBuilt — leave alone)")]
    [SerializeField] private RectTransform paper;
    [SerializeField] private CanvasGroup paperGroup;
    [SerializeField] private Image paperImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text scoreValue;
    [SerializeField] private TMP_Text barsPlayedValue;
    [SerializeField] private TMP_Text barsPassedValue;
    [SerializeField] private TMP_Text perfectHitsValue;
    [SerializeField] private TMP_Text perfectPhrasesValue;
    [SerializeField] private TMP_Text highScoreValue;
    [SerializeField] private TMP_Text timingVerdict;
    [SerializeField] private TMP_Text timingDetail;
    [SerializeField] private RectTransform stamp;
    [SerializeField] private Image stampRing;
    [SerializeField] private TMP_Text stampLetter;
    #endregion

    public bool IsBuilt => paper != null && paperImage != null && barsPlayedValue != null && scoreValue != null;

    private Coroutine _routine;
    private Sprite _generatedPaper;
    private Sprite _generatedRing;

    // Resolved by ComputePlacement() on every Show — where the sheet rests and how big it is.
    private Vector2 _restPosition;
    private float _restScale = 1f;

    #region Public API
    /// <summary>Fills the sheet, shows it and slides it in. <paramref name="headline"/> carries score / grade / high score.</summary>
    public void Show(BongoRunStats stats, Headline headline)
    {
        if (stats == null) { Hide(); return; }

        EnsureBuilt();
        // Draw beneath the page's other children (the Back button) so the button can
        // sit on top of the paper and stay clickable.
        transform.SetAsFirstSibling();
        gameObject.SetActive(true);
        ComputePlacement();
        Fill(stats, headline);
        PlayEntrance(headline);

        Debug.Log($"[BongoResultsReport] score {headline.Score} grade {headline.Grade} | {stats}");
    }

    public void Hide()
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Builds the paper + stamp + text hierarchy under this object if it isn't there
    /// yet. Safe to call repeatedly. Used at runtime (first Show) and by the editor baker.
    /// </summary>
    public void EnsureBuilt()
    {
        var root = GetComponent<RectTransform>();
        if (root == null) root = gameObject.AddComponent<RectTransform>();
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = Vector2.zero;
        root.sizeDelta = Vector2.zero;
        root.localScale = Vector3.one;

        if (paper == null)
        {
            paper = NewRect("Paper", root);
            paper.sizeDelta = paperSize;
            paper.anchoredPosition = fallbackRestPosition;
            paper.localRotation = Quaternion.Euler(0f, 0f, tiltDegrees);
        }
        // NB: Unity's "fake null" for missing components defeats ??, so compare with == null.
        if (paperGroup == null) paperGroup = paper.GetComponent<CanvasGroup>();
        if (paperGroup == null) paperGroup = paper.gameObject.AddComponent<CanvasGroup>();
        if (paperImage == null) paperImage = paper.GetComponent<Image>();
        if (paperImage == null)
        {
            paperImage = paper.gameObject.AddComponent<Image>();
            paperImage.type = Image.Type.Simple;
            paperImage.preserveAspect = false;
            paperImage.raycastTarget = false;
        }
        paperImage.color = paperTint;
        ApplyPaperSprite();

        var fontAsset = ResolveFont();

        if (titleText == null)
        {
            // Content column: padded so the ink stays clear of the torn edge.
            var content = NewRect("Content", paper);
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            // Bottom offset leaves a text-free strip for the Back button to overlap.
            content.offsetMin = new Vector2(36f, bottomTextMargin);
            content.offsetMax = new Vector2(-36f, -30f);

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 5f;

            titleText = NewText("Title", content, fontAsset, titleFontSize, inkColor, TextAlignmentOptions.Center, 46f);
            titleText.text = "BONGO REPORT";
            titleText.fontStyle = FontStyles.Bold;

            scoreValue = NewRow("Score", content, fontAsset, "Score", rowFontSize, scoreFontSize, 56f);

            NewText("Rule", content, fontAsset, rowFontSize * 0.55f, faintInkColor, TextAlignmentOptions.Center, 14f).text =
                "- - - - - - - - - - - - - - - - - - - -";

            barsPlayedValue     = NewRow("BarsPlayed",     content, fontAsset, "Bars played",     rowFontSize, rowFontSize, 38f);
            barsPassedValue     = NewRow("BarsPassed",     content, fontAsset, "Bars passed",     rowFontSize, rowFontSize, 38f);
            perfectHitsValue    = NewRow("PerfectHits",    content, fontAsset, "Perfect hits",    rowFontSize, rowFontSize, 38f);
            perfectPhrasesValue = NewRow("PerfectPhrases", content, fontAsset, "Perfect phrases", rowFontSize, rowFontSize, 38f);
            highScoreValue      = NewRow("HighScore",      content, fontAsset, "High score",      rowFontSize, rowFontSize, 38f);

            NewText("Spacer", content, fontAsset, 8f, Color.clear, TextAlignmentOptions.Center, 6f);

            var timingLabel = NewText("TimingLabel", content, fontAsset, detailFontSize * 0.95f, faintInkColor, TextAlignmentOptions.Center, 22f);
            timingLabel.text = "TIMING";
            timingLabel.characterSpacing = 8f;

            timingVerdict = NewText("TimingVerdict", content, fontAsset, verdictFontSize, inkColor, TextAlignmentOptions.Center, 40f);
            timingVerdict.fontStyle = FontStyles.Bold;

            timingDetail = NewText("TimingDetail", content, fontAsset, detailFontSize, faintInkColor, TextAlignmentOptions.Center, 24f);
        }

        if (stamp == null)
        {
            // Grade stamp: an inked letter inside a ring, tilted, in the top-right corner of the sheet.
            stamp = NewRect("GradeStamp", paper);
            stamp.anchorMin = stamp.anchorMax = new Vector2(1f, 1f);
            stamp.anchoredPosition = stampOffsetFromTopRight;
            stamp.sizeDelta = new Vector2(stampSize, stampSize);
            stamp.localRotation = Quaternion.Euler(0f, 0f, stampTiltDegrees);

            var ringRt = NewRect("Ring", stamp);
            ringRt.anchorMin = Vector2.zero;
            ringRt.anchorMax = Vector2.one;
            ringRt.offsetMin = ringRt.offsetMax = Vector2.zero;
            stampRing = ringRt.gameObject.AddComponent<Image>();
            stampRing.raycastTarget = false;
            stampRing.type = Image.Type.Simple;
            stampRing.preserveAspect = true;

            stampLetter = NewStretchText("Letter", stamp, fontAsset, stampFontSize, inkColor, TextAlignmentOptions.Center);
            stampLetter.fontStyle = FontStyles.Bold;
        }
        if (stampRing != null && stampRing.sprite == null)
        {
            if (_generatedRing == null) _generatedRing = GenerateRingSprite();
            stampRing.sprite = _generatedRing;
        }
    }
    #endregion

    #region Fill
    private void Fill(BongoRunStats s, Headline h)
    {
        scoreValue.text          = h.ScoreCountDuration > 0f ? "0" : h.Score.ToString();
        barsPlayedValue.text     = s.BarsPlayed.ToString();
        barsPassedValue.text     = $"{s.BarsPassed} / {s.BarsPlayed}";
        perfectHitsValue.text    = $"{s.PerfectHits} / {s.TotalBeats}";
        perfectPhrasesValue.text = s.PerfectPhrases.ToString();

        highScoreValue.text  = string.IsNullOrEmpty(h.HighScoreText) ? "-" : h.HighScoreText;
        highScoreValue.color = h.IsNewHighScore ? highlightInkColor : inkColor;

        timingVerdict.text = s.LeanText;
        if (s.Lean == BongoRunStats.TimingLean.NotEnoughData)
        {
            timingDetail.text = "";
        }
        else
        {
            string avg = Mathf.Abs(s.MeanOffsetMs) < 0.5f
                ? "avg on the beat"
                : $"avg {Mathf.Abs(s.MeanOffsetMs):0} ms {(s.MeanOffsetMs < 0f ? "early" : "late")}";
            timingDetail.text = $"early {s.EarlyHits}  ·  late {s.LateHits}  ·  {avg}";
        }

        // Grade stamp
        bool hasGrade = !string.IsNullOrEmpty(h.Grade);
        stamp.gameObject.SetActive(hasGrade);
        if (hasGrade)
        {
            Color stampCol = h.GradeColor;
            // Grade colours come from the old on-screen grade text (bright, for a dark
            // background). Darken them so they read as ink on paper.
            stampCol = Color.Lerp(stampCol, inkColor, 0.45f);
            stampCol.a = stampInkAlpha;
            stampLetter.text = h.Grade;
            stampLetter.color = stampCol;
            stampRing.color = stampCol;
        }
    }
    #endregion

    #region Entrance + score count-up
    private void PlayEntrance(Headline h)
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }

        if (!isActiveAndEnabled)
        {
            SnapToRest();
            scoreValue.text = h.Score.ToString();
            return;
        }
        _routine = StartCoroutine(EntranceRoutine(h));
    }

    /// <summary>
    /// Works out where the sheet rests and how big it is: A4 proportions, scaled so it
    /// fits between the top of the input bongos (bottom of the screen) and the rim of
    /// the rat's bongo (a world-space sprite), using the live canvas height and camera.
    /// </summary>
    private void ComputePlacement()
    {
        _restPosition = fallbackRestPosition;
        _restScale = 1f;

        var canvas = GetComponentInParent<Canvas>();
        if (!fitBetweenBongos || canvas == null) { ApplyPlacement(); return; }

        var canvasRt = canvas.rootCanvas.GetComponent<RectTransform>();
        float canvasHeight = canvasRt != null ? canvasRt.rect.height : 1422f;
        float halfH = canvasHeight * 0.5f;

        // World-y → canvas-y for an orthographic camera sitting at y = 0.
        var cam = Camera.main;
        float unitsPerWorld = (cam != null && cam.orthographic && cam.orthographicSize > 0f)
            ? canvasHeight / (2f * cam.orthographicSize)
            : canvasHeight / 10f;
        float camY = cam != null ? cam.transform.position.y : 0f;

        float bottomLimit = -halfH + inputBongosTopFromBottom + bottomGap;
        float topLimit = (ratBongoTopWorldY - camY) * unitsPerWorld;

        float available = topLimit - bottomLimit;
        float scale = Mathf.Clamp(available / paperSize.y, minScale, maxScale);
        float height = paperSize.y * scale;

        _restScale = scale;
        _restPosition = new Vector2(restX, bottomLimit + height * 0.5f);
        ApplyPlacement();

        Debug.Log($"[BongoResultsReport] placement: canvasH {canvasHeight:0}, bongos top {bottomLimit:0}, " +
                  $"rat bongo rim {topLimit:0} → scale {scale:0.00}, rest y {_restPosition.y:0}");
    }

    private void ApplyPlacement()
    {
        paper.sizeDelta = paperSize;
        paper.localScale = Vector3.one * _restScale;
    }

    private void SnapToRest()
    {
        paper.anchoredPosition = _restPosition;
        paper.localRotation = Quaternion.Euler(0f, 0f, tiltDegrees);
        paper.localScale = Vector3.one * _restScale;
        paperGroup.alpha = 1f;
    }

    private IEnumerator EntranceRoutine(Headline h)
    {
        // Unscaled time: the pause handler may have left timeScale at 0 on a game over.
        paperGroup.alpha = 1f;
        Vector2 restPosition = _restPosition;
        Vector2 from = restPosition + enterOffset;
        float fromTilt = tiltDegrees + enterExtraTilt;

        if (enterDuration > 0f)
        {
            float t = 0f;
            while (t < enterDuration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / enterDuration);
                float e = 1f - Mathf.Pow(1f - k, 3f);                     // ease-out cubic
                float settle = Mathf.Sin(k * Mathf.PI) * 0.12f * (1f - k); // tiny overshoot on the tilt

                paper.anchoredPosition = Vector2.LerpUnclamped(from, restPosition, e);
                paper.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(fromTilt, tiltDegrees, e) - settle * enterExtraTilt);
                yield return null;
            }
        }
        SnapToRest();

        // Then count the score up, like the old FinalScoreTxt did.
        if (h.ScoreCountDuration > 0f)
        {
            float t = 0f;
            while (t < h.ScoreCountDuration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / h.ScoreCountDuration);
                float e = 1f - Mathf.Pow(1f - k, 3f);
                scoreValue.text = Mathf.RoundToInt(Mathf.Lerp(0f, h.Score, e)).ToString();
                yield return null;
            }
        }
        scoreValue.text = h.Score.ToString();
        _routine = null;
    }
    #endregion

    #region Build helpers
    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        return rt;
    }

    private static TMP_Text NewText(string name, Transform parent, TMP_FontAsset fontAsset, float size, Color color,
                                    TextAlignmentOptions align, float preferredHeight)
    {
        var rt = NewRect(name, parent);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) tmp.font = fontAsset;
        tmp.fontSize = size;
        tmp.enableAutoSizing = false;
        tmp.color = color;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        tmp.text = "";

        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = preferredHeight;
        le.minHeight = preferredHeight;
        return tmp;
    }

    /// <summary>A label on the left and a (bold) value on the right, sharing one row. Returns the value text.</summary>
    private TMP_Text NewRow(string name, Transform parent, TMP_FontAsset fontAsset, string label,
                            float labelSize, float valueSize, float height)
    {
        var row = NewRect(name, parent);
        var le = row.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;

        var l = NewStretchText("Label", row, fontAsset, labelSize, inkColor, TextAlignmentOptions.MidlineLeft);
        l.text = label;
        var v = NewStretchText("Value", row, fontAsset, valueSize, inkColor, TextAlignmentOptions.MidlineRight);
        v.fontStyle = FontStyles.Bold;
        return v;
    }

    private static TMP_Text NewStretchText(string name, Transform parent, TMP_FontAsset fontAsset, float size, Color color,
                                           TextAlignmentOptions align)
    {
        var rt = NewRect(name, parent);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) tmp.font = fontAsset;
        tmp.fontSize = size;
        tmp.enableAutoSizing = false;
        tmp.color = color;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        tmp.text = "";
        return tmp;
    }

    private TMP_FontAsset ResolveFont()
    {
        if (font != null) return font;
        var page = GetComponentInParent<ScoreScreen>(true);
        if (page != null)
        {
            if (page.perfectHitsText != null && page.perfectHitsText.font != null) return page.perfectHitsText.font;
            if (page.finalScoreText != null && page.finalScoreText.font != null) return page.finalScoreText.font;
        }
        return TMP_Settings.defaultFontAsset;
    }

    private void ApplyPaperSprite()
    {
        if (paperSprite != null)
        {
            paperImage.sprite = paperSprite;
            return;
        }
        if (paperImage.sprite != null && paperImage.sprite != _generatedPaper) return; // baked/other art already set
        if (_generatedPaper == null) _generatedPaper = GeneratePaperSprite();
        paperImage.sprite = _generatedPaper;
    }
    #endregion

    #region Placeholder textures
    /// <summary>
    /// Procedural "slightly crumpled paper": warm off-white base, fine grain, soft
    /// broad shading, sharp ridged creases, a faint darkening toward the edges and a
    /// wobbly (torn-ish) outline carved into the alpha. Deterministic for a given seed.
    /// </summary>
    public static Sprite GeneratePaperSprite(int width = 384, int height = 346, int seed = 7)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "BongoReportPaper (generated)",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        var pixels = new Color32[width * height];
        Color baseCol = new Color(0.965f, 0.935f, 0.86f);
        float so = seed * 13.37f;

        for (int y = 0; y < height; y++)
        {
            float v = (float)y / (height - 1);
            for (int x = 0; x < width; x++)
            {
                float u = (float)x / (width - 1);

                // --- outline: distance to the nearest edge, with a low-frequency wobble so the edge isn't ruler-straight
                float wobble = (Mathf.PerlinNoise(u * 6f + so, v * 6f + so * 0.3f) - 0.5f) * 0.03f
                             + (Mathf.PerlinNoise(u * 23f + so * 2f, v * 23f) - 0.5f) * 0.008f;
                float inset = 0.018f + wobble;
                float edgeDist = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)) - inset;
                float alpha = Mathf.Clamp01(edgeDist / 0.006f);     // ~2px soft edge
                if (alpha <= 0f) { pixels[y * width + x] = new Color32(0, 0, 0, 0); continue; }

                // --- broad soft shading (big gentle folds)
                float broad = (Mathf.PerlinNoise(u * 2.2f + so, v * 2.2f - so) - 0.5f) * 0.10f;

                // --- ridged creases: thin dark lines where mid-scale noise crosses its middle
                float n1 = Mathf.PerlinNoise(u * 5.5f + so * 0.7f, v * 5.5f + so * 1.1f);
                float n2 = Mathf.PerlinNoise(u * 11f - so * 0.4f, v * 11f + so * 0.9f);
                float ridge1 = Mathf.Pow(1f - Mathf.Abs(n1 * 2f - 1f), 10f);
                float ridge2 = Mathf.Pow(1f - Mathf.Abs(n2 * 2f - 1f), 14f);
                float crease = ridge1 * 0.16f + ridge2 * 0.07f;
                // creases catch a little light on one side
                float highlight = Mathf.Pow(1f - Mathf.Abs((n1 + 0.012f) * 2f - 1f), 10f) * 0.05f;

                // --- paper grain
                float grain = (Mathf.PerlinNoise(x * 0.9f + so, y * 0.9f) - 0.5f) * 0.05f;

                // --- gentle darkening toward the edge
                float vignette = (1f - Mathf.SmoothStep(0f, 0.10f, edgeDist)) * 0.09f;

                float shade = 1f + broad - crease + highlight - vignette + grain;
                Color c = baseCol * shade;
                // creases go slightly warm/brown rather than grey
                c.r += crease * 0.03f;
                c.a = alpha;
                pixels[y * width + x] = c;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, false);

        var sprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "BongoReportPaper (generated)";
        return sprite;
    }

    /// <summary>A slightly uneven inked ring (white, tinted by the Image colour) for the grade stamp.</summary>
    public static Sprite GenerateRingSprite(int size = 128, int seed = 3)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "BongoReportStampRing (generated)",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        var pixels = new Color32[size * size];
        float so = seed * 7.77f;
        float c = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x - c) / c, dy = (y - c) / c;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float ang = Mathf.Atan2(dy, dx);
            // ring radius wobbles a little around the circle; ink thickness varies too
            float wob = (Mathf.PerlinNoise(Mathf.Cos(ang) * 1.6f + so, Mathf.Sin(ang) * 1.6f + so) - 0.5f) * 0.05f;
            float ringR = 0.90f + wob;
            float thick = 0.045f + (Mathf.PerlinNoise(Mathf.Cos(ang) * 3f - so, Mathf.Sin(ang) * 3f) - 0.5f) * 0.02f;
            float d = Mathf.Abs(r - ringR);
            float a = 1f - Mathf.SmoothStep(thick * 0.6f, thick, d);
            // patchy ink coverage
            a *= 0.75f + 0.25f * Mathf.PerlinNoise(x * 0.25f + so, y * 0.25f);
            pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "BongoReportStampRing (generated)";
        return sprite;
    }
    #endregion
}
