using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bongo "beat flash" HUD. Replaces the slider-bar beat/input dots with
/// short-lived flashes on a full-canvas layer:
///
///   • Custard's call beat  → the Indicator icon pops up LARGE near screen
///     centre, nudged left/right for the bongo side, then soft-fades out.
///   • Player hit           → the SAME big centre flash as the call (same size,
///     placement, lean, pop and fade), tinted for the bongo side; a bad hit
///     shows the same flash in grey, leaning early/late or keeled over on a
///     full miss.
///   • Perfect hit          → the PerfectStar prefab pops in BEHIND the player
///     flash, centred on it and larger than the disc, like a background stamp,
///     with a gentle eased turn, and fades out before the disc does.
///
/// Lives on its own stretched RectTransform under GameplayElements so it
/// inherits the gameplay show/hide lifecycle. Never blocks raycasts.
/// BeatVisualScheduler / PlayerInputVisualHandler call into it when
/// <see cref="useFlash"/> is on; switch it off to fall back to the old
/// slider-bar dots (re-activate IndicatorBar / ScorePanel / Score(Combo)
/// in the scene to see them again).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class BongoBeatFlash : MonoBehaviour
{
    [Header("Enable")]
    [Tooltip("When off, the visual handlers fall back to spawning dots on the slider bar.")]
    public bool useFlash = true;

    [Header("Custard call flash (big, centre)")]
    [Tooltip("Icon size in canvas units (canvas reference width is 800).")]
    public float callSize = 270f;
    [Tooltip("Horizontal nudge from centre; left bongo flashes at -X, right at +X.")]
    public float callOffsetX = 135f;
    [Tooltip("Vertical offset from the layer centre.")]
    public float callOffsetY = -60f;
    [Tooltip("Lean toward the bongo side, in degrees (left leans +, right leans -).")]
    public float callTiltDegrees = 8f;
    [Tooltip("Scale the icon pops in at before settling to 1.")]
    public float callPopScale = 1.25f;
    public float callPopDuration = 0.08f;
    [Tooltip("Time held at full opacity before the fade starts.")]
    public float callHoldDuration = 0.04f;
    [Tooltip("Soft fade-out length.")]
    public float callFadeDuration = 0.25f;
    [Tooltip("Scale drifted to while fading, for a gentle release.")]
    public float callFadeEndScale = 1.08f;

    [Header("Player hit feedback (same style as the call flash)")]
    [Tooltip("Player hits reuse the call flash's size, position, lean, pop and fade so both read the same. Bad hits use this tint instead of the side colour.")]
    public Color missColor = Color.gray;
    [Tooltip("Extra lean for an early (+) / late (-) hit, in degrees, on top of the side lean.")]
    public float playerOffTimingTilt = 45f;
    [Tooltip("Lean for a full miss / wrong-side hit, in degrees (keeled over).")]
    public float playerMissTilt = 90f;

    [Header("Perfect star (background stamp behind the player flash)")]
    [Tooltip("Star width/height as a multiple of callSize; >1 so the points peek out around the disc.")]
    public float starSizeFactor = 1.6f;
    [Tooltip("Vertical offset from the player flash's centre (0 = perfectly centred behind it).")]
    public float starOffsetY = 0f;
    [Tooltip("Total gentle turn over the star's whole life, in degrees (eased out, so most of it happens early).")]
    public float starSpinDegrees = 60f;
    [Tooltip("Scale the star pops in from before settling to 1.")]
    public float starStartScale = 0.7f;
    [Tooltip("Pop-in length; keep it close to callPopDuration so star and disc arrive together.")]
    public float starGrowDuration = 0.08f;
    [Tooltip("Fade length. Grow + fade should be shorter than the disc's pop + hold + fade (0.37 s) so the star is gone first.")]
    public float starFadeDuration = 0.2f;
    [Tooltip("Scale drifted to while fading, for a gentle release.")]
    public float starFadeEndScale = 1.05f;

    private RectTransform layer;
    private Vector2 lastPlayerPos;
    private bool hasPlayerPos;
    private RectTransform lastPlayerRt;

    private void Awake()
    {
        layer = (RectTransform)transform;

        // The layer sits over the whole play area: make sure it can never eat touches.
        var group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    // ---------------------------------------------------------------- Custard

    /// <summary>Big centre flash for one of Custard's call beats.</summary>
    public void FlashCall(GameObject iconPrefab, bool isRightBongo, Color tint)
    {
        if (!CanSpawn(iconPrefab)) return;

        RectTransform rt = SpawnIcon(iconPrefab, tint, out CanvasGroup group);
        rt.sizeDelta = new Vector2(callSize, callSize);
        rt.anchoredPosition = new Vector2(isRightBongo ? callOffsetX : -callOffsetX, callOffsetY);
        rt.localRotation = Quaternion.Euler(0f, 0f, isRightBongo ? -callTiltDegrees : callTiltDegrees);

        StartCoroutine(AnimateCall(rt, group));
    }

    private IEnumerator AnimateCall(RectTransform rt, CanvasGroup group)
    {
        group.alpha = 1f;

        // Pop in: large → normal (ease out)
        yield return Tween(callPopDuration, t =>
        {
            float e = EaseOutCubic(t);
            rt.localScale = Vector3.one * Mathf.Lerp(callPopScale, 1f, e);
        });

        if (callHoldDuration > 0f) yield return Tween(callHoldDuration, null);

        // Soft fade out with a gentle drift outward
        yield return Tween(callFadeDuration, t =>
        {
            group.alpha = 1f - EaseInQuad(t);
            rt.localScale = Vector3.one * Mathf.Lerp(1f, callFadeEndScale, t);
        });

        if (rt != null) Destroy(rt.gameObject);
    }

    // ----------------------------------------------------------------- Player

    /// <summary>
    /// Player hit flash: identical to <see cref="FlashCall"/> (size, placement, lean, pop, hold, fade)
    /// so the response reads like the call. Good/Perfect use the side tint; everything else goes
    /// grey and leans early/late, or keels over on a full miss / wrong side.
    /// </summary>
    public void FlashPlayer(GameObject iconPrefab, bool isRightBongo, Color tint, InputMatch.MatchQuality quality)
    {
        if (!CanSpawn(iconPrefab)) return;

        bool isGood = quality == InputMatch.MatchQuality.Perfect || quality == InputMatch.MatchQuality.Good;

        RectTransform rt = SpawnIcon(iconPrefab, isGood ? tint : missColor, out CanvasGroup group);
        rt.sizeDelta = new Vector2(callSize, callSize);

        Vector2 pos = new Vector2(isRightBongo ? callOffsetX : -callOffsetX, callOffsetY);
        rt.anchoredPosition = pos;
        lastPlayerPos = pos;
        lastPlayerRt = rt;
        hasPlayerPos = true;

        float tilt = isRightBongo ? -callTiltDegrees : callTiltDegrees;
        if (!isGood)
        {
            // Same language as the old slider dots: early leans one way, late the other, a full miss keels over.
            if (quality == InputMatch.MatchQuality.Miss || quality == InputMatch.MatchQuality.WrongSide)
                tilt = playerMissTilt;
            else
                tilt += quality == InputMatch.MatchQuality.TooEarly ? playerOffTimingTilt : -playerOffTimingTilt;
        }
        rt.localRotation = Quaternion.Euler(0f, 0f, tilt);

        StartCoroutine(AnimateCall(rt, group));
    }

    /// <summary>Spin-up star centred BEHIND the most recent player flash (a big background stamp), then fade.</summary>
    public void FlashPerfectStar(GameObject starPrefab)
    {
        if (!CanSpawn(starPrefab)) return;

        GameObject star = Instantiate(starPrefab, layer);
        DisableRaycasts(star);
        CanvasGroup group = star.GetComponent<CanvasGroup>();
        if (group == null) group = star.AddComponent<CanvasGroup>();

        Vector2 basePos = hasPlayerPos ? lastPlayerPos : new Vector2(0f, callOffsetY);
        if (star.TryGetComponent(out RectTransform rt))
        {
            rt.anchoredPosition = basePos + Vector2.up * starOffsetY;
            float size = callSize * starSizeFactor;
            rt.sizeDelta = new Vector2(size, size);
            rt.localScale = Vector3.zero;

            // Render behind the player's disc: slot the star just below the flash in sibling order
            // (UI draws later siblings on top). Falls back to the very back if the flash is gone.
            if (lastPlayerRt != null && lastPlayerRt.parent == layer)
                rt.SetSiblingIndex(lastPlayerRt.GetSiblingIndex());
            else
                rt.SetAsFirstSibling();

            StartCoroutine(AnimateStar(rt, group));
        }
    }

    private IEnumerator AnimateStar(RectTransform rt, CanvasGroup group)
    {
        group.alpha = 1f;
        float total = Mathf.Max(0.0001f, starGrowDuration + starFadeDuration);

        // Gentle eased turn across the whole life: fast at first, settling as it fades.
        void Turn(float lifeSeconds) =>
            rt.localRotation = Quaternion.Euler(0f, 0f, starSpinDegrees * EaseOutCubic(Mathf.Clamp01(lifeSeconds / total)));

        // Pop in: small → normal, alongside the disc's pop.
        yield return Tween(starGrowDuration, t =>
        {
            rt.localScale = Vector3.one * Mathf.Lerp(starStartScale, 1f, EaseOutCubic(t));
            Turn(t * starGrowDuration);
        });

        // Fade straight away (no hold) so the star is gone before the disc finishes.
        yield return Tween(starFadeDuration, t =>
        {
            Turn(starGrowDuration + t * starFadeDuration);
            group.alpha = 1f - EaseInQuad(t);
            rt.localScale = Vector3.one * Mathf.Lerp(1f, starFadeEndScale, t);
        });

        if (rt != null) Destroy(rt.gameObject);
    }

    // ---------------------------------------------------------------- Shared

    /// <summary>Stop every in-flight flash and remove its object. Called on cleanup / reset.</summary>
    public void Clear()
    {
        StopAllCoroutines();
        hasPlayerPos = false;
        lastPlayerRt = null;
        if (layer == null) layer = (RectTransform)transform;
        for (int i = layer.childCount - 1; i >= 0; i--)
            Destroy(layer.GetChild(i).gameObject);
    }

    private bool CanSpawn(GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogWarning("[BongoBeatFlash] No prefab supplied for flash.");
            return false;
        }
        // Coroutines need an active object; if GameplayElements is hidden there is nothing to show anyway.
        return isActiveAndEnabled;
    }

    /// <summary>
    /// Instantiate the Indicator prefab under the layer and strip the slider-dot behaviours
    /// (spawn-pop, metronome wobble, bounce) so this component owns the whole animation.
    /// </summary>
    private RectTransform SpawnIcon(GameObject prefab, Color tint, out CanvasGroup group)
    {
        GameObject go = Instantiate(prefab, layer);

        foreach (var b in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (b is UIQuickSpawnEffect || b is UIRotateWobble || b is BounceOnBeat)
            {
                b.StopAllCoroutines();
                b.enabled = false;
                Destroy(b);
            }
        }

        DisableRaycasts(go);

        if (go.TryGetComponent(out Image img)) img.color = tint;

        group = go.GetComponent<CanvasGroup>();
        if (group == null) group = go.AddComponent<CanvasGroup>();

        RectTransform rt = (RectTransform)go.transform;
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;
        return rt;
    }

    private static void DisableRaycasts(GameObject go)
    {
        foreach (var g in go.GetComponentsInChildren<Graphic>(true))
            g.raycastTarget = false;
    }

    /// <summary>Runs <paramref name="step"/> with t in [0,1] over <paramref name="duration"/> seconds of scaled time (freezes on pause).</summary>
    private static IEnumerator Tween(float duration, System.Action<float> step)
    {
        if (duration <= 0f)
        {
            step?.Invoke(1f);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            step?.Invoke(Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        step?.Invoke(1f);
    }

    private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
    private static float EaseInQuad(float t) => t * t;

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
