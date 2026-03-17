// DrumGridCell.cs
// A single cell. Belongs to a DrumPlot. No changes needed here
// as plots are added — cells only talk to DrumMachine via Tap().

using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class DrumGridCell : MonoBehaviour
{
    public int Row;
    public int Col;

    [Header("Colors")]
    [SerializeField] private Color inactiveColor = new Color(0.2f, 0.2f, 0.25f);
    [SerializeField] private Color activeColor = new Color(0.9f, 0.6f, 0.1f);
    [SerializeField] private Color stepColor = new Color(1f, 1f, 1f);

    private SpriteRenderer sr;
    private bool isActive;

    private void Awake() => sr = GetComponent<SpriteRenderer>();

    public void Tap() => DrumMachine.Instance.OnCellTapped(this);

    public void SetActive(bool active)
    {
        isActive = active;
        sr.color = active ? activeColor : inactiveColor;
    }

    public void SetStepHighlight(bool on) =>
        sr.color = on ? stepColor : (isActive ? activeColor : inactiveColor);
}