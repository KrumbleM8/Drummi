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

    [Header("Lead-in")]
    [Tooltip("How many beats early the handle begins sliding in")]
    [Range(1, 3)] public int leadInBeats = 1;

    [Tooltip("Extra X-offset (px) to park the handle fully off-screen")]
    public float offScreenPadding = 10f;

    private RectTransform handleRect;
    private Vector2 readyPos;
    private Vector2 parkedPos;

    private bool isFrozen = false;
    private bool hasInitialized = false;

    // In PlayerInputVisualHandler.cs - MODIFY OnEnable to NOT call ResetLoopStartTime
    private void OnEnable()
    {
        if (metronome == null)
        {
            Debug.LogError("PlayerInputVisualHandler: Metronome reference is missing!");
            enabled = false;
            return;
        }

        // DON'T call ResetLoopStartTime() here - timing will be synced explicitly
        isFrozen = false;

        if (inputSlider != null)
        {
            inputSlider.value = 0f;
        }

        if (hasInitialized && handleRect != null)
        {
            handleRect.anchoredPosition = parkedPos;
        }

        if (beatGenerator != null)
        {
            beatGenerator.OnFinalBarComplete -= FreezeVisuals;
            beatGenerator.OnFinalBarComplete += FreezeVisuals;

            if (hasInitialized)
            {
                Debug.Log("PlayerInputVisualHandler: Subscribed to OnFinalBarComplete");
            }
        }
        else
        {
            Debug.LogError("PlayerInputVisualHandler: BeatGenerator reference is missing! Assign it in Inspector.");
        }
    }

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
        hasInitialized = true;

        if (beatGenerator != null)
        {
            Debug.Log("PlayerInputVisualHandler: Subscribed to OnFinalBarComplete");
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
        fullLoopStartDspTime = AudioSettings.dspTime;
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
        if (GameClock.Instance.IsPaused) return;
        if (isFrozen) return;

        double currentTime = AudioSettings.dspTime;
        double elapsedLoop = currentTime - fullLoopStartDspTime;

        // Use while instead of if to handle large jumps (e.g. after hiccups)
        while (elapsedLoop >= fullLoopDuration)
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

    }

    public void OnResume()
    {

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

        StopAllCoroutines();
        ResetVisuals();

        if (inputSlider != null)
        {
            inputSlider.value = 0f;
        }

        if (handleRect != null)
        {
            handleRect.anchoredPosition = parkedPos;
        }

        isFrozen = false;

        // DON'T call ResetLoopStartTime() or InitializeBeatValues() here
        // Timing will be synced when game starts

        Debug.Log("[PlayerInputVisualHandler] Reset complete");
    }

    private void ResetLoopStartTime()
    {
        fullLoopStartDspTime = AudioSettings.dspTime;
    }

    // In PlayerInputVisualHandler.cs - ADD SyncWithMetronome method
    // In PlayerInputVisualHandler.cs - MODIFY to accept cached time
    public void SyncWithMetronome(double nextBeatTime)
    {
        if (metronome == null)
        {
            Debug.LogError("[PlayerInputVisualHandler] Cannot sync - metronome is null");
            return;
        }

        InitializeBeatValues();
        fullLoopStartDspTime = nextBeatTime;

        Debug.Log($"[PlayerInputVisualHandler] Synced - nextBeat: {nextBeatTime:F4}");
    }
}
