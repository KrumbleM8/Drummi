using System;
using System.Collections.Generic;
using KrumbleHut.Drummi.Dungeons;
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

    [Header("Floor Transition")]
    [Tooltip("Presents the wipe + floor title card between floors. Optional — without it, floors advance with no visual transition.")]
    [SerializeField] private FloorTransitionController floorTransition;

    [Tooltip("Seconds over which the outgoing floor's music fades to silence under the transition card. Keep at or below cover duration + title hold.")]
    [SerializeField] private float floorMusicFadeSeconds = 1.5f;

    [Header("Effects")]
    [Tooltip("Run-effect state holder. Optional — runs work without the effect system.")]
    [SerializeField] private DungeonEffectsController effectsController;

    [Tooltip("Pick overlay shown between rooms. Optional — no overlay means no picks.")]
    [SerializeField] private EffectPickOverlayController pickOverlay;

    [Tooltip("A pick is offered after every Nth completed room (global across floors).")]
    [SerializeField] private int pickCadence = 2;

    [Tooltip("Number of effects offered per pick.")]
    [SerializeField] private int pickOfferSize = 3;

    // ── Tutorial ──────────────────────────────────────────────────────────────

    private const string TutorialCompletedKey = "Drummi_Dungeon_TutorialCompleted";

    /// <summary>True once the player has completed a tutorial floor at least once.</summary>
    public static bool TutorialCompleted => PlayerPrefs.GetInt(TutorialCompletedKey, 0) == 1;

    /// <summary>
    /// When true, the next StartRun() includes tutorial floors even though the
    /// tutorial has already been completed. Set by TutorialPromptController from the
    /// start-page toggle before the run begins.
    /// </summary>
    public bool ReplayTutorialRequested { get; set; }

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
    private bool _pendingFloorTransition;

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
        _currentFloorIndex      = 0;
        _currentRoomIndex       = 0;
        _runActive              = true;
        _roomsCleared           = 0;
        _pendingFloorTransition = false;

        // Skip leading tutorial floors once completed, unless a replay was requested.
        // (Dying on the tutorial leaves the flag unset, so it is forced again — intended.)
        bool includeTutorial = !TutorialCompleted || ReplayTutorialRequested;
        if (!includeTutorial)
        {
            while (_currentFloorIndex < floors.Count && floors[_currentFloorIndex] != null
                   && floors[_currentFloorIndex].IsTutorialFloor)
                _currentFloorIndex++;
        }

        // Reset score and health exactly once at run start.
        // StartMode (called per-room) no longer resets them so they persist across rooms.
        modeController?.ResetToInitialState();
        effectsController?.ResetForRun();

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

        // Room-start seam for effect lifetimes: NRooms effects tick down here.
        effectsController?.NotifyRoomStarted();

        roomController.OnRoomComplete    += HandleRoomComplete;
        roomController.OnDirectionChosen += HandleDirectionChosen;
        roomController.LoadRoom(room, floor);
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

        var currentFloor = floors[_currentFloorIndex];
        bool clearedTutorialRoom = currentFloor.IsTutorialFloor;

        // Tutorial rooms don't count toward the effect-pick cadence — live-floor pick
        // pacing must be identical whether or not the tutorial was played.
        if (!clearedTutorialRoom)
            _roomsCleared++;

        if (_currentRoomIndex >= currentFloor.Rooms.Count)
        {
            if (clearedTutorialRoom)
            {
                // Tutorial floor completed — future runs skip it unless a replay is requested.
                PlayerPrefs.SetInt(TutorialCompletedKey, 1);
                PlayerPrefs.Save();
                Debug.Log("[DungeonRunner] Tutorial floor completed — flag saved");
            }

            _currentFloorIndex++;
            _currentRoomIndex = 0;

            if (_currentFloorIndex >= floors.Count)
            {
                _runActive = false;
                Debug.Log($"[DungeonRunner] Run complete — {_currentFloorIndex} floors cleared, score {runScore}");
                OnRunComplete?.Invoke(_currentFloorIndex, runScore);
                // Route through the shared results sequence (music stop, metronome off,
                // screen cover/reveal, populated score screen). Win presentation lives
                // behind GameManager.TriggerRunComplete — swap in a dedicated victory
                // screen there later without touching run sequencing.
                GameManager.instance?.TriggerRunComplete();
                return;
            }

            Debug.Log($"[DungeonRunner] Advancing to floor {_currentFloorIndex}");
            effectsController?.NotifyFloorAdvanced();
            // Present the wipe + floor title card before the new floor's first room.
            // Deferred to ProceedToNextRoom so an effect pick (if due) resolves first.
            _pendingFloorTransition = true;
        }

        // Effect pick: after every Nth completed room, defer the next room until the
        // player picks. Between rooms the encounter is already disarmed
        // (DungeonBeatManager sets EncounterActive=false at room end) and the floor
        // BGM loops on untouched — input re-arms only via the room-start path, and
        // the next room's bar seam is computed after the pick, inside StartRoom.
        if (!clearedTutorialRoom && effectsController != null && pickOverlay != null
            && pickCadence > 0 && _roomsCleared % pickCadence == 0)
        {
            var offer = effectsController.GetOffer(pickOfferSize);
            if (offer.Count > 0)
            {
                Debug.Log($"[DungeonRunner] Effect pick — offering {offer.Count} effect(s)");
                pickOverlay.Show(offer, HandleEffectPicked);
                return;
            }
            // Empty offer (whole pool active) — skip the pick phase entirely.
        }

        ProceedToNextRoom();
    }

    private void HandleEffectPicked(EffectDefinition effect)
    {
        effectsController?.ApplyEffect(effect);
        ProceedToNextRoom();
    }

    /// <summary>
    /// Routes to the next room, inserting the floor transition presentation first
    /// when a floor boundary was just crossed. If a pick and a floor advance coincide,
    /// the pick overlay resolves first (existing order in HandleRoomComplete), then the
    /// transition runs here — deterministic either way.
    /// </summary>
    private void ProceedToNextRoom()
    {
        if (_pendingFloorTransition && floorTransition != null)
        {
            _pendingFloorTransition = false;
            StartCoroutine(FloorTransitionRoutine());
        }
        else
        {
            _pendingFloorTransition = false;
            LoadNextRoom();
        }
    }

    private System.Collections.IEnumerator FloorTransitionRoutine()
    {
        var floor = floors[_currentFloorIndex];

        // Fade the outgoing floor's music to silence under the card.
        // (Stem-reduction hook lives in AudioManager.FadeOutMusic — when stem-authored
        // tracks exist, this becomes a filter down to the bass stem instead.)
        AudioManager.instance?.FadeOutMusic(floorMusicFadeSeconds);

        yield return floorTransition.PlayCoverAndTitle(floor.FloorName);

        // Never start the next room while paused — StartRoom schedules audio against
        // the clock, and the player can open the pause menu during the title card.
        while (GameClock.Instance != null && GameClock.Instance.IsPaused)
            yield return null;

        if (!_runActive) yield break;

        // Restart the music session for the new floor: fresh clock origin, new track
        // scheduled from silence at the floor's BPM, musical lead-in applied — the
        // outgoing track was just faded out, so a bar-seam swap would splice into silence.
        modeController?.PrepareFloorRestart();
        LoadNextRoom();
    }
}
