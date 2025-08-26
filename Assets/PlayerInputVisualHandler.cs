using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerInputVisualHandler : MonoBehaviour
{
    /* ──────────────  UI REFERENCES  ────────────── */
    [Header("UI References")]
    public Slider inputSlider;
    public RectTransform indicatorParent;
    public GameObject inputIndicatorPrefab;

    [Header("Metronome Reference")]
    public Metronome metronome;

    /* ──────────────  LOOP / BAR TIMING  ────────────── */
    private double beatDuration;
    private double barDuration;
    private double fullLoopDuration;
    private double fullLoopStartDspTime;
    public int fullLoopBeats = 8;             // (opponent bar + player bar)

    /* ──────────────  LEAD-IN SETTINGS  ────────────── */
    [Header("Lead-in")]
    [Tooltip("How many beats early the handle begins sliding in")]
    [Range(1, 3)] public int leadInBeats = 1;

    [Tooltip("Extra X-offset (px) to park the handle fully off-screen")]
    public float offScreenPadding = 10f;

    /* ──────────────  LEAD-IN RUNTIME DATA  ────────────── */
    private RectTransform handleRect;
    private Vector2 readyPos;   // anchoredPosition when slider.value == 0
    private Vector2 parkedPos;  // fully hidden off-screen

    /* ──────────────────────────  INITIALISATION  ────────────────────────── */
    private void Start()
    {
        if (metronome == null)
        {
            Debug.LogError("Metronome reference is missing in PlayerInputVisualHandler!");
            enabled = false;
            return;
        }

        // Cache timing values & handle positions
        InitializeBeatValues();
        CacheHandlePositions();
    }

    private void OnEnable() => InitializeBeatValues();

    /* --------------------------------------------------------------------- */
    private void InitializeBeatValues()
    {
        beatDuration = 60.0 / metronome.bpm;   // one beat
        barDuration = 4 * beatDuration;       // 4/4 bar
        fullLoopDuration = fullLoopBeats * beatDuration;
        fullLoopStartDspTime = AudioSettings.dspTime;
    }

    private void CacheHandlePositions()
    {
        handleRect = inputSlider.handleRect;
        readyPos = handleRect.anchoredPosition;

        float sliderWidth = inputSlider.GetComponent<RectTransform>().rect.width;
        parkedPos = readyPos + Vector2.left * (sliderWidth + offScreenPadding);

        // Start hidden
        handleRect.anchoredPosition = parkedPos;
        inputSlider.value = 0f;
    }

    /* ──────────────────────────  RUNTIME UPDATE  ────────────────────────── */
    private void Update()
    {
        if (GameManager.instance.isPaused) return;

        double currentTime = GameManager.instance.VirtualDspTime();
        double elapsedLoop = currentTime - fullLoopStartDspTime;

        /*  Restart loop timer every fullLoopDuration ---------------------- */
        if (elapsedLoop >= fullLoopDuration)
        {
            fullLoopStartDspTime += fullLoopDuration;
            elapsedLoop = currentTime - fullLoopStartDspTime;
        }

        /*  PHASE 1 – Opponent bar (handle parked) ------------------------- */
        double leadInStart = barDuration - (beatDuration * leadInBeats);

        if (elapsedLoop < leadInStart)
        {
            handleRect.anchoredPosition = parkedPos;
            inputSlider.value = 0f;
            return;
        }

        /*  PHASE 2 – Lead-in (slide parked ➜ ready) ----------------------- */
        if (elapsedLoop < barDuration)
        {
            double leadElapsed = elapsedLoop - leadInStart;
            float t = (float)(leadElapsed / (beatDuration * leadInBeats));
            handleRect.anchoredPosition = Vector2.Lerp(parkedPos, readyPos, t);
            inputSlider.value = 0f;      // still fixed at the bar start
            return;
        }

        /*  PHASE 3 – Player bar (normal slider sweep) --------------------- */
        double elapsedBar = elapsedLoop - barDuration;          // 0 → barDuration
        float progress = (float)(elapsedBar / barDuration);  // 0 → 1
        inputSlider.value = progress;
    }

    /* ──────────────────────────  INDICATORS  ────────────────────────── */
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
}
