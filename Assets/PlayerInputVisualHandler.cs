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

    private double barDuration;
    private double fullLoopDuration;
    private double fullLoopStartDspTime;
    public int fullLoopBeats = 8;

    // Pause tracking variables
    private bool paused = false;
    private double pauseStartTime = 0.0;

    private void Start()
    {
        if (metronome == null)
        {
            Debug.LogError("Metronome reference is missing in PlayerInputVisualScheduler!");
            return;
        }

        double beatDuration = 60.0 / metronome.bpm;
        barDuration = 4 * beatDuration;
        fullLoopDuration = fullLoopBeats * beatDuration;
        fullLoopStartDspTime = AudioSettings.dspTime;
    }

    private void Update()
    {
        if (paused)
            return; // Freeze updates while paused

        double currentTime = AudioSettings.dspTime;
        double elapsedLoop = currentTime - fullLoopStartDspTime;

        if (elapsedLoop >= fullLoopDuration)
        {
            fullLoopStartDspTime += fullLoopDuration;
            elapsedLoop = currentTime - fullLoopStartDspTime;
        }

        if (elapsedLoop >= barDuration)
        {
            double elapsedBar2 = elapsedLoop - barDuration;
            float normalizedProgress = (float)(elapsedBar2 / barDuration);
            inputSlider.value = normalizedProgress;
        }
        else
        {
            inputSlider.value = 0f;
        }
    }

    public void SpawnInputIndicator(bool isRightBongo)
    {
        if (inputIndicatorPrefab == null || indicatorParent == null || inputSlider == null)
        {
            Debug.LogWarning("PlayerInputVisualScheduler is missing required references.");
            return;
        }

        GameObject indicator = Instantiate(inputIndicatorPrefab, indicatorParent);
        Image img = indicator.GetComponent<Image>();
        if (img != null)
        {
            img.color = isRightBongo ? Color.red : Color.green;
        }

        float parentWidth = indicatorParent.rect.width;
        float posX = (inputSlider.value * parentWidth) - (parentWidth / 2f);
        RectTransform rt = indicator.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = new Vector2(posX, 0);
        }
    }

    public void ResetVisuals()
    {
        if (indicatorParent != null)
        {
            for (int i = indicatorParent.childCount - 1; i >= 0; i--)
            {
                Destroy(indicatorParent.GetChild(i).gameObject);
            }
        }
    }

    // Call this method when pausing the game.
    public void OnPause()
    {
        if (!paused)
        {
            paused = true;
            pauseStartTime = AudioSettings.dspTime;
        }
    }

    // Call this method when resuming the game.
    public void OnResume()
    {
        if (paused)
        {
            double pauseDuration = AudioSettings.dspTime - pauseStartTime;
            fullLoopStartDspTime += pauseDuration; // Adjust our reference
            paused = false;
        }
    }
}
