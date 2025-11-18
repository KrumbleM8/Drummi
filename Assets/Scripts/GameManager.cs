using System.Collections;
using UnityEngine;

/// <summary>
/// Central game coordinator. Manages game lifecycle and orchestrates subsystems.
/// Simplified initialization flow with clear state management.
/// </summary>
public class GameManager : MonoBehaviour
{
    #region Singleton
    public static GameManager instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }
    #endregion

    #region Inspector References
    [Header("Core Gameplay Systems")]
    [SerializeField] public Metronome metronome;
    [SerializeField] private BeatGenerator beatGenerator;
    [SerializeField] private BeatEvaluator beatEvaluator;
    [SerializeField] private BeatVisualScheduler visualScheduler;
    [SerializeField] private PlayerInputVisualHandler playerInputVisual;

    [Header("UI & Transitions")]
    [SerializeField] private ScreenTransition screenTransition;
    [SerializeField] private UIMenuManager menuManager;
    [SerializeField] private GameObject gameplayElements;
    [SerializeField] private GameObject pageToCloseOnStart;

    [Header("Misc")]
    [SerializeField] private GameObject gameplayElementsObject; // TODO: Consolidate with gameplayElements
    [SerializeField] private EyeBlinker blinking;

    [Header("Delegates")]
    [SerializeField] private PauseHandler pauseHandler;
    [SerializeField] private SceneLoadManager sceneLoader;
    #endregion

    #region Game State
    private enum GamePhase
    {
        Menu,
        Initializing,
        Playing,
        ShowingResults
    }

    private GamePhase currentPhase = GamePhase.Menu;
    private bool hasEverPlayed = false; // Track if this is first play or replay
    #endregion

    #region Lifecycle
    private void Start()
    {
        Time.timeScale = 1f;

        // Initialize delegates
        pauseHandler = GetComponent<PauseHandler>();
        sceneLoader = SceneLoadManager.instance;

        // Subscribe to events
        if (beatGenerator != null)
        {
            beatGenerator.OnSongComplete += HandleSongComplete;
        }

        Debug.Log("[GameManager] Initialized");
    }

    private void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks
        if (beatGenerator != null)
        {
            beatGenerator.OnSongComplete -= HandleSongComplete;
        }
    }
    #endregion

    #region Public API - Game Control
    /// <summary>
    /// Start a new game session.
    /// </summary>
    public void StartGame()
    {
        if (currentPhase == GamePhase.Playing)
        {
            Debug.LogWarning("[GameManager] Game already in progress");
            return;
        }

        StartCoroutine(StartGameSequence());
    }

    /// <summary>
    /// Set difficulty level (0=starter, 1=standard, 2=spicy).
    /// </summary>
    public void SetDifficulty(int difficultyIndex)
    {
        if (beatGenerator != null)
        {
            beatGenerator.difficultyIndex = difficultyIndex;
            Debug.Log($"[GameManager] Difficulty set to {difficultyIndex}");
        }
    }

    /// <summary>
    /// Set music track and BPM.
    /// </summary>
    public void SetMusic(int songIndex)
    {
        if (metronome == null || AudioManager.instance == null)
        {
            Debug.LogError("[GameManager] Cannot set music - missing references");
            return;
        }

        // Set BPM based on song
        metronome.bpm = songIndex switch
        {
            0 => 111,
            1 => 111,
            2 => 79,
            _ => 105
        };

        // Set audio manager's selected song
        AudioManager.instance.selectedSongIndex = songIndex;

        // Notify subsystems to recalculate beat intervals
        InitializeSubsystemTiming();

        Debug.Log($"[GameManager] Music set to index {songIndex}, BPM: {metronome.bpm}");
    }

    /// <summary>
    /// Toggle pause state.
    /// </summary>
    public void TogglePause()
    {
        if (pauseHandler != null)
        {
            pauseHandler.TogglePause();
        }
    }

    /// <summary>
    /// Reset game and return to main menu.
    /// </summary>
    public void ResetDrummi()
    {
        if (sceneLoader != null)
        {
            sceneLoader.ResetDrummi();
        }
    }
    #endregion

    #region Game Sequence - Start
    private IEnumerator StartGameSequence()
    {
        currentPhase = GamePhase.Initializing;

        Debug.Log("=== STARTING UP GAME SEQUENCE ===");

        // 1. Screen transition (cover)
        yield return StartCoroutine(TransitionScreenCover());

        // 2. Hide menu, show gameplay UI
        SetupGameplayUI();

        // 3. Screen transition (reveal)
        screenTransition.StartReveal();

        // 4. Initialize game systems
        InitializeGameSystems();

        // 5. Prepare metronome
        PrepareMetronome();
        metronome.Initialize();

        // 6. Get synchronized start time
        double startTime = metronome.VirtualDspTime;
        Debug.Log($"[GameManager] Synchronized start time: {startTime:F4}");

        // 7. Schedule music
        ScheduleMusic(startTime);

        // 8. Enable metronome if needed (first play)
        if (!hasEverPlayed && metronome != null)
        {
            metronome.enabled = true;
            Debug.Log("[GameManager] Metronome enabled (first play)");
        }

        // 9. Synchronize visual systems
        SynchronizeVisuals(startTime);

        // 10. Start beat generator gameplay
        beatGenerator.StartGameplay(startTime);

        // 11. Mark state
        currentPhase = GamePhase.Playing;
        hasEverPlayed = true;

        Debug.Log("=== GAME SEQUENCE COMPLETE ===");

        yield return null;
    }

    private IEnumerator TransitionScreenCover()
    {
        if (screenTransition == null)
        {
            Debug.LogError("[GameManager] Screen transition missing!");
            yield break;
        }

        screenTransition.StartCover();

        while (!screenTransition.IsScreenCovered)
        {
            yield return null;
        }
    }

    private void SetupGameplayUI()
    {
        // Hide menu page
        if (pageToCloseOnStart != null)
        {
            pageToCloseOnStart.SetActive(false);
        }

        // Show gameplay elements
        if (gameplayElements != null)
        {
            gameplayElements.SetActive(true);
        }

        if (gameplayElementsObject != null)
        {
            gameplayElementsObject.SetActive(true);
        }

        // Disable eye blink animation during gameplay
        if (blinking != null)
        {
            blinking.enabled = false;
            if (blinking.transform.childCount > 0)
            {
                blinking.transform.GetChild(0).gameObject.SetActive(false);
            }
        }
    }

    private void InitializeGameSystems()
    {
        // Reset game clock if this is a replay
        if (hasEverPlayed && GameClock.Instance != null)
        {
            GameClock.Instance.Reset();
            Debug.Log("[GameManager] GameClock reset for replay");
        }

        // Initialize beat generator with song parameters
        if (beatGenerator != null)
        {
            beatGenerator.enabled = true;
            beatGenerator.Initialize(
                (int)metronome.bpm,
                AudioManager.instance.selectedSongIndex
            );
            Debug.Log("[GameManager] BeatGenerator initialized");
        }

        // Initialize visual schedulers
        if (visualScheduler != null)
        {
            visualScheduler.enabled = true;
            visualScheduler.InitalizeBeatValues();
            Debug.Log("[GameManager] BeatVisualScheduler initialized");
        }

        if (playerInputVisual != null)
        {
            playerInputVisual.enabled = true;
            playerInputVisual.InitializeBeatValues();
            Debug.Log("[GameManager] PlayerInputVisualHandler initialized");
        }
    }

    private void PrepareMetronome()
    {
        if (metronome == null) return;

        // On replay, metronome is already enabled but needs timing refresh
        if (hasEverPlayed)
        {
            metronome.RefreshTimingForGameStart();
            metronome.enabled = true;
            Debug.Log("[GameManager] Metronome timing refreshed for replay");
        }
        // On first play, metronome.Start() will be called when enabled later
    }

    private void ScheduleMusic(double startTime)
    {
        if (AudioManager.instance == null)
        {
            Debug.LogError("[GameManager] AudioManager missing!");
            return;
        }

        AudioManager.instance.scheduledStartTime = startTime;
        AudioManager.instance.PlayMusic();

        Debug.Log($"[GameManager] Music scheduled at: {startTime:F4}");
    }

    private void SynchronizeVisuals(double startTime)
    {
        if (visualScheduler != null)
        {
            visualScheduler.SyncWithMetronome(startTime);
        }

        if (playerInputVisual != null)
        {
            playerInputVisual.SyncWithMetronome(startTime);
        }

        Debug.Log("[GameManager] Visuals synchronized");
    }

    private void InitializeSubsystemTiming()
    {
        // Notify all systems that BPM has changed
        if (visualScheduler != null) visualScheduler.InitalizeBeatValues();
        if (playerInputVisual != null) playerInputVisual.InitializeBeatValues();
        if (beatGenerator != null) beatGenerator.Initialize((int)metronome.bpm, AudioManager.instance.selectedSongIndex);

        Debug.Log("[GameManager] Subsystem timing initialized");
    }
    #endregion

    #region Game Sequence - End
    private void HandleSongComplete()
    {
        if (currentPhase == GamePhase.ShowingResults)
        {
            Debug.LogWarning("[GameManager] Already showing results");
            return;
        }

        currentPhase = GamePhase.ShowingResults;

        Debug.Log("[GameManager] Song complete - transitioning to results");

        StartCoroutine(ShowResultsSequence());
    }

    private IEnumerator ShowResultsSequence()
    {
        Debug.Log("=== SHOWING RESULTS SEQUENCE ===");

        // 1. Screen transition (cover)
        if (screenTransition != null)
        {
            screenTransition.StartCover();

            while (!screenTransition.IsScreenCovered)
            {
                yield return null;
            }
        }

        // 2. Cleanup gameplay systems
        CleanupGameplaySystems();

        // 3. Show results UI
        ShowResultsUI();

        // 4. Screen transition (reveal)
        if (screenTransition != null)
        {
            screenTransition.StartReveal();
        }

        Debug.Log("=== RESULTS SEQUENCE COMPLETE ===");
    }

    private void CleanupGameplaySystems()
    {
        // Hide gameplay elements
        if (gameplayElements != null)
        {
            gameplayElements.SetActive(false);
            Debug.Log("[GameManager] Gameplay elements hidden");
        }

        // Cleanup visual schedulers
        if (visualScheduler != null)
        {
            visualScheduler.CleanupAndDisable();
            Debug.Log("[GameManager] BeatVisualScheduler cleaned up");
        }

        if (playerInputVisual != null)
        {
            playerInputVisual.CleanupAndDisable();
            Debug.Log("[GameManager] PlayerInputVisualHandler cleaned up");
        }

        // Disable metronome
        if (metronome != null && metronome.enabled)
        {
            metronome.ResetToInitialState();
            metronome.enabled = false;
            Debug.Log("[GameManager] Metronome disabled");
        }

        // Cleanup beat generator
        if (beatGenerator != null)
        {
            beatGenerator.ResetToInitialState();
            Debug.Log("[GameManager] BeatGenerator cleaned up");
        }

        // Stop music
        if (AudioManager.instance != null)
        {
            AudioManager.instance.StopMusic();
            Debug.Log("[GameManager] Music stopped");
        }
    }

    private void ShowResultsUI()
    {
        if (menuManager == null)
        {
            Debug.LogError("[GameManager] UIMenuManager missing!");
            return;
        }

        // Show score page
        menuManager.ShowPageImmediate("Score");

        // Find and initialize score screen
        var scoreScreen = menuManager.currentPage.pageTransform.GetComponent<ScoreScreen>();
        if (scoreScreen != null && beatEvaluator != null)
        {
            scoreScreen.DisplayScore(beatEvaluator.Score, beatEvaluator.PerfectHits);
            Debug.Log($"[GameManager] Score displayed: {beatEvaluator.Score}");
        }
        else
        {
            Debug.LogError("[GameManager] ScoreScreen component not found!");
        }

        currentPhase = GamePhase.Menu;
    }

    /// <summary>
    /// Reset all game values to initial state. Called when returning to menu.
    /// </summary>
    public void ResetGameValues()
    {
        Debug.Log("[GameManager] Resetting game values");

        if (beatGenerator != null) beatGenerator.ResetToInitialState();
        if (visualScheduler != null) visualScheduler.ResetToInitialState();
        if (playerInputVisual != null) playerInputVisual.ResetToInitialState();
        if (metronome != null) metronome.ResetToInitialState();
        if (beatEvaluator != null) beatEvaluator.ResetScore();
        if (GameClock.Instance != null) GameClock.Instance.Reset();

        hasEverPlayed = false;
        currentPhase = GamePhase.Menu;

        Debug.Log("[GameManager] Reset complete");
    }
    #endregion
}