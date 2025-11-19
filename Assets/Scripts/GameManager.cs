using System.Collections;
using UnityEngine;

/// <summary>
/// Central game coordinator. Manages game lifecycle and orchestrates subsystems.
/// REFACTORED: Uses TimingCoordinator for synchronized timing
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
    [SerializeField] private TimingCoordinator timingCoordinator;

    [Header("UI & Transitions")]
    [SerializeField] private ScreenTransition screenTransition;
    [SerializeField] private UIMenuManager menuManager;
    [SerializeField] private GameObject gameplayElements;
    [SerializeField] private GameObject pageToCloseOnStart;

    [Header("Misc")]
    [SerializeField] private GameObject gameplayElementsObject;
    [SerializeField] private EyeBlinker blinking;

    [Header("Delegates")]
    [SerializeField] private PauseHandler pauseHandler;
    [SerializeField] private SceneLoadManager sceneLoader;

    [Header("Song Progression")]
    [SerializeField] private int barsBeforeEndForFinalBar = 1;
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
        if (beatGenerator != null)
        {
            beatGenerator.OnSongComplete -= HandleSongComplete;
        }
    }
    #endregion

    #region Public API - Game Control
    public void StartGame()
    {
        if (currentPhase == GamePhase.Playing)
        {
            Debug.LogWarning("[GameManager] Game already in progress");
            return;
        }

        StartCoroutine(StartGameSequence());
    }

    public void SetDifficulty(int difficultyIndex)
    {
        if (beatGenerator != null)
        {
            beatGenerator.difficultyIndex = difficultyIndex;
            Debug.Log($"[GameManager] Difficulty set to {difficultyIndex}");
        }
    }

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
            2 => 94,
            _ => 105
        };

        AudioManager.instance.selectedSongIndex = songIndex;

        Debug.Log($"[GameManager] Music set to index {songIndex}, BPM: {metronome.bpm}");
    }

    public void TogglePause()
    {
        if (pauseHandler != null)
        {
            pauseHandler.TogglePause();
        }
    }

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

        // 4. Calculate synchronized start time with minimal lookahead
        const double LOOKAHEAD_TIME = 0.05; // 50ms - minimal but safe
        double synchronizedStartTime = AudioSettings.dspTime + LOOKAHEAD_TIME;

        Debug.Log($"[GameManager] === SYNCHRONIZED TIMING ===");
        Debug.Log($"  Current DSP: {AudioSettings.dspTime:F4}");
        Debug.Log($"  Start time: {synchronizedStartTime:F4}");
        Debug.Log($"  Lookahead: {LOOKAHEAD_TIME * 1000:F1}ms");

        // 5. Get song info
        AudioClip clip = AudioManager.instance.musicTracks[AudioManager.instance.selectedSongIndex];
        if (clip == null)
        {
            Debug.LogError("[GameManager] Song clip is null!");
            yield break;
        }

        double beatInterval = 60.0 / metronome.bpm;
        int totalBeats = Mathf.FloorToInt((float)(clip.length / beatInterval));

        Debug.Log($"  Song: {clip.name}");
        Debug.Log($"  BPM: {metronome.bpm}");
        Debug.Log($"  Total beats: {totalBeats}");
        Debug.Log($"  Duration: {clip.length:F2}s");

        // 6. Initialize TimingCoordinator FIRST (single source of truth)
        if (timingCoordinator != null)
        {
            timingCoordinator.Initialize(
                synchronizedStartTime,
                metronome.bpm,
                totalBeats,
                barsBeforeEndForFinalBar
            );
        }
        else
        {
            Debug.LogError("[GameManager] TimingCoordinator is missing!");
            yield break;
        }

        // 7. Initialize game systems with synchronized timing
        InitializeGameSystems();

        // 8. Initialize metronome (for visual feedback only)
        if (metronome != null)
        {
            metronome.InitializeWithStartTime(synchronizedStartTime);
            metronome.enabled = true;
        }

        // 9. Schedule music
        ScheduleMusic(synchronizedStartTime);

        // 10. Synchronize visual systems
        SynchronizeVisuals();

        // 11. Start beat generator gameplay
        beatGenerator.StartGameplay(synchronizedStartTime);

        // 12. Reset GameClock
        if (GameClock.Instance != null)
        {
            GameClock.Instance.Reset();
        }

        // 13. Mark state
        currentPhase = GamePhase.Playing;

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
        if (pageToCloseOnStart != null)
        {
            pageToCloseOnStart.SetActive(false);
        }

        if (gameplayElements != null)
        {
            gameplayElements.SetActive(true);
        }

        if (gameplayElementsObject != null)
        {
            gameplayElementsObject.SetActive(true);
        }

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

    private void SynchronizeVisuals()
    {
        // Visual systems now use TimingCoordinator directly
        if (visualScheduler != null)
        {
            visualScheduler.SyncWithTimingCoordinator();
        }

        if (playerInputVisual != null)
        {
            playerInputVisual.SyncWithTimingCoordinator();
        }

        Debug.Log("[GameManager] Visuals synchronized with TimingCoordinator");
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
        if (gameplayElements != null)
        {
            gameplayElements.SetActive(false);
            Debug.Log("[GameManager] Gameplay elements hidden");
        }

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

        if (metronome != null && metronome.enabled)
        {
            metronome.ResetToInitialState();
            metronome.enabled = false;
            Debug.Log("[GameManager] Metronome disabled");
        }

        if (beatGenerator != null)
        {
            beatGenerator.ResetToInitialState();
            Debug.Log("[GameManager] BeatGenerator cleaned up");
        }

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

        menuManager.ShowPageImmediate("Score");

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

    public void ResetGameValues()
    {
        Debug.Log("[GameManager] Resetting game values");

        if (beatGenerator != null) beatGenerator.ResetToInitialState();
        if (visualScheduler != null) visualScheduler.ResetToInitialState();
        if (playerInputVisual != null) playerInputVisual.ResetToInitialState();
        if (metronome != null) metronome.ResetToInitialState();
        if (beatEvaluator != null) beatEvaluator.ResetScore();
        if (GameClock.Instance != null) GameClock.Instance.Reset();
        if (timingCoordinator != null) timingCoordinator.Reset();

        currentPhase = GamePhase.Menu;

        Debug.Log("[GameManager] Reset complete");
    }
    #endregion
}