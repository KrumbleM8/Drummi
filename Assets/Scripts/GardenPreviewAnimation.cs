using UnityEngine;
using UnityEngine.UI;

namespace KrumbleHut.Drummi.FlashCards
{
    /// <summary>
    /// Garden (drum machine) how-to-play loop, 3.2 s = 16 steps × 0.2 s: a
    /// 4×4 grid of coloured drum cells; a playhead column flashes white left
    /// to right (four passes per loop); a tap ring toggles one empty cell to
    /// Clap pink at 1.0 s and back to empty at 2.6 s so the loop ends where
    /// it began. Colours match DrumGridCell. Wired by
    /// CoplayScripts/AddSignPreviews.cs: cells[16] row-major, basePattern[16]
    /// (0 none, 1 kick, 2 snare, 3 hi-hat, 4 clap).
    /// </summary>
    public class GardenPreviewAnimation : SignPreviewAnimation
    {
        [SerializeField] private Image[] cells = new Image[16];

        [Tooltip("Sound per cell, row-major: 0 none, 1 kick, 2 snare, 3 hi-hat, 4 clap.")]
        [SerializeField] private int[] basePattern = new int[16];

        [Tooltip("Cell index toggled by the demo tap.")]
        [SerializeField, Range(0, 15)] private int toggleCell = 13;

        [Tooltip("Sound the toggled cell becomes (1–4).")]
        [SerializeField, Range(1, 4)] private int toggleSound = 4;

        [SerializeField] private RectTransform ring;
        [SerializeField] private Image         ringImage;

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

        private const int   Cols     = 4;
        private const int   Steps    = 16;
        private const float StepDur  = 0.2f;
        private const float TapOn    = 1.0f;
        private const float TapOff   = 2.6f;

        private static readonly float[] TapTimes = { TapOn, TapOff };

        protected override float LoopLength => Steps * StepDur;

        private void Awake()
        {
            if (ring != null && cells.Length > toggleCell && cells[toggleCell] != null)
                ring.anchoredPosition = cells[toggleCell].rectTransform.anchoredPosition;
        }

        private Color SoundColor(int sound) =>
            sound <= 0 || sound > soundColors.Length ? inactiveColor : soundColors[sound - 1];

        protected override void Evaluate(float t)
        {
            int   step = Mathf.Min(Mathf.FloorToInt(t / StepDur), Steps - 1);
            float u    = t - step * StepDur;
            int   col  = step % Cols;

            // Toggle progress: 0 → 1 at TapOn, back to 0 at TapOff.
            float toggled = Progress(t, TapOn, 0.1f) * (1f - Progress(t, TapOff, 0.1f));

            // Playhead flash: up to white over 0.05 s, back down over 0.12 s.
            float flash = Progress(u, 0f, 0.05f) * (1f - Progress(u, 0.05f, 0.12f));

            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] == null) continue;
                int   sound = basePattern.Length > i ? basePattern[i] : 0;
                Color baseC = SoundColor(sound);
                if (i == toggleCell) baseC = Color.Lerp(baseC, SoundColor(toggleSound), toggled);

                bool inColumn = i % Cols == col;
                cells[i].color = inColumn ? Color.Lerp(baseC, flashColor, flash) : baseC;
            }

            TapRing(ring, ringImage, t, TapTimes);
        }
    }
}
