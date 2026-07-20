using System;
using System.Collections.Generic;
using UnityEngine;
using static InputMatch;

public class BongoModeInputReader : MonoBehaviour
{
    [SerializeField] private DrumPadTouch drumPadTouch;

    // Controlled by BeatGenerator — true during free play and response windows.
    // Property wraps a backing field purely to fire AllowInputChanged on transitions;
    // gating semantics are identical to the previous public field.
    public bool allowInput
    {
        get => _allowInput;
        set
        {
            if (_allowInput == value) return;
            _allowInput = value;
            AllowInputChanged?.Invoke(value);
        }
    }
    private bool _allowInput = true;

    /// <summary>
    /// Fired whenever allowInput transitions (false↔true). Purely observational —
    /// subscribers (e.g. DrumPadStateVisual) react cosmetically and must never
    /// drive input logic from this event.
    /// </summary>
    public event Action<bool> AllowInputChanged;

    public Metronome metronome;
    public List<BongoInput> playerInputData = new List<BongoInput>();
    public PlayerInputVisualHandler playerInputVisualScheduler;

    // Used for logging inputs as they are pressed:
    public BeatEvaluator beatEvaluator;

    private void Awake()
    {
        beatEvaluator = GetComponent<BeatEvaluator>();
    }

    private void OnEnable()
    {
        if (drumPadTouch != null)
        {
            drumPadTouch.OnLeftHit += OnLeft;
            drumPadTouch.OnRightHit += OnRight;
        }
        else
        {
            Debug.LogError("[BongoModeInputReader] DrumPadTouch not assigned!");
        }
    }

    private void OnDisable()
    {
        if (drumPadTouch != null)
        {
            drumPadTouch.OnLeftHit -= OnLeft;
            drumPadTouch.OnRightHit -= OnRight;
        }
    }

    private void OnLeft() => TriggerInput(false);
    private void OnRight() => TriggerInput(true);

    public void TriggerInput(bool isRightBongo)
    {
        // Always play animation and sound (for player feedback)
        // Inverted: this scene's bongo rig needs the opposite tilt direction
        BongoAnimator.instance.PlayBongoAnimation(!isRightBongo);

        if (isRightBongo)
            AudioManager.instance?.PlayBongoRight();
        else
            AudioManager.instance?.PlayBongoLeft();

        // Early exit if input not allowed or game paused
        if (!allowInput || GameClock.Instance.IsPaused)
            return;

        // Record input using GameClock time (not raw DSP time)
        var input = new BongoInput(GameClock.Instance.GameTime, isRightBongo);
        playerInputData.Add(input);

        var result = beatEvaluator.EvaluateSingleInput(input);
        switch (result)
        {
            case InputMatch.MatchQuality.Perfect:
                playerInputVisualScheduler.SpawnInputIndicator(isRightBongo);
                playerInputVisualScheduler.SpawnPerfectInputStar();
                break;
            case InputMatch.MatchQuality.Good:
                playerInputVisualScheduler?.SpawnInputIndicator(isRightBongo);
                break;
            default:
                playerInputVisualScheduler.SpawnMissedInputIndicator(isRightBongo, result);
                break;
        }

        // Note: Evaluation happens later in BeatGenerator.EvaluateBar()
    }

    public void ResetInputs()
    {
        playerInputData.Clear();
    }
}
