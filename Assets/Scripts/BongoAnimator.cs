using UnityEngine;
using System.Collections;

public class BongoAnimator : MonoBehaviour
{
    public static BongoAnimator instance;

    [Header("Animation Settings")]
    public float scaleAmount = 1.2f;
    public float scaleDuration = 0.15f;
    public float rotationAmount = 25f;
    public float rotationDuration = 0.2f;

    public float rangeMultiplier = 1.133f;

    [Header("Auto-Subscribe (optional)")]
    [Tooltip("Assign in scenes where BongoAnimator should subscribe to DrumPadTouch events " +
             "directly (e.g. Dungeon BongoRig). Leave null in Bongo scene — BongoModeInputReader " +
             "calls PlayBongoAnimation() externally instead.")]
    [SerializeField] private DrumPadTouch drumPadTouch;

    private RectTransform rectTransform;
    private Coroutine animationRoutine;

    private readonly Vector3 neutralScale = Vector3.one;
    private readonly Quaternion neutralRotation = Quaternion.identity;

    private void Awake()
    {
        instance = this;
        rectTransform = GetComponent<RectTransform>();
        ResetToNeutral();
    }

    private void OnEnable()
    {
        if (drumPadTouch != null)
        {
            drumPadTouch.OnLeftHit  += OnLeftHit;
            drumPadTouch.OnRightHit += OnRightHit;
        }
    }

    private void OnDisable()
    {
        if (drumPadTouch != null)
        {
            drumPadTouch.OnLeftHit  -= OnLeftHit;
            drumPadTouch.OnRightHit -= OnRightHit;
        }
        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
            ResetToNeutral();
        }
    }

    // Auto-subscribe path (Dungeon BongoRig): this rig needs the opposite
    // mapping from the Bongo scene, hence left → false / right → true.
    private void OnLeftHit()  => PlayBongoAnimation(isLeftSide: false);
    private void OnRightHit() => PlayBongoAnimation(isLeftSide: true);

    /// <summary>
    /// Play the bongo animation. The parameter picks the tilt direction
    /// (true = negative Z rotation); which side that reads as visually
    /// depends on the scene's bongo rig, so callers tune it per scene.
    /// </summary>
    public void PlayBongoAnimation(bool isLeftSide)
    {
        if (animationRoutine != null)
            StopCoroutine(animationRoutine);

        ResetToNeutral(); // Ensures consistent start
        animationRoutine = StartCoroutine(Animate(isLeftSide));
    }

    private IEnumerator Animate(bool isLeftSide)
    {
        float direction = isLeftSide ? -1f : 1f;

        float newScaleAmount = Random.Range(scaleAmount, scaleAmount * rangeMultiplier); ;
        float newRotationAmount = Random.Range(rotationAmount, rotationAmount * rangeMultiplier);

        Vector3 targetScale = neutralScale * newScaleAmount;
        Quaternion targetRotation = Quaternion.Euler(0, 0, direction * newRotationAmount);

        // Grow and rotate
        float timer = 0f;
        while (timer < scaleDuration)
        {
            float t = timer / scaleDuration;
            rectTransform.localScale = Vector3.Lerp(neutralScale, targetScale, t);
            rectTransform.localRotation = Quaternion.Lerp(neutralRotation, targetRotation, t);
            timer += Time.unscaledDeltaTime; // UI-safe timing
            yield return null;
        }

        // Shrink and return
        timer = 0f;
        while (timer < rotationDuration)
        {
            float t = timer / rotationDuration;
            rectTransform.localScale = Vector3.Lerp(targetScale, neutralScale, t);
            rectTransform.localRotation = Quaternion.Lerp(targetRotation, neutralRotation, t);
            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        ResetToNeutral();
        animationRoutine = null;
    }

    private void ResetToNeutral()
    {
        rectTransform.localScale = neutralScale;
        rectTransform.localRotation = neutralRotation;
    }
}
