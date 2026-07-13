using UnityEngine;

/// <summary>
/// Bridges DrumPadTouch hit events to AudioManager bongo sound playback and BongoAnimator.
/// Use this in scenes where bongo input only needs to produce sound and animation (no scoring/evaluation).
/// The BongoAnimator is resolved from the same GameObject as the DrumPadTouch reference.
/// </summary>
public class BongoInputReader : MonoBehaviour
{
    [SerializeField] private DrumPadTouch drumPadTouch;

    private BongoAnimator _bongoAnimator;

    private void Awake()
    {
        if (drumPadTouch != null)
            _bongoAnimator = drumPadTouch.GetComponent<BongoAnimator>();
    }

    private void OnEnable()
    {
        if (drumPadTouch != null)
        {
            drumPadTouch.OnLeftHit  += OnLeft;
            drumPadTouch.OnRightHit += OnRight;
        }
        else
        {
            Debug.LogError("[BongoInputReader] DrumPadTouch not assigned!");
        }
    }

    private void OnDisable()
    {
        if (drumPadTouch != null)
        {
            drumPadTouch.OnLeftHit  -= OnLeft;
            drumPadTouch.OnRightHit -= OnRight;
        }
    }

    private void OnLeft()
    {
        _bongoAnimator?.PlayBongoAnimation(isLeftSide: true);
        AudioManager.instance?.PlayBongoLeft();
    }

    private void OnRight()
    {
        _bongoAnimator?.PlayBongoAnimation(isLeftSide: false);
        AudioManager.instance?.PlayBongoRight();
    }
}
