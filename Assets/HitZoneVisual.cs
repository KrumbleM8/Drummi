using UnityEngine;

/// <summary>
/// Visual feedback for a single hit zone (one instance per lane).
/// Handles idle pulse, press flash, and per-judgement colour feedback.
///
/// Attach to the hit zone SpriteRenderer GameObject for each lane.
/// Wire RhythmInputHandler to call NotifyPress(lane) and
/// RhythmLaneManager.OnJudgement to call NotifyJudgement(lane, judgement).
///
/// SETUP:
///   1. Create two hit zone GameObjects (left + right).
///   2. Add HitZoneVisual to each.
///   3. Assign this instance's lane in the inspector.
///   4. Create a HitZoneManager (or wire from ArcadeGameController) to
///      subscribe to RhythmLaneManager.OnJudgement and call NotifyJudgement.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class HitZoneVisual : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────

    [Header("Lane")]
    public Lane lane;

    [Header("Colours")]
    public Color idleColour    = new Color(1f, 1f, 1f, 0.4f);
    public Color pressColour   = new Color(1f, 1f, 1f, 1.0f);
    public Color perfectColour = new Color(1f, 0.92f, 0.0f, 1.0f);  // Gold
    public Color goodColour    = new Color(0.3f, 0.8f, 1.0f, 1.0f); // Cyan
    public Color missColour    = new Color(1f, 0.2f, 0.2f, 1.0f);   // Red

    [Header("Timing")]
    [Tooltip("Seconds the press flash holds before fading back to idle.")]
    public float pressFlashSeconds    = 0.08f;

    [Tooltip("Seconds the judgement colour holds before fading back to idle.")]
    public float judgementHoldSeconds = 0.15f;

    [Tooltip("Seconds to fade from judgement/press colour back to idle.")]
    public float fadeSeconds          = 0.12f;

    // ── Private ───────────────────────────────────────────────────────────

    private SpriteRenderer _renderer;
    private Color  _targetColour;
    private Color  _fromColour;
    private float  _flashTimer;   // > 0 while in flash/hold phase
    private float  _fadeTimer;    // > 0 while fading back to idle
    private bool   _fading;

    // ── Unity ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _renderer    = GetComponent<SpriteRenderer>();
        _targetColour = idleColour;
        _renderer.color = idleColour;
    }

    void Update()
    {
        if (_flashTimer > 0f)
        {
            _flashTimer -= Time.deltaTime;
            if (_flashTimer <= 0f)
                BeginFade();
            return;
        }

        if (_fading)
        {
            _fadeTimer -= Time.deltaTime;
            float t = 1f - Mathf.Clamp01(_fadeTimer / fadeSeconds);
            _renderer.color = Color.Lerp(_fromColour, idleColour, t);
            if (_fadeTimer <= 0f)
            {
                _renderer.color = idleColour;
                _fading = false;
            }
        }
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>Call from RhythmInputHandler when this lane is tapped.</summary>
    public void NotifyPress()
    {
        Flash(pressColour, pressFlashSeconds);
    }

    /// <summary>Call from a judgement subscriber when this lane's note is judged.</summary>
    public void NotifyJudgement(HitJudgement judgement)
    {
        Color col = judgement switch
        {
            HitJudgement.Perfect => perfectColour,
            HitJudgement.Good    => goodColour,
            HitJudgement.Miss    => missColour,
            _                    => idleColour
        };
        Flash(col, judgementHoldSeconds);
    }

    // ── Private ───────────────────────────────────────────────────────────

    private void Flash(Color colour, float holdSeconds)
    {
        _renderer.color = colour;
        _fromColour     = colour;
        _flashTimer     = holdSeconds;
        _fading         = false;
    }

    private void BeginFade()
    {
        _fromColour = _renderer.color;
        _fadeTimer  = fadeSeconds;
        _fading     = true;
    }
}
