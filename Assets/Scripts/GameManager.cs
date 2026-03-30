using System.Collections;
using UnityEngine;

/// <summary>
/// Central game coordinator. Manages game lifecycle and orchestrates shared subsystems.
/// Mode-specific logic (beat generation, evaluation, visuals) lives in mode controllers.
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
    [Header("Shared Timing Systems")]
    [SerializeField] public Metronome metronome;
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

    [Header("Mode Controllers")]
    [SerializeField] private BongoModeController bongoMode;
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

        pauseHandler = GetComponent<PauseHandler>();
        sceneLoader = SceneLoadManager.instance;

        if (bongoMode != null)
        {
            bongoMode.OnModeComplete += HandleModeComplete;
        }

        Debug.Log("[GameManager] Initialized");
    }

    private void OnDestroy()
    {
        if (bongoMode != null)
        {
            bongoMode.OnModeComplete -= HandleModeComplete;
        }
    }
    #endregion

    #region Public API
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
        if (bongoMode != null)
        {
            bongoMode.SetDifficulty(difficultyIndex);
        }
    }

    public void SetMusic(int songIndex)
    {
        if (metronome == null || AudioManager.instance == null)
        {
            Debug.LogError("[GameManager] Cannot set music - missing references");
            return;
        }

        metronome.bpm = songIndex switch
        {
            0 => 111,
            1 => 111,
            2 => 94,
            3 => 150,
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

        // 4. Calculate synchronized start time
        const double LOOKAHEAD_TIME = 0.05;
        double baseStartTime = AudioSettings.dspTime + LOOKAHEAD_TIME;
        double virtualStartTime = GameClock.Instance.RealDspToVirtual(baseStartTime);

        Debug.Log($"[GameManager] === SYNCHRONIZED TIMING ===");
        Debug.Log($"  Current Real DSP: {AudioSettings.dspTime:F4}");
        Debug.Log($"  Start Real DSP:   {baseStartTime:F4}");
        Debug.Log($"  Start Virtual:    {virtualStartTime:F4}");
        Debug.Log($"  Lookahead:        {LOOKAHEAD_TIME * 1000:F1}ms");

        // 5. Initialize metronome (shared, used for visual feedback across modes)
        if (metronome != null)
        {
            metronome.InitializeWithStartTime(virtualStartTime);
            metronome.enabled = true;
        }

        // 6. Initialize TimingCoordinator (shared source of truth)
        if (timingCoordinator == null)
        {
            Debug.LogError("[GameManager] TimingCoordinator is missing!");
            yield break;
        }

        // TimingCoordinator needs song length - ask the active mode controller
        if (bongoMode == null)
        {
            Debug.LogError("[GameManager] No mode controller assigned!");
            yield break;
        }

        int totalBeats = bongoMode.CalculateTotalBeats((int)metronome.bpm);

        timingCoordinator.Initialize(
            virtualStartTime,
            metronome.bpm,
            totalBeats,
            bongoMode.BarsBeforeEndForFinalBar
        );

        // 7. Reset GameClock
        if (GameClock.Instance != null)
        {
            GameClock.Instance.Reset();
        }

        // 8. Hand off to Bongo mode controller
        bongoMode.StartMode(
            (int)metronome.bpm,
            virtualStartTime,
            baseStartTime   // Real DSP for audio scheduling
        );

        currentPhase = GamePhase.Playing;

        Debug.Log("=== GAME SEQUENCE COMPLETE ===");
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
            pageToCloseOnStart.SetActive(false);

        if (gameplayElements != null)
            gameplayElements.SetActive(true);

        if (gameplayElementsObject != null)
            gameplayElementsObject.SetActive(true);

        if (blinking != null)
        {
            blinking.enabled = false;
            if (blinking.transform.childCount > 0)
                blinking.transform.GetChild(0).gameObject.SetActive(false);
        }
    }
    #endregion

    #region Game Sequence - End
    private void HandleModeComplete()
    {
        if (currentPhase == GamePhase.ShowingResults)
        {
            Debug.LogWarning("[GameManager] Already showing results");
            return;
        }

        currentPhase = GamePhase.ShowingResults;

        Debug.Log("[GameManager] Mode complete - transitioning to results");

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

        // 2. Cleanup shared systems
        CleanupSharedSystems();

        // 3. Cleanup mode-specific systems
        bongoMode.Cleanup();

        // 4. Show results UI (score data sourced from mode controller)
        ShowResultsUI();

        // 5. Screen transition (reveal)
        if (screenTransition != null)
        {
            screenTransition.StartReveal();
        }

        Debug.Log("=== RESULTS SEQUENCE COMPLETE ===");
    }

    private void CleanupSharedSystems()
    {
        if (gameplayElements != null)
        {
            gameplayElements.SetActive(false);
            Debug.Log("[GameManager] Gameplay elements hidden");
        }

        if (metronome != null && metronome.enabled)
        {
            metronome.ResetToInitialState();
            metronome.enabled = false;
            Debug.Log("[GameManager] Metronome disabled");
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
        if (scoreScreen != null)
        {
            scoreScreen.DisplayScore(bongoMode.Score, bongoMode.TotalPerfectHits);
            Debug.Log($"[GameManager] Score displayed: {bongoMode.Score}");
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

        if (metronome != null) metronome.ResetToInitialState();
        if (GameClock.Instance != null) GameClock.Instance.Reset();
        if (timingCoordinator != null) timingCoordinator.Reset();
        if (bongoMode != null) bongoMode.ResetToInitialState();

        currentPhase = GamePhase.Menu;

        Debug.Log("[GameManager] Reset complete");
    }
    #endregion
}