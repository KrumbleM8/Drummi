using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sequences a full dungeon run: iterates through floors and rooms in order,
/// aggregates score, and routes to the appropriate UI page on completion or failure.
///
/// FLOW:
///   StartRun() → LoadNextRoom() → [RoomController plays room] →
///   HandleRoomComplete() → (advance or end run)
///
/// NOTE: DungeonGameOverHandler also listens for DungeonHealth.OnHealthDepleted
/// independently. When a run is active DungeonRunner owns the routing; the handler
/// is suppressed via its IsRunActive guard. If no run is active the handler fires
/// as normal (e.g. during standalone testing).
/// </summary>
public class DungeonRunner : MonoBehaviour
{
    [SerializeField] private RoomController            roomController;
    [SerializeField] private DungeonModeController    modeController;
    [SerializeField] private List<DungeonFloorDefinition> floors;
    [SerializeField] private UIMenuManager            uiMenuManager;
    [SerializeField] private string                   runCompletePageName = "RunComplete";
    [SerializeField] private string                   gameOverPageName    = "GameOver";

    // ── Public Events ─────────────────────────────────────────────────────────

    /// <summary>Fired when all floors are cleared. (floorsCleared, totalScore)</summary>
    public event Action<int, int> OnRunComplete;

    /// <summary>Fired when the player dies mid-run. (floorReached, totalScore)</summary>
    public event Action<int, int> OnRunFailed;

    // ── State ─────────────────────────────────────────────────────────────────

    private int  _currentFloorIndex;
    private int  _currentRoomIndex;
    private bool _runActive;
    private int  _lastChosenDirection = -1;
    private int  _roomsCleared;

    /// <summary>
    /// True while a run is in progress.
    /// DungeonGameOverHandler reads this to suppress its own routing when DungeonRunner
    /// is already handling the failure path.
    /// </summary>
    public bool IsRunActive => _runActive;

    /// <summary>Number of rooms successfully cleared in the current run.</summary>
    public int RoomsCleared => _roomsCleared;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Resets all run state and begins from floor 0, room 0.</summary>
    public void StartRun()
    {
        _currentFloorIndex = 0;
        _currentRoomIndex  = 0;
        _runActive         = true;
        _roomsCleared      = 0;

        // Reset score and health exactly once at run start.
        // StartMode (called per-room) no longer resets them so they persist across rooms.
        modeController?.ResetToInitialState();

        Debug.Log("[DungeonRunner] Run started");
        LoadNextRoom();
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void LoadNextRoom()
    {
        if (!_runActive) return;

        if (floors == null || _currentFloorIndex >= floors.Count)
        {
            Debug.LogError("[DungeonRunner] LoadNextRoom: floor index out of range");
            return;
        }

        var floor = floors[_currentFloorIndex];
        if (floor == null)
        {
            Debug.LogError($"[DungeonRunner] Floor at index {_currentFloorIndex} is null");
            return;
        }

        if (_currentRoomIndex >= floor.Rooms.Count)
        {
            Debug.LogError($"[DungeonRunner] Room index {_currentRoomIndex} out of range for floor '{floor.FloorName}'");
            return;
        }

        var room = floor.Rooms[_currentRoomIndex];
        Debug.Log($"[DungeonRunner] Loading floor '{floor.FloorName}' room {_currentRoomIndex} — '{room?.RoomName}'");

        roomController.OnRoomComplete    += HandleRoomComplete;
        roomController.OnDirectionChosen += HandleDirectionChosen;
        roomController.LoadRoom(room);
    }

    private void HandleDirectionChosen(int directionIndex)
    {
        _lastChosenDirection = directionIndex;
        Debug.Log($"[DungeonRunner] Direction chosen: {directionIndex} — TODO: implement floor branching");
        // Future: use directionIndex to pick between branching floor paths.
    }

    private void HandleRoomComplete(RoomResult result)
    {
        roomController.OnRoomComplete    -= HandleRoomComplete;
        roomController.OnDirectionChosen -= HandleDirectionChosen;

        if (!_runActive) return;

        // Score accumulates in DungeonEvaluator across all rooms — read it directly
        // rather than summing result.Score per room (which would double-count now that
        // the evaluator is no longer reset between rooms).
        int runScore = modeController != null ? modeController.Score : result.Score;

        if (!result.Survived)
        {
            _runActive = false;
            Debug.Log($"[DungeonRunner] Run failed — floor {_currentFloorIndex}, score {runScore}");
            OnRunFailed?.Invoke(_currentFloorIndex, runScore);
            // TriggerGameOver runs ShowResultsSequence immediately. The screen is already
            // covered by RoomController at this point, so the sequence skips straight to
            // displaying the Score page and revealing.
            GameManager.instance?.TriggerGameOver();
            return;
        }

        _currentRoomIndex++;
        _roomsCleared++;

        var currentFloor = floors[_currentFloorIndex];
        if (_currentRoomIndex >= currentFloor.Rooms.Count)
        {
            _currentFloorIndex++;
            _currentRoomIndex = 0;

            if (_currentFloorIndex >= floors.Count)
            {
                _runActive = false;
                Debug.Log($"[DungeonRunner] Run complete — {_currentFloorIndex} floors cleared, score {runScore}");
                OnRunComplete?.Invoke(_currentFloorIndex, runScore);
                uiMenuManager?.ShowPage(runCompletePageName);
                return;
            }

            Debug.Log($"[DungeonRunner] Advancing to floor {_currentFloorIndex}");
        }

        LoadNextRoom();
    }
}
