using UnityEngine;
using UnityEngine.UI;

namespace KrumbleHut.Drummi.FlashCards
{
    /// <summary>
    /// Garden (drum machine) how-to-play loop: a miniature of the real
    /// Garden.unity screen (built by CoplayScripts/AddSignPreviews.cs in the
    /// game's 800-wide canvas units): tiled grass, the brown plot with its blue
    /// strip and 4×8 cell circles, the play Button and the X icon. Playback
    /// runs like DrumMachine at 120 BPM: the playhead column turns solid white
    /// for 0.25 s per step, 8 steps per 2 s pass. Over the 10 s loop one demo
    /// cell is "tapped" every 2 s and cycles None → Kick → Snare → HiHat →
    /// Clap → None exactly like DrumGridCell.CycleSound, so the loop is
    /// seamless. Colours match DrumGridCell. Pure visuals.
    /// </summary>
    public class GardenPreviewAnimation : SignPreviewAnimation
    {
        [Tooltip("32 cell images, row-major (4 rows × 8 columns).")]
        [SerializeField] private Image[] cells = new Image[32];

        [Tooltip("Sound per cell, row-major: 0 none, 1 kick, 2 snare, 3 hi-hat, 4 clap.")]
        [SerializeField] private int[] basePattern = new int[32];

        [Tooltip("Cell index the demo tap cycles through all sounds.")]
        [SerializeField] private int toggleCell = 3;

        [SerializeField] private Color inactiveColor = new Color(0.2f, 0.2f, 0.25f, 1f);
        [SerializeField] private Color flashColor    = Color.white;

        [Tooltip("Kick, Snare, HiHat, Clap — DrumGridCell palette.")]
        [SerializeField] private Color[] soundColors =
        {
            new Color(0.95f, 0.55f, 0.10f),
            new Color(0.25f, 0.60f, 1.00f),
            new Color(0.25f, 0.90f, 0.40f),
            new Color(0.90f, 0.25f, 0.60f),
        };

        private const int   Cols     = 8;
        private const float StepDur  = 0.25f;   // 120 BPM, 8th notes
        private const float TapEvery = 2.0f;    // one demo tap per playhead pass
        private const int   TapStates = 5;      // None, Kick, Snare, HiHat, Clap

        protected override float LoopLength => TapEvery * TapStates; // 10 s

        private Color SoundColor(int sound) =>
            sound <= 0 || sound > soundColors.Length ? inactiveColor : soundColors[sound - 1];

        protected override void Evaluate(float t)
        {
            int step = Mathf.FloorToInt(t / StepDur) % Cols;
            int tapState = Mathf.FloorToInt(t / TapEvery) % TapStates; // 0 = None … 4 = Clap

            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] == null) continue;
                int sound = basePattern.Length > i ? basePattern[i] : 0;
                if (i == toggleCell) sound = tapState;
                cells[i].color = (i % Cols) == step ? flashColor : SoundColor(sound);
            }
        }
    }
}
