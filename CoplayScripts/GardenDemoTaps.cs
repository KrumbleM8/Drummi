using System.Text;
using UnityEngine;

// PLAY-MODE (Garden.unity): dumps the grid and taps a small demo pattern so reference
// captures show coloured cells. Execute() = dump only; ExecuteTap() = tap pattern.
public static class GardenDemoTaps
{
    public static void Execute()
    {
        var cells = Object.FindObjectsByType<DrumGridCell>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var sb = new StringBuilder();
        sb.Append("[GardenTaps] cells=").Append(cells.Length);
        int maxRow = -1, maxCol = -1;
        foreach (var c in cells) { if (c.Row > maxRow) maxRow = c.Row; if (c.Col > maxCol) maxCol = c.Col; }
        sb.Append(" rows=").Append(maxRow + 1).Append(" cols=").Append(maxCol + 1);
        foreach (var c in cells)
            if (c.Row == 0 && c.Col <= 1 || c.Row == maxRow && c.Col == maxCol)
                sb.Append("\n  cell(").Append(c.Row).Append(",").Append(c.Col).Append(") world=").Append(c.transform.position.ToString("F2"))
                  .Append(" scale=").Append(c.transform.lossyScale.ToString("F2")).Append(" sound=").Append(c.CurrentSound);
        var dm = Object.FindFirstObjectByType<DrumMachine>(FindObjectsInactive.Include);
        sb.Append("\n  DrumMachine=").Append(dm != null ? dm.name : "none");
        Debug.Log(sb.ToString());
    }

    public static void ExecutePlay()
    {
        var dm = Object.FindFirstObjectByType<DrumMachine>(FindObjectsInactive.Include);
        if (dm == null) { Debug.Log("[GardenTaps] no DrumMachine"); return; }
        dm.TogglePlayback();
        Debug.Log("[GardenTaps] TogglePlayback invoked");
    }

    public static void ExecuteTap()
    {
        var cells = Object.FindObjectsByType<DrumGridCell>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int taps = 0;
        foreach (var c in cells)
        {
            int n = 0;
            if (c.Row == 0 && (c.Col % 2 == 0)) n = 1;          // kick on even cols, row 0
            else if (c.Row == 1 && (c.Col % 4 == 2)) n = 2;     // snare
            else if (c.Row == 2) n = 3;                          // hi-hat every step
            else if (c.Row == 3 && c.Col % 4 == 3) n = 4;        // clap
            for (int i = 0; i < n; i++) { c.Tap(); taps++; }
        }
        Debug.Log("[GardenTaps] tapped " + taps + " times across " + cells.Length + " cells");
    }
}
