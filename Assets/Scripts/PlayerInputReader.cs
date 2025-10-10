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
        BongoAnimator.instance.PlayBongoAnimation(isRightBongo);

        if (isRightBongo)
            AudioManager.instance?.PlayBongoRight();
        else
            AudioManager.instance?.PlayBongoLeft();

        if (!allowInput || Time.timeScale == 0) return;

        var input = new BongoInput(AudioSettings.dspTime, isRightBongo);
        playerInputData.Add(input);

        playerInputVisualScheduler?.SpawnInputIndicator(isRightBongo);

        beatEvaluator.LogInput(input);
    }

    public void ResetInputs()
    {
        playerInputData.Clear();
    }
}
