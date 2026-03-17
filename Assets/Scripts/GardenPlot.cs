// DrumPlot.cs

using UnityEngine;

public class GardenPlot : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private int rows = 4;
    [SerializeField] private int cols = 8;

    //[Header("Spawning")]
    //[Tooltip("OFF = cells already exist as children in the scene. " +
    //         "ON  = cells will be spawned at runtime from the prefab.")]
    //[SerializeField] private bool spawnCells = true;
    //[SerializeField] private GameObject cellPrefab;
    //[SerializeField] private float cellSize = 1f;
    //[SerializeField] private float cellSpacing = 0.1f;

    [Header("State")]
    public bool isActive = true;

    private bool[,] gridState;
    private DrumGridCell[,] cells;

    public int Cols => cols;

    // ------------------------------------------------------------------ setup

    private void Awake()
    {
        gridState = new bool[rows, cols];
        cells = new DrumGridCell[rows, cols];
        CollectExistingCells();

        //if (spawnCells) SpawnCells();
        //else CollectExistingCells();
    }

    // Instantiates cells at runtime — original behaviour
    //private void SpawnCells()
    //{
    //    float stride = cellSize + cellSpacing;

    //    for (int r = 0; r < rows; r++)
    //    {
    //        for (int c = 0; c < cols; c++)
    //        {
    //            Vector3 localPos = new Vector3(c * stride, -r * stride, 0);
    //            var go = Instantiate(cellPrefab, transform.position + localPos, Quaternion.identity, transform);
    //            go.name = $"{gameObject.name}_Cell_{r}_{c}";

    //            var cell = go.GetComponent<DrumGridCell>();
    //            cell.Row = r;
    //            cell.Col = c;
    //            cells[r, c] = cell;
    //        }
    //    }
    //}

    // Reads hand-placed children — no spawning
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

        // Catch any gaps — a null in the array means a cell is missing or misconfigured
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (cells[r, c] == null)
                    Debug.LogError($"[{gameObject.name}] No cell found for Row {r}, Col {c}. Make sure every cell has correct Row/Col values set in the Inspector.");
    }

    // ------------------------------------------------------------------ called by DrumMachine

    public void ToggleCell(int row, int col)
    {
        gridState[row, col] = !gridState[row, col];
        cells[row, col].SetActive(gridState[row, col]);
    }

    public void ProcessStep(int step)
    {
        for (int r = 0; r < rows; r++)
        {
            cells[r, step].SetStepHighlight(true);
            if (gridState[r, step]) TriggerSound((DrumSoundType)r);
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
        // TODO: route to AudioManager
        Debug.Log($"[{gameObject.name}] ♪ {sound}");
    }

    // ------------------------------------------------------------------ utils

    public void ClearGrid()
    {
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                gridState[r, c] = false;
                cells[r, c].SetActive(false);
            }
    }
}