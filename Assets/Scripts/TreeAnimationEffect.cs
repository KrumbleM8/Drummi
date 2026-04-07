using UnityEngine;

public class TreeAnimationEffect : MonoBehaviour
{
    [Header("Shake Settings")]
    public float animationDuration = 0.5f;
    public float shakeStrength = 0.1f;
    public float shakeFrequency = 25f;

    [Header("Rotation Settings")]
    public float rotationStrength = 5f; // degrees

    private Vector3 initialLocalPos;
    private Quaternion initialLocalRot;

    private float elapsedTime;
    private bool isAnimating;

    private void Awake()
    {
        initialLocalPos = transform.localPosition;
        initialLocalRot = transform.localRotation;
    }

    private void OnEnable()
    {
        ResetState();
    }

    private void OnDisable()
    {
        ResetState();
    }

    private void Update()
    {
        if (!isAnimating) return;

        elapsedTime += Time.deltaTime;

        if (elapsedTime >= animationDuration)
        {
            ResetState();
            return;
        }

        // Oscillation (same driver for both position + rotation)
        float wave = Mathf.Sin(elapsedTime * shakeFrequency);

        // Position (left-right)
        float offsetX = wave * shakeStrength;
        transform.localPosition = initialLocalPos + new Vector3(offsetX, 0f, 0f);

        // Rotation (tilt with movement)
        float angle = -wave * rotationStrength;
        transform.localRotation = initialLocalRot * Quaternion.Euler(0f, 0f, angle);
    }

    public void TriggerEffect()
    {
        elapsedTime = 0f;
        isAnimating = true;
    }

    private void ResetState()
    {
        isAnimating = false;
        elapsedTime = 0f;
        transform.localPosition = initialLocalPos;
        transform.localRotation = initialLocalRot;
    }
}