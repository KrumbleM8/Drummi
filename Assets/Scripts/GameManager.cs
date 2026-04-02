using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central game coordinator. Manages game lifecycle and orchestrates shared subsystems.
/// Mode-specific logic lives in ModeController subclasses.
///
/// ADDING A NEW MODE:
///   1. Implement a ModeController subclass.
///   2. Add its component to a GameObject in the scene.
///   3. Drag it into the modeControllers list in this inspector.
///   4. Call SetMode("YourModeId") before StartGame() — e.g. from a menu button.
/// </summary>
public class GameManager : MonoBehaviour
{
    #region Singleton
    public static GameManager instance;

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
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

    [Header("Mode Controllers")]
    [Tooltip("Register all ModeController implementations here. " +
             "Call SetMode(modeId) before StartGame() to choose the active mode.")]
    [SerializeField] private List<ModeController> modeControllers = new();

    [Tooltip("ModeId to activate at Start() if SetMode() has not been called yet.")]
    [SerializeField] private string defaultModeId = "";
    #endregion

    #region Game State
    private enum GamePhase { Menu, Initializing, Playing, ShowingResults }
    private GamePhase currentPhase = GamePhase.Menu;

    private ModeController _activeMode;

    /// <summary>The currently active mode controller. Null until SetMode() is called.</summary>
    public ModeController ActiveMode => _activeMode;
    #endregion

    #region Mode Registry
    private Dictionary<string, ModeController> _modeRegistry;

    private void BuildRegistry()
    {
        _modeRegistry = new Dictionary<string, ModeController>(modeControllers.Count);

        foreach (ModeController mode in modeControllers)
        {
            if (mode == null)
            {
                Debug.LogWarning("[GameManager] Null entry in modeControllers list — skipping.");
                continue;
            }

            if (_modeRegistry.ContainsKey(mode.ModeId))
            {
                Debug.LogWarning($"[GameManager] Duplicate ModeId '{mode.ModeId}' — second entry ignored.");
                continue;
            }

            _modeRegistry[mode.ModeId] = mode;
            Debug.Log($"[GameManager] Registered mode: '{mode.ModeId}'");
        }
    }
    #endregion

    #region Lifecycle
    private void Start()
    {
        Time.timeScale = 1f;
        pauseHandler = GetComponent<PauseHandler>();

        BuildRegistry();

        if (!string.IsNullOrEmpty(defaultModeId))
            SetMode(defaultModeId);

        Debug.Log($"[GameManager] Initialized — {_modeRegistry.Count} mode(s) registered.");
    }

    private void OnDestroy()
    {
        UnsubscribeActiveMode();
    }
    #endregion

    #region Public API
    /// <summary>
    /// Switch to a registered mode by its ModeId.
    /// Call this from menu buttons before StartGame().
    /// e.g. SetMode("Bongo") / SetMode("GuitarHero") / SetMode("WorldRhythms")
    /// </summary>
    public void SetMode(string modeId)
    {
        if (_modeRegistry == null) BuildRegistry();

        if (!_modeRegistry.TryGetValue(modeId, out ModeController next))
        {
            Debug.LogError($"[GameManager] SetMode failed — no mode with id '{modeId}' registered.");
            return;
        }

        UnsubscribeActiveMode();
        _activeMode = next;
        _activeMode.OnModeComplete += HandleModeComplete;

        Debug.Log($"[GameManager] Active mode → '{modeId}'");
    }

    /// <summary>Start the game using the currently active mode.</summary>
    public void StartGame()
    {
        if (currentPhase == GamePhase.Playing)
        {
            Debug.LogWarning("[GameManager] Game already in progress.");
            return;
        }

        if (_activeMode == null)
        {
            Debug.LogError("[GameManager] StartGame called but no mode is active. Call SetMode() first.");
            return;
        }

        StartCoroutine(StartGameSequence());
    }

    public void SetDifficulty(int difficultyIndex)
    {
        if (_activeMode == null) { Debug.LogWarning("[GameManager] SetDifficulty — no active mode."); return; }
        _activeMode.SetDifficulty(difficultyIndex);
    }

    public void SetMusic(int songIndex)
    {
        if (metronome == null || AudioManager.instance == null)
        {
            Debug.LogError("[GameManager] Cannot set music — missing references.");
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

    public void TogglePause() => pauseHandler?.TogglePause();

    public void ResetDrummi() => SceneLoadManager.instance.ResetDrummi();

    public void ResetGameValues()
    {
        Debug.Log("[GameManager] Resetting game values");

        if (metronome != null) metronome.ResetToInitialState();
        if (GameClock.Instance != null) GameClock.Instance.Reset();
        if (timingCoordinator != null) timingCoordinator.Reset();
        if (_activeMode != null) _activeMode.ResetToInitialState();

        currentPhase = GamePhase.Menu;
        Debug.Log("[GameManager] Reset complete");
    }
    #endregion

    #region Game Sequence — Start
    private IEnumerator StartGameSequence()
    {
        currentPhase = GamePhase.Initializing;
        Debug.Log("=== STARTING UP GAME SEQUENCE ===");

        yield return StartCoroutine(TransitionScreenCover());

        SetupGameplayUI();
        screenTransition.StartReveal();

        const double LOOKAHEAD_TIME = 0.05;
        double baseStartTime = AudioSettings.dspTime + LOOKAHEAD_TIME;
        double virtualStartTime = GameClock.Instance.RealDspToVirtual(baseStartTime);

        Debug.Log($"[GameManager] === SYNCHRONIZED TIMING ===");
        Debug.Log($"  Current Real DSP: {AudioSettings.dspTime:F4}");
        Debug.Log($"  Start Real DSP:   {baseStartTime:F4}");
        Debug.Log($"  Start Virtual:    {virtualStartTime:F4}");
        Debug.Log($"  Lookahead:        {LOOKAHEAD_TIME * 1000:F1}ms");

        if (metronome != null)
        {
            metronome.InitializeWithStartTime(virtualStartTime);
            metronome.enabled = true;
        }

        if (timingCoordinator == null)
        {
            Debug.LogError("[GameManager] TimingCoordinator is missing!");
            yield break;
        }

        int totalBeats = _activeMode.CalculateTotalBeats((int)metronome.bpm);

        timingCoordinator.Initialize(
            virtualStartTime,
            metronome.bpm,
            totalBeats,
            _activeMode.BarsBeforeEndForFinalBar
        );

        GameClock.Instance?.Reset();

        _activeMode.StartMode((int)metronome.bpm, virtualStartTime, baseStartTime);

        currentPhase = GamePhase.Playing;
        Debug.Log($"=== GAME SEQUENCE COMPLETE — Mode: '{_activeMode.ModeId}' ===");
    }

    private IEnumerator TransitionScreenCover()
    {
        if (screenTransition == null)
        {
            Debug.LogError("[GameManager] Screen transition missing!");
            yield break;
        }
        screenTransition.StartCover();
        while (!screenTransition.IsScreenCovered) yield return null;
    }

    private void SetupGameplayUI()
    {
        if (pageToCloseOnStart != null) pageToCloseOnStart.SetActive(false);
        if (gameplayElements != null) gameplayElements.SetActive(true);
        if (gameplayElementsObject != null) gameplayElementsObject.SetActive(true);

        if (blinking != null)
        {
            blinking.enabled = false;
            if (blinking.transform.childCount > 0)
                blinking.transform.GetChild(0).gameObject.SetActive(false);
        }
    }
    #endregion

    #region Game Sequence — End
    private void HandleModeComplete()
    {
        if (currentPhase == GamePhase.ShowingResults)
        {
            Debug.LogWarning("[GameManager] Already showing results.");
            return;
        }
        currentPhase = GamePhase.ShowingResults;
        Debug.Log($"[GameManager] Mode '{_activeMode.ModeId}' complete — transitioning to results.");
        StartCoroutine(ShowResultsSequence());
    }

    private IEnumerator ShowResultsSequence()
    {
        Debug.Log("=== SHOWING RESULTS SEQUENCE ===");

        if (screenTransition != null)
        {
            screenTransition.StartCover();
            while (!screenTransition.IsScreenCovered) yield return null;
        }

        CleanupSharedSystems();
        _activeMode.Cleanup();
        ShowResultsUI();
        screenTransition?.StartReveal();

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
        if (menuManager == null) { Debug.LogError("[GameManager] UIMenuManager missing!"); return; }

        menuManager.ShowPageImmediate("Score");

        var scoreScreen = menuManager.currentPage.pageTransform.GetComponent<ScoreScreen>();
        if (scoreScreen != null)
        {
            scoreScreen.DisplayScore(_activeMode.Score, _activeMode.TotalPerfectHits);
            Debug.Log($"[GameManager] Score displayed: {_activeMode.Score}");
        }
        else
        {
            Debug.LogError("[GameManager] ScoreScreen component not found!");
        }

        currentPhase = GamePhase.Menu;
    }
    #endregion

    #region Private Helpers
    private void UnsubscribeActiveMode()
    {
        if (_activeMode != null)
            _activeMode.OnModeComplete -= HandleModeComplete;
    }
    #endregion
}