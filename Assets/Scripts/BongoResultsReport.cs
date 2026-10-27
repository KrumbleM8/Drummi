using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Bongo results "report card": a slightly crumpled sheet of paper on the
/// Score page listing bars played / passed, perfect hits, perfect phrases and an
/// early-or-late timing verdict for the run just finished.
///
/// Visuals are a PLACEHOLDER. The sheet is a procedurally generated paper texture
/// (creases, grain, a wobbly torn edge) until real art is dropped into
/// <see cref="paperSprite"/>. The whole hierarchy (paper image, title, rows) is
/// built by <see cref="EnsureBuilt"/> — at runtime on first use when nothing is
/// wired, or in the editor via the "Drummi/Bongo/Bake Results Report" menu so the
/// pieces can be tuned in the inspector.
///
/// Flow: BeatEvaluator tallies a <see cref="BongoRunStats"/> per bar →
/// GameManager snapshots it at song end → ScoreScreen.ShowBongoReport → <see cref="Show"/>.
/// Non-Bongo modes pass null and the report stays hidden.
/// </summary>
[DisallowMultipleComponent]
public class BongoResultsReport : MonoBehaviour
{
    public const string DefaultObjectName = "BongoReport";

    #region Inspector — look
    [Header("Paper (placeholder until real art exists)")]
    [Tooltip("Drop the real crumpled-paper sprite here. Empty = a procedural placeholder is generated.")]
    [SerializeField] private Sprite paperSprite;
    [SerializeField] private Color paperTint = Color.white;
    [Tooltip("Size of the sheet in page units (the Score page is 800 x 1422).")]
    [SerializeField] private Vector2 paperSize = new Vector2(600f, 540f);
    [Tooltip("Where the sheet rests on the page. Above the score block by default.")]
    [SerializeField] private Vector2 restPosition = new Vector2(0f, 400f);
    [Tooltip("Resting tilt, degrees. A few degrees off-square sells 'tossed on the table'.")]
    [SerializeField] private float tiltDegrees = -3f;

    [Header("Ink")]
    [SerializeField] private Color inkColor = new Color(0.22f, 0.16f, 0.11f, 1f);
    [SerializeField] private Color faintInkColor = new Color(0.22f, 0.16f, 0.11f, 0.55f);
    [Tooltip("Empty = copied from the page's other texts (ScoreScreen.perfectHitsText), else TMP default.")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private float titleFontSize = 40f;
    [SerializeField] private float rowFontSize = 30f;
    [SerializeField] private float verdictFontSize = 34f;
    [SerializeField] private float detailFontSize = 22f;

    [Header("Entrance")]
    [Tooltip("Seconds for the sheet to drop onto the page. 0 = snap.")]
    [SerializeField] private float dropDuration = 0.45f;
    [SerializeField] private float dropHeight = 220f;
    [SerializeField] private float dropExtraTilt = 7f;
    #endregion

    #region Inspector — built parts
    [Header("Built parts (filled by EnsureBuilt — leave alone)")]
    [SerializeField] private RectTransform paper;
    [SerializeField] private CanvasGroup paperGroup;
    [SerializeField] private Image paperImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text barsPlayedValue;
    [SerializeField] private TMP_Text barsPassedValue;
    [SerializeField] private TMP_Text perfectHitsValue;
    [SerializeField] private TMP_Text perfectPhrasesValue;
    [SerializeField] private TMP_Text timingVerdict;
    [SerializeField] private TMP_Text timingDetail;
    #endregion

    public bool IsBuilt => paper != null && paperImage != null && barsPlayedValue != null;

    private Coroutine _entrance;
    private Sprite _generatedPaper;

    #region Public API
    /// <summary>Fills the sheet from <paramref name="stats"/>, shows it and plays the drop-in.</summary>
    public void Show(BongoRunStats stats)
    {
        if (stats == null) { Hide(); return; }

        EnsureBuilt();
        gameObject.SetActive(true);
        Fill(stats);
        PlayEntrance();

        Debug.Log($"[BongoResultsReport] {stats}");
    }

    public void Hide()
    {
        if (_entrance != null) { StopCoroutine(_entrance); _entrance = null; }
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Builds the paper + text hierarchy under this object if it isn't there yet.
    /// Safe to call repeatedly. Used at runtime (first Show) and by the editor baker.
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
            paper.anchoredPosition = restPosition;
            paper.localRotation = Quaternion.Euler(0f, 0f, tiltDegrees);
        }
        if (paperGroup == null) paperGroup = paper.GetComponent<CanvasGroup>() ?? paper.gameObject.AddComponent<CanvasGroup>();
        if (paperImage == null)
        {
            paperImage = paper.GetComponent<Image>() ?? paper.gameObject.AddComponent<Image>();
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
            content.offsetMin = new Vector2(48f, 40f);
            content.offsetMax = new Vector2(-48f, -36f);

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 6f;

            titleText = NewText("Title", content, fontAsset, titleFontSize, inkColor, TextAlignmentOptions.Center, 56f);
            titleText.text = "BONGO REPORT";
            titleText.fontStyle = FontStyles.Bold;

            NewText("Rule", content, fontAsset, rowFontSize * 0.6f, faintInkColor, TextAlignmentOptions.Center, 20f).text =
                "- - - - - - - - - - - - - - - - - -";

            barsPlayedValue     = NewRow("BarsPlayed",     content, fontAsset, "Bars played");
            barsPassedValue     = NewRow("BarsPassed",     content, fontAsset, "Bars passed");
            perfectHitsValue    = NewRow("PerfectHits",    content, fontAsset, "Perfect hits");
            perfectPhrasesValue = NewRow("PerfectPhrases", content, fontAsset, "Perfect phrases");

            NewText("Spacer", content, fontAsset, 10f, Color.clear, TextAlignmentOptions.Center, 14f);

            var timingLabel = NewText("TimingLabel", content, fontAsset, detailFontSize, faintInkColor, TextAlignmentOptions.Center, 28f);
            timingLabel.text = "TIMING";
            timingLabel.characterSpacing = 8f;

            timingVerdict = NewText("TimingVerdict", content, fontAsset, verdictFontSize, inkColor, TextAlignmentOptions.Center, 46f);
            timingVerdict.fontStyle = FontStyles.Bold;

            timingDetail = NewText("TimingDetail", content, fontAsset, detailFontSize, faintInkColor, TextAlignmentOptions.Center, 30f);
        }
    }
    #endregion

    #region Fill
    private void Fill(BongoRunStats s)
    {
        barsPlayedValue.text     = s.BarsPlayed.ToString();
        barsPassedValue.text     = $"{s.BarsPassed} / {s.BarsPlayed}";
        perfectHitsValue.text    = $"{s.PerfectHits} / {s.TotalBeats}";
        perfectPhrasesValue.text = s.PerfectPhrases.ToString();

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
    }
    #endregion

    #region Entrance
    private void PlayEntrance()
    {
        if (_entrance != null) { StopCoroutine(_entrance); _entrance = null; }

        if (dropDuration <= 0f || !isActiveAndEnabled)
        {
            SnapToRest();
            return;
        }
        _entrance = StartCoroutine(EntranceRoutine());
    }

    private void SnapToRest()
    {
        paper.anchoredPosition = restPosition;
        paper.localRotation = Quaternion.Euler(0f, 0f, tiltDegrees);
        paperGroup.alpha = 1f;
    }

    private IEnumerator EntranceRoutine()
    {
        // Unscaled time: the pause handler may have left timeScale at 0 on a game over.
        float t = 0f;
        Vector2 from = restPosition + new Vector2(0f, dropHeight);
        float fromTilt = tiltDegrees + dropExtraTilt;

        while (t < dropDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dropDuration);
            float e = 1f - Mathf.Pow(1f - k, 3f);                  // ease-out cubic
            float settle = Mathf.Sin(k * Mathf.PI) * 0.12f * (1f - k); // tiny overshoot on the tilt

            paper.anchoredPosition = Vector2.LerpUnclamped(from, restPosition, e);
            paper.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(fromTilt, tiltDegrees, e) - settle * dropExtraTilt);
            paperGroup.alpha = Mathf.Clamp01(k * 2.5f);
            yield return null;
        }

        SnapToRest();
        _entrance = null;
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

    /// <summary>A label on the left and a value on the right, sharing one row.</summary>
    private TMP_Text NewRow(string name, Transform parent, TMP_FontAsset fontAsset, string label)
    {
        var row = NewRect(name, parent);
        var le = row.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = rowFontSize + 16f;
        le.minHeight = le.preferredHeight;

        var l = NewStretchText("Label", row, fontAsset, rowFontSize, inkColor, TextAlignmentOptions.MidlineLeft);
        l.text = label;
        var v = NewStretchText("Value", row, fontAsset, rowFontSize, inkColor, TextAlignmentOptions.MidlineRight);
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

    #region Placeholder paper texture
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
    #endregion
}
