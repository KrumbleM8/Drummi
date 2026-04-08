using UnityEngine;

/// <summary>
/// Detects player input for Left and Right lanes via DrumPadTouch (hitbox-based touch
/// and keyboard) and forwards hits to HitJudge. Delegates all input detection to
/// DrumPadTouch so that touch and keyboard behaviour is identical to BongoMode.
///
/// Place on the same GameObject as HitJudge.
/// Assign the scene's DrumPadTouch in the Inspector.
/// </summary>
[RequireComponent(typeof(HitJudge))]
public class ArcadeInputHandler : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────

    [SerializeField] private DrumPadTouch drumPadTouch;

    // ── Private ───────────────────────────────────────────────────────────

    private HitJudge _hitJudge;
    private HitZoneManager _hitZoneManager;

    // ── Unity ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _hitJudge = GetComponent<HitJudge>();
        _hitZoneManager = FindFirstObjectByType<HitZoneManager>(); // optional

        if (drumPadTouch == null)
            Debug.LogError("[ArcadeInputHandler] DrumPadTouch not assigned!");
    }

    void OnEnable()
    {
        if (drumPadTouch != null)
        {
            drumPadTouch.OnLeftHit += OnLeft;
            drumPadTouch.OnRightHit += OnRight;
        }
    }

    void OnDisable()
    {
        if (drumPadTouch != null)
        {
            drumPadTouch.OnLeftHit -= OnLeft;
            drumPadTouch.OnRightHit -= OnRight;
        }
    }

    // ── Private ───────────────────────────────────────────────────────────

    private void OnLeft() => FireLane(Lane.Left);
    private void OnRight() => FireLane(Lane.Right);

    private void FireLane(Lane lane)
    {
        // Always play animation and sound (for player feedback), matching BongoModeInputReader behaviour
        bool isRight = lane == Lane.Right;
        BongoAnimator.instance.PlayBongoAnimation(isRight);
        if (isRight)
            AudioManager.instance?.PlayBongoRight();
        else
            AudioManager.instance?.PlayBongoLeft();

        // Instant press flash — before judgement so it feels responsive
        _hitZoneManager?.NotifyPress(lane);

        HitJudgement result = _hitJudge.Judge(lane);
        Debug.Log($"[RhythmInput] {lane} tapped → {result}");
    }
}
