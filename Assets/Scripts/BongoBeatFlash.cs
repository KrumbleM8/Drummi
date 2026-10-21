using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bongo "beat flash" HUD. Replaces the slider-bar beat/input dots with
/// short-lived flashes on a full-canvas layer:
///
///   • Custard's call beat  → the Indicator icon pops up LARGE near screen
///     centre, nudged left/right for the bongo side, then soft-fades out.
///   • Player hit           → a SMALL icon rises from just above the hit-side
///     bongo and fades (grey + tilted + dropping on a miss), so player feedback
///     reads differently from Custard's call.
///   • Perfect hit          → the PerfectStar prefab spins up over the player
///     flash and fades.
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

    [Header("Player hit feedback (small, above the bongos)")]
    public float playerSize = 96f;
    [Tooltip("Horizontal position; left hits at -X, right hits at +X.")]
    public float playerOffsetX = 200f;
    [Tooltip("Vertical offset from the layer centre (negative = toward the bongos).")]
    public float playerOffsetY = -250f;
    [Tooltip("How far a good/perfect hit floats upward while fading.")]
    public float playerRise = 70f;
    public float playerPopDuration = 0.06f;
    public float playerFadeDuration = 0.3f;
    [Tooltip("How far a miss sinks while fading.")]
    public float playerMissDrop = 40f;
    [Tooltip("Tint used for missed / wrong-side / off-timing hits.")]
    public Color missColor = Color.gray;

    [Header("Perfect star")]
    public float starOffsetY = 60f;
    public float starGrowDuration = 0.17f;
    public float starShrinkDuration = 0.12f;
    public float starPeakScale = 1.2f;
    public float starRestingScale = 0.8f;
    public float starFadeDuration = 0.45f;

    private RectTransform layer;
    private Vector2 lastPlayerPos;
    private bool hasPlayerPos;

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

    /// <summary>Small feedback flash for a player hit. Good/Perfect rise, everything else sinks grey and tilted.</summary>
    public void FlashPlayer(GameObject iconPrefab, bool isRightBongo, Color tint, InputMatch.MatchQuality quality)
    {
        if (!CanSpawn(iconPrefab)) return;

        bool isGood = quality == InputMatch.MatchQuality.Perfect || quality == InputMatch.MatchQuality.Good;

        RectTransform rt = SpawnIcon(iconPrefab, isGood ? tint : missColor, out CanvasGroup group);
        rt.sizeDelta = new Vector2(playerSize, playerSize);

        Vector2 start = new Vector2(isRightBongo ? playerOffsetX : -playerOffsetX, playerOffsetY);
        rt.anchoredPosition = start;
        lastPlayerPos = start;
        hasPlayerPos = true;

        float tilt = 0f;
        if (!isGood)
        {
            // Same language as the old slider dots: early leans one way, late the other, a full miss keels over.
            tilt = quality == InputMatch.MatchQuality.TooEarly ? 45f : -45f;
            if (quality == InputMatch.MatchQuality.Miss || quality == InputMatch.MatchQuality.WrongSide)
                tilt = 90f;
        }
        rt.localRotation = Quaternion.Euler(0f, 0f, tilt);

        StartCoroutine(AnimatePlayer(rt, group, start, isGood));
    }

    private IEnumerator AnimatePlayer(RectTransform rt, CanvasGroup group, Vector2 start, bool isGood)
    {
        group.alpha = 1f;

        if (isGood)
        {
            // Quick pop: small → normal
            yield return Tween(playerPopDuration, t =>
            {
                rt.localScale = Vector3.one * Mathf.Lerp(0.75f, 1f, EaseOutBack(t));
            });

            Vector2 end = start + Vector2.up * playerRise;
            yield return Tween(playerFadeDuration, t =>
            {
                rt.anchoredPosition = Vector2.Lerp(start, end, EaseOutCubic(t));
                group.alpha = 1f - EaseInQuad(t);
            });
        }
        else
        {
            rt.localScale = Vector3.one;
            Vector2 end = start + Vector2.down * playerMissDrop;
            yield return Tween(playerFadeDuration, t =>
            {
                rt.anchoredPosition = Vector2.Lerp(start, end, EaseInQuad(t));
                rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.85f, t);
                group.alpha = 1f - EaseInQuad(t);
            });
        }

        if (rt != null) Destroy(rt.gameObject);
    }

    /// <summary>Spin-up star over the most recent player flash, then fade.</summary>
    public void FlashPerfectStar(GameObject starPrefab)
    {
        if (!CanSpawn(starPrefab)) return;

        GameObject star = Instantiate(starPrefab, layer);
        DisableRaycasts(star);
        CanvasGroup group = star.GetComponent<CanvasGroup>();
        if (group == null) group = star.AddComponent<CanvasGroup>();

        Vector2 basePos = hasPlayerPos ? lastPlayerPos : new Vector2(0f, playerOffsetY);
        if (star.TryGetComponent(out RectTransform rt))
        {
            rt.anchoredPosition = basePos + Vector2.up * starOffsetY;
            rt.localScale = Vector3.zero;
            StartCoroutine(AnimateStar(rt, group));
        }
    }

    private IEnumerator AnimateStar(RectTransform rt, CanvasGroup group)
    {
        group.alpha = 1f;
        float totalSpin = 360f;
        float popTotal = starGrowDuration + starShrinkDuration;

        yield return Tween(popTotal, t =>
        {
            float elapsed = t * popTotal;
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, totalSpin, t));

            float scale;
            if (elapsed < starGrowDuration)
                scale = Mathf.Lerp(0f, starPeakScale, EaseOutBack(elapsed / starGrowDuration));
            else
                scale = Mathf.Lerp(starPeakScale, starRestingScale, EaseInQuad((elapsed - starGrowDuration) / starShrinkDuration));
            rt.localScale = Vector3.one * scale;
        });

        rt.localScale = Vector3.one * starRestingScale;

        yield return Tween(starFadeDuration, t =>
        {
            group.alpha = 1f - EaseInQuad(t);
        });

        if (rt != null) Destroy(rt.gameObject);
    }

    // ---------------------------------------------------------------- Shared

    /// <summary>Stop every in-flight flash and remove its object. Called on cleanup / reset.</summary>
    public void Clear()
    {
        StopAllCoroutines();
        hasPlayerPos = false;
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
