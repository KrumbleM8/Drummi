using UnityEngine;
using UnityEngine.UI;

public class TorchFlicker : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Image image;

    [Header("Opacity Range")]
    [SerializeField] private float minAlpha = 0.6f;
    [SerializeField] private float maxAlpha = 1.0f;

    [Header("Flicker Speed")]
    [SerializeField] private float minInterval = 0.04f;
    [SerializeField] private float maxInterval = 0.15f;

    [Header("Smoothing")]
    [SerializeField] private float lerpSpeed = 12f;

    private float targetAlpha;
    private float timer;
    private float currentInterval;

    private void Awake()
    {
        if (image == null)
            image = GetComponent<Image>();

        targetAlpha = maxAlpha;
        PickNextTarget();
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (timer >= currentInterval)
        {
            PickNextTarget();
            timer = 0f;
        }

        Color c = image.color;
        c.a = Mathf.Lerp(c.a, targetAlpha, Time.deltaTime * lerpSpeed);
        image.color = c;
    }

    private void PickNextTarget()
    {
        targetAlpha = Random.Range(minAlpha, maxAlpha);
        currentInterval = Random.Range(minInterval, maxInterval);
    }
}