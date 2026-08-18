using System;
using KrumbleHut.Drummi.Dungeons;
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
    private enum RoomState { Entering, IntroDelay, Playing, Evaluating, Exiting, Complete }

    [SerializeField] private DungeonModeController          modeController;
    [SerializeField] private DungeonInputLayoutController   inputLayoutController;
    [SerializeField] private CallAndResponseController       callAndResponseController;
    [SerializeField] private FlashCardController              flashCardController;

    [Header("Flick Room")]
    [Tooltip("Flick beat manager (the flick rig's own, not DungeonBeatManager) — initialized/armed per flick room.")]
    [SerializeField] private FlickBeatManager                flickBeatManager;

    [Tooltip("Shared enemy-loop director — the same component the standalone Flick mode drives.")]
    [SerializeField] private FlickEncounterDirector          flickDirector;
    [SerializeField] private ScreenTransition                screenTransition;
    [SerializeField] private DungeonZoomTransition  dungeonTransition;
    [SerializeField] private DungeonHealth         playerHealth;
    [SerializeField] private SpriteRenderer        backgroundRenderer;
    [SerializeField] private SpriteRenderer        backgroundZoomerRenderer;
    [SerializeField] private Sprite                titleBackgroundSprite;
    [SerializeField] private GameObject            doorsUI;

    [Header("Intro Delay (First Room Only)")]
    [Tooltip("Real-time seconds between reveal-complete and gameplay start, first room only. " +
             "Default 0 = no delay (current behaviour). The body of IntroDelayRoutine " +
             "is the hook a future intro animation sequence will replace.")]
    [SerializeField] private float introDuration = 0.0f;

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
    private int                   _flickBarsElapsed;

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
                    BeginIntroOrPlay();
                break;

            // Coroutine-driven hold before gameplay. SetState(Playing) fires on completion.
            // Update does nothing here; all logic lives in IntroDelayRoutine().
            case RoomState.IntroDelay:
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
                modeController.ClearVisuals(_currentDef != null &&
                                            (_currentDef.RoomType == RoomType.CallAndResponse ||
                                             _currentDef.RoomType == RoomType.FlashCard ||
                                             _currentDef.RoomType == RoomType.Flick));
                ApplyBackground();
                screenTransition.StartReveal();
                dungeonTransition.StartReveal();
                break;

            case RoomState.Playing:
                _roomStartTime = Time.realtimeSinceStartup;
                inputLayoutController?.SwitchLayout(
                    _currentDef != null ? _currentDef.InputLayout : InputLayoutType.ThreePad);
                if (_currentDef != null && _currentDef.RoomType == RoomType.DirectionChoice)
                {
                    // Direction-choice rooms: just show the door buttons, no combat.
                    if (doorsUI != null) doorsUI.SetActive(true);
                }
                else if (_currentDef != null && _currentDef.RoomType == RoomType.CallAndResponse)
                {
                    // Call-and-response room: same full setup as standard combat (music scheduling,
                    // visual init, timing/metronome) but with DungeonBeatManager suppressed so
                    // standard enemies don't spawn. C&R drives its own encounter loop.
                    if (doorsUI != null) doorsUI.SetActive(false);
                    modeController.StartRoomAudioVisual(_currentDef, _currentFloor);
                    if (callAndResponseController != null)
                    {
                        // Pass the seam time that StartRoomAudioVisual computed so the C&R
                        // encounter begins on the exact same beat boundary as standard combat would.
                        // StartEncounter's beat-snap is a no-op: the seam is already on the grid.
                        callAndResponseController.OnEncounterComplete += HandleCarEncounterComplete;
                        callAndResponseController.StartEncounter(
                            modeController.EncounterStartVirtualTime,
                            _currentDef.PhraseLengths);
                    }
                    else
                    {
                        Debug.LogError("[RoomController] CallAndResponseController not assigned — C&R room cannot start.");
                    }
                    // Health depletion during C&R must route through the same failure path as combat.
                    playerHealth.OnHealthDepleted += HandleHealthDepleted;
                }
                else if (_currentDef != null && _currentDef.RoomType == RoomType.FlashCard)
                {
                    // Flash Card room: same audio/visual setup as C&R (music scheduling, timing,
                    // metronome) with DungeonBeatManager suppressed. FlashCardController drives
                    // its own reveal/perform encounter loop.
                    if (doorsUI != null) doorsUI.SetActive(false);
                    modeController.StartRoomAudioVisual(_currentDef, _currentFloor);
                    if (flashCardController != null)
                    {
                        // Card count derives from the authored room length: each card is one
                        // 2-bar reveal/perform cycle, so cards = RoomLengthBars / 2. Zero =
                        // fall back to the controller's Inspector default.
                        int bars  = _currentDef.RoomLengthBars;
                        int cards = bars > 0 ? Mathf.Max(1, bars / 2) : 0;
                        if (bars > 0 && bars % 2 != 0)
                            Debug.LogWarning($"[RoomController] FlashCard room '{_currentDef.RoomName}' has odd " +
                                             $"RoomLengthBars={bars} — each card spans 2 bars; rounding down to {cards} card(s).");

                        // EncounterStartVirtualTime is already on the beat grid — the
                        // controller's beat-snap is a no-op, same as the C&R path.
                        flashCardController.OnEncounterComplete += HandleFlashCardEncounterComplete;
                        flashCardController.StartEncounter(
                            modeController.EncounterStartVirtualTime,
                            cards,
                            _currentDef.FlashCardNoteRange,
                            _currentDef.FlashCardVocabulary);
                    }
                    else
                    {
                        Debug.LogError("[RoomController] FlashCardController not assigned — FlashCard room cannot start.");
                    }
                    // Health depletion must route through the same failure path as combat.
                    playerHealth.OnHealthDepleted += HandleHealthDepleted;
                }
                else if (_currentDef != null && _currentDef.RoomType == RoomType.Flick)
                {
                    // Flick room: same audio/visual setup as C&R (music scheduling, timing,
                    // metronome) with DungeonBeatManager suppressed. The shared
                    // FlickEncounterDirector — the exact component the standalone Flick mode
                    // drives — owns the enemy loop; this controller only counts graded bars
                    // up to RoomLengthBars (kill count is score, not completion).
                    if (doorsUI != null) doorsUI.SetActive(false);
                    modeController.StartRoomAudioVisual(_currentDef, _currentFloor);
                    if (flickDirector != null && flickBeatManager != null)
                    {
                        // Sentinels restore the flick rig's scene defaults, so overrides are
                        // applied unconditionally — a vanilla flick room resets any tuning a
                        // previous flick room left behind.
                        flickDirector.ApplyRoomOverrides(
                            _currentDef.FlickRequiredBeatSlots,
                            _currentDef.FlickOffBeatSlots,
                            _currentDef.FlickBeatsPerCrossing,
                            _currentDef.FlickHitboxRadiusWorld,
                            _currentDef.FlickEnemyMaxHP,
                            _currentDef.FlickSurvivorAttackUnits);

                        // Same metronome/TimingCoordinator grid StartRoomAudioVisual set up;
                        // Initialize just re-derives the beat interval from the same BPM.
                        flickBeatManager.Initialize(modeController.CurrentRoomBpm);
                        flickDirector.SetSuiteVisible(true);

                        // Grading starts at EncounterStartVirtualTime — the same ramp policy
                        // every room type uses: first room of a floor gets the floor's
                        // musical lead-in bars, mid-floor rooms start on the next bar seam.
                        double flickGradingStart = modeController.EncounterStartVirtualTime;

                        // NOTHING runs before the grading start: cues are gated below, the
                        // sweep origin is the grading start (it holds at the left anchor
                        // until then), and the enemy pops in exactly at the grading start —
                        // the whole encounter starts together on the bar.
                        flickBeatManager.CueDuringLeadIn = false;
                        flickDirector.BeginEncounter(
                            flickGradingStart,
                            60.0 / modeController.CurrentRoomBpm,
                            flickGradingStart);

                        _flickBarsElapsed = 0;
                        // Subscribe AFTER BeginEncounter so the director's seam handler
                        // (retreat + ResetForSpawn) precedes the bar counter in multicast
                        // order — room-end teardown then cancels the just-scheduled spawn.
                        flickBeatManager.OnBarResolved += HandleFlickBarResolved;

                        flickBeatManager.Arm(flickGradingStart);
                    }
                    else
                    {
                        Debug.LogError("[RoomController] flickDirector/flickBeatManager not assigned — Flick room cannot start.");
                    }
                    // Health depletion must route through the same failure path as combat.
                    playerHealth.OnHealthDepleted += HandleHealthDepleted;
                }
                else
                {
                    // Standard combat encounter: hide doors, wire events, start the mode.
                    if (doorsUI != null) doorsUI.SetActive(false);
                    SubscribeEvents();
                    modeController.StartRoom(_currentDef, _currentFloor);
                }
                break;

            case RoomState.Exiting:
                if (doorsUI != null) doorsUI.SetActive(false);
                dungeonTransition.StartCover();
                break;
        }
    }

    // ── Intro Delay ───────────────────────────────────────────────────────────

    /// <summary>
    /// Called when the reveal animation completes. Applies the intro delay on the first room
    /// only (determined by <see cref="DungeonModeController.IsFirstRoom"/>, which is still
    /// true at this point — StartRoom() sets it to false later, inside SetState(Playing)).
    /// With introDuration == 0 or on subsequent rooms, advances directly to Playing
    /// (identical to previous behaviour). Either path ends in SetState(RoomState.Playing).
    /// </summary>
    private void BeginIntroOrPlay()
    {
        bool applyDelay = modeController != null
                       && modeController.IsFirstRoom
                       && introDuration > 0f;
        if (applyDelay)
        {
            _state = RoomState.IntroDelay;
            StartCoroutine(IntroDelayRoutine());
        }
        else
        {
            SetState(RoomState.Playing);
        }
    }

    /// <summary>
    /// Real-time hold between room reveal and gameplay start, first room only.
    /// Uses WaitForSecondsRealtime — this wait is entirely BEFORE GameClock.Reset()
    /// (which runs inside SetState(Playing) → StartRoom), so it does not affect
    /// the DSP time-zero or beat alignment. Music and beats start sample-aligned to
    /// gameplay; they simply begin later.
    ///
    /// FUTURE HOOK: replace the yield body with an intro animation sequence.
    /// When the animation finishes, call SetState(RoomState.Playing) to hand off.
    /// </summary>
    private System.Collections.IEnumerator IntroDelayRoutine()
    {
        yield return new WaitForSecondsRealtime(introDuration);
        SetState(RoomState.Playing);
    }

    // ── Event Handlers ────────────────────────────────────────────────────────

    private void SubscribeEvents()
    {
        modeController.OnModeComplete  += HandleModeComplete;
        playerHealth.OnHealthDepleted  += HandleHealthDepleted;
    }

    private void UnsubscribeEvents()
    {
        modeController.OnModeComplete                          -= HandleModeComplete;
        playerHealth.OnHealthDepleted                          -= HandleHealthDepleted;
        if (callAndResponseController != null)
            callAndResponseController.OnEncounterComplete      -= HandleCarEncounterComplete;
        if (flashCardController != null)
            flashCardController.OnEncounterComplete            -= HandleFlashCardEncounterComplete;
        if (flickBeatManager != null)
            flickBeatManager.OnBarResolved                     -= HandleFlickBarResolved;
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
        // Stop a running C&R / Flash Card encounter so it cannot fire OnEncounterComplete after death.
        callAndResponseController?.StopEncounter();
        if (flashCardController != null && flashCardController.IsRunning)
            flashCardController.StopEncounter();
        // Full flick teardown through the shared single entry point — unsubscribes the
        // enemy loop, stops FlickBeatManager (gating the reader off), drops any active
        // touch track, and resets ball/visual/enemy. Then the rig goes dormant and the
        // pads' touch handler is restored (game-over routing never calls SwitchLayout).
        if (_currentDef != null && _currentDef.RoomType == RoomType.Flick)
        {
            flickDirector?.TeardownEncounter();
            flickDirector?.SetSuiteVisible(false);
            inputLayoutController?.RestorePadTouch();
        }
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

    private void HandleCarEncounterComplete()
    {
        if (_state != RoomState.Playing) return;
        _state = RoomState.Evaluating;
        // Full unsubscribe (not just this handler) — the health-depleted subscription
        // added at room start must not accumulate across completed encounter rooms.
        UnsubscribeEvents();
        BuildResult(survived: true);
        SetState(RoomState.Exiting);
    }

    private void HandleFlashCardEncounterComplete()
    {
        if (_state != RoomState.Playing) return;
        _state = RoomState.Evaluating;
        // Full unsubscribe (not just this handler) — same rationale as the C&R handler.
        UnsubscribeEvents();
        BuildResult(survived: true);
        SetState(RoomState.Exiting);
    }

    /// <summary>
    /// Counts graded flick bars up to RoomLengthBars, then completes the room via the
    /// same path as C&R. Grace bars (enemy killed mid-bar) COUNT — a kill shortens
    /// the danger, not the room. Lead-in bars never fire OnBarResolved, so only
    /// graded bars are counted.
    /// </summary>
    private void HandleFlickBarResolved(FlickBarResult result)
    {
        if (_state != RoomState.Playing) return;

        _flickBarsElapsed++;
        int roomBars = Mathf.Max(1, _currentDef != null ? _currentDef.RoomLengthBars : 1);
        if (_flickBarsElapsed < roomBars) return;

        _state = RoomState.Evaluating;
        UnsubscribeEvents();
        // Single teardown entry point (also cancels the spawn the director just
        // scheduled for the next bar), then the rig sleeps until the next flick room.
        flickDirector?.TeardownEncounter();
        flickDirector?.SetSuiteVisible(false);
        BuildResult(survived: true);
        SetState(RoomState.Exiting);
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
