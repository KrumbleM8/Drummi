using UnityEngine;

public class BounceOnBeat : MonoBehaviour
{
    [Header("Metronome Reference")]
    public Metronome metronome;

    [Header("Bounce Settings")]
    public float baseBounceScaleY = 1.3f;
    public float bounceIntensity = 1.0f;

    [Header("Damping Settings")]
    public float smoothTime = 0.15f; // How quickly it returns to original
    public bool useCurve = false;
    public AnimationCurve bounceCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    private Vector3 originalScale;
    private float currentVelocity = 0f;
    private float currentYScale;
    private float bounceTimer = 0f;
    private float bounceDuration = 0.2f;
    private bool isBouncing = false;

    private void Start()
    {
        if (metronome != null)
        {
            metronome.OnTickEvent += TriggerBounce;
        }
        originalScale = transform.localScale;
        currentYScale = originalScale.y;
    }

    private void OnDestroy()
    {
        if (metronome != null)
        {
            metronome.OnTickEvent -= TriggerBounce;
        }
    }

    private void TriggerBounce()
    {
        if (metronome == null || metronome.IsPaused)
            return;

        currentYScale = baseBounceScaleY * bounceIntensity;
        isBouncing = true;
        bounceTimer = 0f;
    }

    private void Update()
    {
        bounceTimer += Time.deltaTime;

        if (useCurve)
        {
            float t = bounceTimer / bounceDuration;
            if (t >= 1f)
            {
                currentYScale = originalScale.y;
                isBouncing = false;
            }
            else
            {
                float curveValue = bounceCurve.Evaluate(t);
                float targetY = Mathf.Lerp(baseBounceScaleY * bounceIntensity, originalScale.y, curveValue);
                currentYScale = targetY;
            }
        }
        else
        {
            currentYScale = Mathf.SmoothDamp(currentYScale, originalScale.y, ref currentVelocity, smoothTime);
        }

        transform.localScale = new Vector3(originalScale.x, currentYScale, originalScale.z);
    }
}
