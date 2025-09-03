using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Rotates a UI element slightly left/right in perfect sync with a shared Metronome.
/// All instances stay aligned because rotation is driven by metronome phase (dspTime-based).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UIRotateWobble : MonoBehaviour
{
    [Header("Metronome")]
    [Tooltip("Reference to the global Metronome. Must expose BPM and DSP start (see notes).")]
    public Metronome metronome;

    [Header("Wobble")]
    [Tooltip("Max degrees to each side (Z axis).")]
    public float rotationAmount = 12f;

    [Tooltip("How many beats for ONE FULL wobble cycle (left→right→left). 2 = one swing per beat, 4 = slower wobble.")]
    [Min(0.125f)]
    public float beatsPerCycle = 2f;

    [Tooltip("Phase offset in beats if you want to deliberately lead/lag the wobble.")]
    public float phaseOffsetBeats = 0f;

    [Header("Easing (bell curve feel)")]
    [Tooltip("Optional extra shaping. Leave empty for pure sinus (already smooth).")]
    public AnimationCurve easeCurve = AnimationCurve.Linear(0, 1, 1, 1);

    private RectTransform _rt;

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
        metronome = GameManager.instance.metronome;
    }

    void OnEnable()
    {
        // Snap to the exact metronome phase the moment we enable.
        ApplyRotationFromMetronomePhase();
    }

    void Update()
    {
        if (Time.timeScale != 0)
            ApplyRotationFromMetronomePhase();
    }

    private void ApplyRotationFromMetronomePhase()
    {
        if (metronome == null || beatsPerCycle <= 0f)
            return;

        // --- Read a shared clock so ALL instances agree on the same timebase.
        double nowDsp = AudioSettings.dspTime;

        // --- Get beats since metronome start.
        // Assumes your Metronome exposes:
        //   public float BPM;
        //   public double DspStartTime;   // when the metronome began on the DSP clock
        // If your names differ, just map accordingly.
        double bpm = metronome.bpm;
        double t0 = metronome.startTick;

        // Protect against not-started metronome
        if (bpm <= 0.0 || nowDsp < t0)
            return;

        double beatsSinceStart = (nowDsp - t0) * (bpm / 60.0);

        // Optional offset (in beats) to align/shift phase globally or per object
        beatsSinceStart += phaseOffsetBeats;

        // Convert to [0,1) phase for ONE cycle
        double cycles = beatsSinceStart / beatsPerCycle;
        float phase01 = (float)(cycles - System.Math.Floor(cycles)); // fractional part

        // Smooth back-and-forth with a sine wave:
        //   angle = sin(phase * TAU) * amount
        // Sine already gives ease-in/out (bell-like) at extremes.
        float baseWave = Mathf.Sin(phase01 * Mathf.PI * 2f);

        // Optional extra shaping (e.g., slight bell-curve emphasis)
        float shaped = baseWave * (easeCurve != null ? easeCurve.Evaluate(phase01) : 1f);

        float angleZ = shaped * rotationAmount;
        _rt.localRotation = Quaternion.Euler(0f, 0f, angleZ);
    }

    void OnDisable()
    {
        // Reset to neutral so rapid spawns don't leave residual rotation
        if (_rt != null)
            _rt.localRotation = Quaternion.identity;
    }
}
