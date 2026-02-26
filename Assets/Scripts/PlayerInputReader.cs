using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputReader : MonoBehaviour
{
    [SerializeField] private InputActionAsset actionMap;

    private InputAction leftAction;
    private InputAction rightAction;

    public bool allowInput = true;

    public Metronome metronome;
    public List<BongoInput> playerInputData = new List<BongoInput>();
    public PlayerInputVisualHandler playerInputVisualScheduler;

    //Used for logging inputs as they are pressed:
    public BeatEvaluator beatEvaluator;

    private void Awake()
    {
        if (actionMap != null)
        {
            leftAction = actionMap.FindAction("Left");
            rightAction = actionMap.FindAction("Right");
        }
        else
        {
            Debug.LogError("Input Action Map is not assigned!");
        }

        beatEvaluator = GetComponent<BeatEvaluator>();
    }

    private void OnEnable()
    {
        if (leftAction != null)
        {
            leftAction.Enable();
            leftAction.started += OnLeft;
        }

        if (rightAction != null)
        {
            rightAction.Enable();
            rightAction.started += OnRight;
        }
    }

    private void OnDisable()
    {
        if (leftAction != null)
        {
            leftAction.Disable();
            leftAction.started -= OnLeft;
        }

        if (rightAction != null)
        {
            rightAction.Disable();
            rightAction.started -= OnRight;
        }
    }

    private void OnLeft(InputAction.CallbackContext context)
    {
        TriggerInput(false);
    }

    private void OnRight(InputAction.CallbackContext context)
    {
        TriggerInput(true);
    }

    public void TriggerInput(bool isRightBongo)
    {
        // Always play animation and sound (for player feedback)
        BongoAnimator.instance.PlayBongoAnimation(isRightBongo);

        if (isRightBongo)
            AudioManager.instance?.PlayBongoRight();
        else
            AudioManager.instance?.PlayBongoLeft();

        // Early exit if input not allowed or game paused
        if (!allowInput || GameClock.Instance.IsPaused) // ← Changed from Time.timeScale == 0
            return;

        // Record input using GameClock time (not raw DSP time)
        var input = new BongoInput(GameClock.Instance.GameTime, isRightBongo); // ← Changed
        playerInputData.Add(input);

        // Check for Immediate Feedback
        //playerInputVisualScheduler?.SpawnInputIndicator(isRightBongo,beatEvaluator.EvaluateSingleInput(input));
        // ^ This might be more efficient

        switch (beatEvaluator.EvaluateSingleInput(input)) //TODO: FINISH THIS
        {
            case InputMatch.MatchQuality.Perfect:
                //Spawn a star behind the indicator, grow shrink and spin it, keep it 
                playerInputVisualScheduler?.SpawnInputIndicator(isRightBongo);
                break;
            case InputMatch.MatchQuality.Good:
                playerInputVisualScheduler?.SpawnInputIndicator(isRightBongo);
                break;
            default:
                //default = Miss, too late/early or wrong side.
                //Tilt indicator and disable it's bounce on beat.
                playerInputVisualScheduler?.SpawnInputIndicator(isRightBongo);
                break;          
        }

            // Note: Evaluation happens later in BeatGenerator.EvaluateBar()
        }

    public void ResetInputs()
    {
        playerInputData.Clear();
    }
}
