// DrumMachine.cs
// ----------------------------------------------------------------------------
// WHAT IT IS:
//   The sequencer. No longer builds a grid itself — it sequences through
//   a list of DrumPlots, one step at a time.
//
// HOW MULTI-PLOT SEQUENCING WORKS:
//   - All active plots play their steps in sync (same step index, same BPM).
//   - This means all plots fire simultaneously, like a multi-track loop.
//   - Future option: step through plots one at a time (chain mode) — see TODO.
//
// HOW TO ADD A NEW PLOT:
//   1. Duplicate an existing Plot GameObject in the scene.
//   2. Move it where you want.
//   3. Drag it into the Plots list on this component in the Inspector.
//   Done. No code changes needed.
// ----------------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DrumMachine : MonoBehaviour
{
    public static DrumMachine Instance { get; private set; }

    [Header("Plots")]
    [Tooltip("Drag all DrumPlot GameObjects here.")]
    [SerializeField] private List<GardenPlot> plots = new();

    [Header("Playback")]
    [SerializeField] private float bpm = 120f;

    private int currentStep = -1;
    private bool isPlaying = false;

    private void Awake() => Instance = this;

    // ---------------------------------------------------------------- playback

    public void TogglePlayback()
    {
        if (isPlaying) StopAllCoroutines();
        else StartCoroutine(PlaybackLoop());
        isPlaying = !isPlaying;
    }

    private IEnumerator PlaybackLoop()
    {
        float stepDuration = 60f / bpm / 2f; // 8th notes

        // Use the first plot's column count as the loop length
        int stepCount = plots.Count > 0 ? plots[0].Cols : 8;

        while (true)
        {
            // Clear previous step on all active plots
            if (currentStep >= 0)
                foreach (var plot in plots)
                    if (plot.isActive) plot.ClearStepHighlight(currentStep);

            currentStep = (currentStep + 1) % stepCount;

            // Advance all active plots together
            foreach (var plot in plots)
                if (plot.isActive) plot.ProcessStep(currentStep);

            // TODO: "chain mode" — step through plots one at a time instead of all at once

            yield return new WaitForSeconds(stepDuration);
        }
    }

    // ------------------------------------------------------------------ input (called by DrumGridCell.Tap)

    public void OnCellTapped(DrumGridCell cell)
    {
        // Find which plot owns this cell and tell it to toggle
        foreach (var plot in plots)
        {
            if (plot.ContainsCell(cell))
            {
                plot.ToggleCell(cell.Row, cell.Col);
                return;
            }
        }
    }

    // ------------------------------------------------------------------ utils

    public void AddPlot(GardenPlot plot)
    {
        if (!plots.Contains(plot)) plots.Add(plot);
    }

    public void RemovePlot(GardenPlot plot) => plots.Remove(plot);
}