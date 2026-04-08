// DrumGridCell.cs
// A single cell in a drum machine grid. Cycles through DrumSoundTypes on tap.
// Colour reflects the current sound. Step highlight flashes white during playback.

using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class DrumGridCell : MonoBehaviour
{
    public int Row;
    public int Col;

    [Header("Colors")]
    [SerializeField] private Color inactiveColor = new Color(0.2f, 0.2f, 0.25f);
    [SerializeField] private Color stepHighlightColor = Color.white;

    [Tooltip("One colour per DrumSoundType, in enum order: Kick, Snare, HiHat, Clap.")]
    [SerializeField] private Color[] soundColors = new Color[]
    {
        new Color(0.95f, 0.55f, 0.10f),  // Kick  - orange
        new Color(0.25f, 0.60f, 1.00f),  // Snare - blue
        new Color(0.25f, 0.90f, 0.40f),  // HiHat - green
        new Color(0.90f, 0.25f, 0.60f),  // Clap  - pink
    };

    public DrumSoundType CurrentSound { get; private set; } = DrumSoundType.None;

    private SpriteRenderer sr;
    private bool isStepHighlighted;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        sr.color = inactiveColor;
    }

    // ------------------------------------------------------------------ input

    public void Tap() => DrumMachine.Instance.OnCellTapped(this);

    // ------------------------------------------------------------------ sound cycling

    public void CycleSound()
    {
        int next = (int)CurrentSound + 1;
        if (next >= (int)DrumSoundType._Count) next = (int)DrumSoundType.None;
        CurrentSound = (DrumSoundType)next;

        if (!isStepHighlighted) sr.color = GetBaseColor();
    }

    public void ResetSound()
    {
        CurrentSound = DrumSoundType.None;
        isStepHighlighted = false;
        sr.color = inactiveColor;
    }

    // ------------------------------------------------------------------ visuals

    public void SetStepHighlight(bool on)
    {
        isStepHighlighted = on;
        sr.color = on ? stepHighlightColor : GetBaseColor();
    }

    private Color GetBaseColor() =>
        CurrentSound == DrumSoundType.None
            ? inactiveColor
            : soundColors[(int)CurrentSound];
}
