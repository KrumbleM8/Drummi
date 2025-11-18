using System.Collections.Generic;
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

        // Spawn visual indicator
        playerInputVisualScheduler?.SpawnInputIndicator(isRightBongo);

        // Note: Evaluation happens later in BeatGenerator.EvaluateBar()
        // No immediate feedback to keep it simple
    }

    public void ResetInputs()
    {
        playerInputData.Clear();
    }
}
