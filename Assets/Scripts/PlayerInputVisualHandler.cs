using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerInputVisualHandler : MonoBehaviour
{
    [Header("UI References")]
    public Slider inputSlider;
    public RectTransform indicatorParent;
    public GameObject inputIndicatorPrefab;

    [Header("Metronome Reference")]
    public Metronome metronome;

    [Header("Beat Generator Reference")]
    public BeatGenerator beatGenerator;

    private double beatDuration;
    private double barDuration;
    private double fullLoopDuration;
    private double fullLoopStartDspTime;
    public int fullLoopBeats = 8;

    private bool paused = false;

    private double VirtualDspTime => metronome != null ? metronome.VirtualDspTime : AudioSettings.dspTime;

    [Header("Lead-in")]
    [Tooltip("How many beats early the handle begins sliding in")]
    [Range(1, 3)] public int leadInBeats = 1;

    [Tooltip("Extra X-offset (px) to park the handle fully off-screen")]
    public float offScreenPadding = 10f;

    private RectTransform handleRect;
    private Vector2 readyPos;
    private Vector2 parkedPos;

    private bool isFrozen = false;

    private void Start()
    {
        if (metronome == null)
        {
            Debug.LogError("PlayerInputVisualHandler: Metronome reference is missing!");
            enabled = false;
            return;
        }

        InitializeBeatValues();
        CacheHandlePositions();

        // Subscribe to final bar event
        if (beatGenerator != null)
        {
            beatGenerator.OnFinalBarComplete += FreezeVisuals;
            Debug.Log("PlayerInputVisualHandler: Subscribed to OnFinalBarComplete");
        }
        else
        {
            Debug.LogError("PlayerInputVisualHandler: BeatGenerator reference is missing! Assign it in Inspector.");
        }
    }

    private void OnDisable()
    {
        if (beatGenerator != null)
        {
            beatGenerator.OnFinalBarComplete -= FreezeVisuals;
        }
    }

    public void FreezeVisuals()
    {
        if (isFrozen)
        {
            Debug.LogWarning("PlayerInputVisualHandler: Already frozen!");
            return;
        }

        isFrozen = true;
        Debug.Log("PlayerInputVisualHandler: FROZEN");
    }

    private void InitializeBeatValues()
    {
        beatDuration = 60.0 / metronome.bpm;
        barDuration = 4 * beatDuration;
        fullLoopDuration = fullLoopBeats * beatDuration;
        fullLoopStartDspTime = VirtualDspTime;
    }

    private void CacheHandlePositions()
    {
        inputSlider.value = 0f;
        handleRect = inputSlider.handleRect;
        readyPos = handleRect.anchoredPosition;

        float sliderWidth = inputSlider.GetComponent<RectTransform>().rect.width;
        parkedPos = readyPos + Vector2.left * (sliderWidth + offScreenPadding);

        handleRect.anchoredPosition = parkedPos;
    }

    private void Update()
    {
        if (paused) return;
        if (isFrozen) return;

        double currentTime = VirtualDspTime;
        double elapsedLoop = currentTime - fullLoopStartDspTime;

        if (elapsedLoop >= fullLoopDuration)
        {
            fullLoopStartDspTime += fullLoopDuration;
            elapsedLoop = currentTime - fullLoopStartDspTime;
        }

        double leadInStart = barDuration - (beatDuration * leadInBeats);

        if (elapsedLoop < leadInStart)
        {
            handleRect.anchoredPosition = parkedPos;
            inputSlider.value = 0f;
            return;
        }

        if (elapsedLoop < barDuration)
        {
            double leadElapsed = elapsedLoop - leadInStart;
            float t = (float)(leadElapsed / (beatDuration * leadInBeats));
            handleRect.anchoredPosition = Vector2.Lerp(parkedPos, readyPos, t);
            inputSlider.value = 0f;
            return;
        }

        double elapsedBar = elapsedLoop - barDuration;
        float progress = (float)(elapsedBar / barDuration);
        inputSlider.value = progress;
    }

    public void SpawnInputIndicator(bool isRightBongo)
    {
        if (!inputIndicatorPrefab || !indicatorParent || !inputSlider)
        {
            Debug.LogWarning("PlayerInputVisualHandler is missing required references.");
            return;
        }

        GameObject indicator = Instantiate(inputIndicatorPrefab, indicatorParent);
        if (indicator.TryGetComponent(out Image img))
            img.color = isRightBongo ? Color.red : Color.green;

        if (indicator.TryGetComponent(out RectTransform rt))
            rt.anchoredPosition = new Vector2(GetCurrentSliderXPosition(), 0f);
    }

    public float GetCurrentSliderXPosition()
    {
        float parentWidth = indicatorParent.rect.width;
        return (inputSlider.value * parentWidth) - (parentWidth / 2f);
    }

    public void ResetVisuals()
    {
        if (!indicatorParent) return;

        for (int i = indicatorParent.childCount - 1; i >= 0; i--)
            Destroy(indicatorParent.GetChild(i).gameObject);
    }

    public void OnPause()
    {
        if (paused) return;

        paused = true;
    }

    public void OnResume()
    {
        if (!paused) return;

        paused = false;
    }

    public void CleanupAndDisable()
    {
        Debug.Log("[PlayerInputVisualHandler] Cleaning up before disable");

        // Stop any ongoing transitions
        StopAllCoroutines();

        // Remove all spawned indicators
        ResetVisuals();

        // Reset slider to start
        if (inputSlider != null)
        {
            inputSlider.value = 0f;
        }

        // Reset handle position to parked (off-screen)
        if (handleRect != null)
        {
            handleRect.anchoredPosition = parkedPos;
        }

        // Disable the component
        enabled = false;

        Debug.Log("[PlayerInputVisualHandler] Cleanup complete");
    }

    public void ResetToInitialState()
    {
        Debug.Log("[PlayerInputVisualHandler] Resetting to initial state");

        // Stop everything
        StopAllCoroutines();

        // Remove all indicators
        ResetVisuals();

        // Reset slider
        if (inputSlider != null)
        {
            inputSlider.value = 0f;
        }

        // Reset handle to parked position
        if (handleRect != null)
        {
            CacheHandlePositions();  // Recalculate positions
            handleRect.anchoredPosition = parkedPos;
        }

        // Reset state
        isFrozen = false;
        paused = false;
        fullLoopStartDspTime = VirtualDspTime;

        // Re-initialize timing
        if (metronome != null)
        {
            InitializeBeatValues();
        }

        Debug.Log("[PlayerInputVisualHandler] Reset complete");
    }
}