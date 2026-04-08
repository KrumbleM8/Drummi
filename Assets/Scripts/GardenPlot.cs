// GardenPlot.cs

using UnityEngine;

public class GardenPlot : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private int rows = 4;
    [SerializeField] private int cols = 8;

    [Header("State")]
    public bool isActive = true;

    private DrumSoundType[,] gridState;
    private DrumGridCell[,] cells;

    public int Cols => cols;

    // ------------------------------------------------------------------ setup

    private void Awake()
    {
        gridState = new DrumSoundType[rows, cols];
        cells = new DrumGridCell[rows, cols];

        // Initialise all cells to silent
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                gridState[r, c] = DrumSoundType.None;

        CollectExistingCells();
    }

    private void CollectExistingCells()
    {
        var found = GetComponentsInChildren<DrumGridCell>();

        foreach (var cell in found)
        {
            if (cell.Row < rows && cell.Col < cols)
                cells[cell.Row, cell.Col] = cell;
            else
                Debug.LogWarning($"[{gameObject.name}] Cell '{cell.name}' has Row/Col ({cell.Row},{cell.Col}) outside grid bounds ({rows}x{cols}). Check its Inspector values.");
        }

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (cells[r, c] == null)
                    Debug.LogError($"[{gameObject.name}] No cell found for Row {r}, Col {c}. Make sure every cell has correct Row/Col values set in the Inspector.");
    }

    // ------------------------------------------------------------------ called by DrumMachine

    public void CycleCell(int row, int col)
    {
        cells[row, col].CycleSound();
        gridState[row, col] = cells[row, col].CurrentSound;
    }

    public void ProcessStep(int step)
    {
        for (int r = 0; r < rows; r++)
        {
            cells[r, step].SetStepHighlight(true);

            if (gridState[r, step] != DrumSoundType.None)
                TriggerSound(gridState[r, step]);
        }
    }

    public void ClearStepHighlight(int step)
    {
        for (int r = 0; r < rows; r++)
            cells[r, step].SetStepHighlight(false);
    }

    public bool ContainsCell(DrumGridCell cell) =>
        cell.transform.IsChildOf(transform);

    // ------------------------------------------------------------------ audio

    private void TriggerSound(DrumSoundType sound)
    {
        AudioManager.instance?.PlayDrumSound(sound);
    }

    // ------------------------------------------------------------------ utils

    public void ClearGrid()
    {
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                gridState[r, c] = DrumSoundType.None;
                cells[r, c].ResetSound();
            }
    }
}
