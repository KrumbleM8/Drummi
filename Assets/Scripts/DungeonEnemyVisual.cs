using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Purely visual world-space enemy sprite spawned alongside each beat indicator.
/// Managed by an object pool in DungeonVisualController — never Instantiated/Destroyed
/// after the initial pre-warm.
///
/// Animations:
///   Spawn     — fade 0→1 + slide down, ease-out, ~0.35 s
///   Hit       — tilt to random Z + fade 1→0, linear, ~0.175 s  (Scenario A)
///   Bar-end   — slide upward + fade 1→0, ease-out, ~0.35 s      (Scenario B)
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class DungeonEnemyVisual : MonoBehaviour
{
    [SerializeField] private float maxTiltAngle = 35f;

    private const float SpawnDuration      = 0.35f;
    private const float HitDespawnDuration = 0.175f;
    private const float BarEndDuration     = 0.35f;
    private const float YOffset            = 2.5f;  // world units above final pos

    public DungeonEnemyType EnemyType       { get; private set; }
    public GameObject       PairedIndicator { get; private set; }

    /// <summary>True while the object is idle in the pool; prevents stale callbacks.</summary>
    public bool InPool { get; set; } = true;

    private SpriteRenderer _sr;
    private Coroutine      _activeCoroutine;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Configure and play the spawn animation. Called by the pool controller.</summary>
    public void Init(Sprite sprite, Color color, Vector3 finalWorldPos, DungeonEnemyType type, GameObject pairedIndicator)
    {
        EnemyType       = type;
        PairedIndicator = pairedIndicator;

        _sr        = GetComponent<SpriteRenderer>();
        _sr.sprite = sprite;
        _sr.color  = new Color(color.r, color.g, color.b, 0f);

        transform.rotation = Quaternion.identity;
        transform.position = finalWorldPos + new Vector3(0f, YOffset, 0f);

        StartAnim(SpawnAnim(finalWorldPos, color));
    }

    /// <summary>
    /// Scenario A — hit despawn: tilt + linear fade, ~0.175 s.
    /// onComplete is called when the animation finishes so the pool can reclaim the object.
    /// </summary>
    public void DespawnHit(Action onComplete)
    {
        StartAnim(HitAnim(onComplete));
    }

    /// <summary>
    /// Scenario B — bar-end despawn: slide up + ease-out fade, ~0.35 s.
    /// onComplete is called when the animation finishes so the pool can reclaim the object.
    /// </summary>
    public void DespawnBarEnd(Action onComplete)
    {
        StartAnim(BarEndAnim(onComplete));
    }

    /// <summary>
    /// Stop any running animation and reset visual state.
    /// Must be called by the pool before the object is deactivated.
    /// </summary>
    public void StopAndReset()
    {
        if (_activeCoroutine != null)
        {
            StopCoroutine(_activeCoroutine);
            _activeCoroutine = null;
        }

        transform.rotation = Quaternion.identity;
        if (_sr != null) _sr.color = Color.clear;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void StartAnim(IEnumerator anim)
    {
        if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
        _activeCoroutine = StartCoroutine(anim);
    }

    private IEnumerator SpawnAnim(Vector3 finalPos, Color color)
    {
        Vector3 startPos = finalPos + new Vector3(0f, YOffset, 0f);
        Color   c        = color;
        float   elapsed  = 0f;

        while (elapsed < SpawnDuration)
        {
            float t    = Mathf.Clamp01(elapsed / SpawnDuration);
            float ease = 1f - (1f - t) * (1f - t); // quadratic ease-out

            transform.position = Vector3.Lerp(startPos, finalPos, ease);
            c.a                = ease;
            _sr.color          = c;

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = finalPos;
        c.a                = 1f;
        _sr.color          = c;
        _activeCoroutine   = null;
    }

    private IEnumerator HitAnim(Action onComplete)
    {
        float targetAngle = maxTiltAngle * (UnityEngine.Random.value < 0.5f ? 1f : -1f);
        Color c           = _sr.color;
        float startAlpha  = c.a;
        float elapsed     = 0f;

        while (elapsed < HitDespawnDuration)
        {
            float t = Mathf.Clamp01(elapsed / HitDespawnDuration); // linear — abrupt feel

            transform.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(0f, targetAngle, t));
            c.a                        = Mathf.Lerp(startAlpha, 0f, t);
            _sr.color                  = c;

            elapsed += Time.deltaTime;
            yield return null;
        }

        c.a              = 0f;
        _sr.color        = c;
        _activeCoroutine = null;
        onComplete?.Invoke();
    }

    private IEnumerator BarEndAnim(Action onComplete)
    {
        Vector3 startPos   = transform.position;
        Vector3 endPos     = startPos + new Vector3(0f, YOffset, 0f);
        Color   c          = _sr.color;
        float   startAlpha = c.a;
        float   elapsed    = 0f;

        while (elapsed < BarEndDuration)
        {
            float t    = Mathf.Clamp01(elapsed / BarEndDuration);
            float ease = 1f - (1f - t) * (1f - t); // ease-out mirrors spawn

            transform.position = Vector3.Lerp(startPos, endPos, ease);
            c.a                = Mathf.Lerp(startAlpha, 0f, t);
            _sr.color          = c;

            elapsed += Time.deltaTime;
            yield return null;
        }

        c.a              = 0f;
        _sr.color        = c;
        _activeCoroutine = null;
        onComplete?.Invoke();
    }
}
