using UnityEngine;

public class EyeBlinker : MonoBehaviour
{
    [Tooltip("The GameObject (e.g., ClosedEyes Sprite) to toggle on/off")]
    public GameObject closedEyes;

    [Tooltip("Minimum time between blinks (seconds)")]
    public float minBlinkInterval = 2f;

    [Tooltip("Maximum time between blinks (seconds)")]
    public float maxBlinkInterval = 5f;

    [Tooltip("How long the eyes stay closed during a blink (seconds)")]
    public float blinkDuration = 0.1f;

    private void Start()
    {
        ScheduleNextBlink();
    }

    private void ScheduleNextBlink()
    {
        float nextBlinkIn = Random.Range(minBlinkInterval, maxBlinkInterval);
        Invoke(nameof(Blink), nextBlinkIn);
    }

    private void Blink()
    {
        if (closedEyes == null) return;

        closedEyes.SetActive(true);
        Invoke(nameof(EndBlink), blinkDuration);
    }

    private void EndBlink()
    {
        closedEyes.SetActive(false);
        ScheduleNextBlink();
    }

    private void OnDisable()
    {
        CancelInvoke();
    }
}
