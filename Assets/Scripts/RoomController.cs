using System;
using UnityEngine;

/// <summary>
/// Orchestrates a single dungeon room encounter.
/// Drives the screen transition, starts the mode via DungeonModeController.StartRoom(),
/// waits for either song completion or player death, then covers the screen and
/// fires OnRoomComplete with the result.
///
/// FLOW (CombatEncounter):
///   LoadRoom(def) → Entering (reveal) → Playing (combat) →
///   Evaluating (build result) → Exiting (cover) → Complete (idle, OnRoomComplete).
///
/// FLOW (DirectionChoice):
///   LoadRoom(def) → Entering (reveal) → Playing (show doors) →
///   ChooseDirection(n) → Evaluating → Exiting (cover) → Complete (idle, OnRoomComplete).
///   OnDirectionChosen fires before Exiting begins, carrying the chosen index.
/// </summary>
public class RoomController : MonoBehaviour
{
    private enum RoomState { Entering, Playing, Evaluating, Exiting, Complete }

    [SerializeField] private DungeonModeController modeController;
    [SerializeField] private ScreenTransition       screenTransition;
    [SerializeField] private DungeonZoomTransition  dungeonTransition;
    [SerializeField] private DungeonHealth         playerHealth;
    [SerializeField] private SpriteRenderer        backgroundRenderer;
    [SerializeField] private SpriteRenderer        backgroundZoomerRenderer;
    [SerializeField] private Sprite                titleBackgroundSprite;
    [SerializeField] private GameObject            doorsUI;

    /// <summary>Fired when the room is fully exited, carrying the outcome.</summary>
    public event Action<RoomResult> OnRoomComplete;

    /// <summary>
    /// Fired the moment the player selects a door in a DirectionChoice room.
    /// Carries the chosen direction index (0 = left, 1 = right).
    /// Fires before the exit transition begins.
    /// </summary>
    public event Action<int> OnDirectionChosen;

    private RoomState             _state       = RoomState.Complete;
    private RoomDefinition        _currentDef;
    private DungeonFloorDefinition _currentFloor;
    private RoomResult            _result;
    private float                 _roomStartTime;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Begins a new room encounter. Triggers the reveal transition, then starts gameplay.
    /// Safe to call only when the current state is Complete (i.e. no room in progress).
    /// </summary>
    /// <param name="def">The room to load.</param>
    /// <param name="floor">
    /// The floor this room belongs to. Its <see cref="DungeonFloorDefinition.BackgroundSprite"/>
    /// is used as the background fallback when the room's own sprite is null.
    /// </param>
    public void LoadRoom(RoomDefinition def, DungeonFloorDefinition floor = null)
    {
        if (_state != RoomState.Complete)
        {
            Debug.LogWarning("[RoomController] LoadRoom called while a room is already in progress — ignored.");
            return;
        }

        _currentDef   = def;
        _currentFloor = floor;
        SetState(RoomState.Entering);
    }

    /// <summary>
    /// Called by door buttons in a DirectionChoice room.
    /// directionIndex: 0 = left door, 1 = right door.
    /// Ignored if the current room is not a DirectionChoice room or not in Playing state.
    /// </summary>
    public void ChooseDirection(int directionIndex)
    {
        if (_state != RoomState.Playing) return;
        if (_currentDef == null || _currentDef.RoomType != RoomType.DirectionChoice) return;

        _state = RoomState.Evaluating;
        BuildResult(survived: true);
        OnDirectionChosen?.Invoke(directionIndex);
        SetState(RoomState.Exiting);
    }

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Update()
    {
        switch (_state)
        {
            // Transition to Playing only once the reveal animation is fully complete.
            // IsScreenCovered becomes false the moment the transition *starts*, so
            // we also guard on !IsTransitioning to avoid calling StartRoom while the
            // screen is still animating (which would start spawn audio during the wipe).
            case RoomState.Entering:
                if (!screenTransition.IsScreenCovered && !screenTransition.IsTransitioning)
                    SetState(RoomState.Playing);
                break;

            case RoomState.Exiting:
                if (dungeonTransition.IsScreenCovered)
                {
                    SetState(RoomState.Complete);
                    OnRoomComplete?.Invoke(_result);
                }
                break;
        }
    }

    // ── State Machine ─────────────────────────────────────────────────────────

    private void SetState(RoomState next)
    {
        _state = next;

        switch (next)
        {
            case RoomState.Entering:
                modeController.ClearVisuals();
                ApplyBackground();
                screenTransition.StartReveal();
                dungeonTransition.StartReveal();
                break;

            case RoomState.Playing:
                _roomStartTime = Time.realtimeSinceStartup;
                if (_currentDef != null && _currentDef.RoomType == RoomType.DirectionChoice)
                {
                    // Direction-choice rooms: just show the door buttons, no combat.
                    if (doorsUI != null) doorsUI.SetActive(true);
                }
                else
                {
                    // Combat encounter: hide doors, wire events, start the mode.
                    if (doorsUI != null) doorsUI.SetActive(false);
                    SubscribeEvents();
                    modeController.StartRoom(_currentDef);
                }
                break;

            case RoomState.Exiting:
                if (doorsUI != null) doorsUI.SetActive(false);
                dungeonTransition.StartCover();
                break;
        }
    }

    // ── Event Handlers ────────────────────────────────────────────────────────

    private void SubscribeEvents()
    {
        modeController.OnModeComplete  += HandleModeComplete;
        playerHealth.OnHealthDepleted  += HandleHealthDepleted;
    }

    private void UnsubscribeEvents()
    {
        modeController.OnModeComplete  -= HandleModeComplete;
        playerHealth.OnHealthDepleted  -= HandleHealthDepleted;
    }

    private void HandleModeComplete()
    {
        if (_state != RoomState.Playing) return;
        _state = RoomState.Evaluating;
        UnsubscribeEvents();
        BuildResult(survived: true);
        SetState(RoomState.Exiting);
    }

    private void HandleHealthDepleted()
    {
        if (_state != RoomState.Playing) return;
        _state = RoomState.Evaluating;
        UnsubscribeEvents();
        // Stop the beat manager immediately so it cannot fire OnSongComplete later and
        // trigger a second ShowResultsSequence after the run has already handled routing.
        modeController.Cleanup();
        BuildResult(survived: false);
        // Skip the zoom transition on death — the dungeon zoom is a room-change visual.
        // GameManager.ShowResultsSequence will cover the screen with the standard
        // transition before revealing the score page.
        _state = RoomState.Complete;
        OnRoomComplete?.Invoke(_result);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void BuildResult(bool survived)
    {
        _result = new RoomResult
        {
            Survived        = survived,
            Score           = modeController != null ? modeController.Score : 0,
            HealthRemaining = playerHealth   != null ? playerHealth.CurrentHealth : 0,
            Tier            = _currentDef    != null ? _currentDef.Tier : EnemyTier.Standard,
            TimeElapsed     = Time.realtimeSinceStartup - _roomStartTime,
        };
    }

    /// <summary>
    /// Resets both background renderers to the title-screen sprite.
    /// Call this before revealing the screen after a game over so the
    /// score page is shown against the original background.
    /// </summary>
    public void ResetToTitleBackground()
    {
        if (titleBackgroundSprite == null) return;
        if (backgroundRenderer       != null) backgroundRenderer.sprite       = titleBackgroundSprite;
        if (backgroundZoomerRenderer != null) backgroundZoomerRenderer.sprite = titleBackgroundSprite;
    }

    private void ApplyBackground()
    {
        // Use explicit Unity null checks — the ?? operator bypasses Unity's
        // custom == override and will incorrectly treat missing-reference
        // (fake-null) Sprite fields as non-null.
        Sprite roomSprite  = _currentDef  != null ? _currentDef.BackgroundSprite  : null;
        Sprite floorSprite = _currentFloor != null ? _currentFloor.BackgroundSprite : null;
        Sprite sprite      = roomSprite != null ? roomSprite : floorSprite;
        if (sprite == null) return;

        if (backgroundRenderer       != null) backgroundRenderer.sprite       = sprite;
        if (backgroundZoomerRenderer != null) backgroundZoomerRenderer.sprite = sprite;
    }
}
