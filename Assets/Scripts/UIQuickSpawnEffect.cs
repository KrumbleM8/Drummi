using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(RectTransform))]
public class UIQuickSpawnEffect : MonoBehaviour
{
    [Header("Animation Settings")]
    public float growScale = 1.3f;        // how big it grows
    public float growDuration = 0.15f;    // time to grow
    public float recoilScale = 0.9f;      // recoil smaller than original
    public float recoilDuration = 0.1f;   // time to recoil
    public float settleDuration = 0.1f;   // time to return to normal
    public float rotateAngle = 20f;       // max rotation in degrees

    private RectTransform rect;
    private Vector3 originalScale;
    private Quaternion originalRotation;

    public BounceOnBeat bouncerRef;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        originalScale = rect.localScale;
        originalRotation = rect.localRotation;
    }

    private void OnEnable()
    {
        // Reset immediately
        rect.localScale = originalScale;
        rect.localRotation = originalRotation;

        // Pick random rotation direction
        float angle = Random.value > 0.5f ? rotateAngle : -rotateAngle;
        Quaternion targetRot = Quaternion.Euler(0, 0, angle);

        // Play the sequence
        StopAllCoroutines();
        StartCoroutine(PlayEffect(targetRot));
    }

    private IEnumerator PlayEffect(Quaternion targetRotation)
    {
        // Grow + rotate
        yield return Animate(rect.localScale, originalScale * growScale,
                             rect.localRotation, targetRotation, growDuration);

        // Recoil (shrink back smaller, reset rotation a bit)
        yield return Animate(rect.localScale, originalScale * recoilScale,
                             rect.localRotation, originalRotation, recoilDuration);

        // Return to normal
        yield return Animate(rect.localScale, originalScale,
                             rect.localRotation, originalRotation, settleDuration);

        bouncerRef.enabled = true;
    }

    private IEnumerator Animate(Vector3 fromScale, Vector3 toScale,
                                Quaternion fromRot, Quaternion toRot,
                                float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(t / duration);

            rect.localScale = Vector3.Lerp(fromScale, toScale, normalized);
            rect.localRotation = Quaternion.Lerp(fromRot, toRot, normalized);

            yield return null;
        }
    }
}