using UnityEngine;

/// <summary>
/// Top-level orchestrator for the Guitar Hero system.
/// Responsible for:
///   - Initialising TimingCoordinator and HitJudge from SongChart BPM
///   - Starting, pausing, resuming, and stopping the game session
///   - Owning the AudioSource and scheduling playback via GameClock DSP time
///   - Detecting song completion and firing OnSongComplete
///
/// SETUP:
///   Attach to the same GameObject as NoteSpawner, RhythmLaneManager,
///   RhythmInputHandler, and HitJudge. Assign a SongChart and an AudioSource.
///
/// START FLOW:
///   1. StartGame() is called (manually or from a UI button)
///   2. GameClock is reset
///   3. AudioSource is scheduled via VirtualToRealDsp so audio stays in sync
///   4. TimingCoordinator is initialised with BPM and total beats
///   5. HitJudge windows are scaled to BPM
///   6. NoteSpawner begins streaming notes
/// </summary>
public class ArcadeGameController : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────

    public static ArcadeGameController Instance { get; private set; }

    // ── Inspector ─────────────────────────────────────────────────────────

    [Header("Chart")]
    [Tooltip("Assign the same SongChart that NoteSpawner uses.")]
    public SongChart chart;

    [Header("Audio")]
    [Tooltip("AudioSource used for song playback. Should have no clip pre-assigned; " +
             "the controller sets it from the chart at runtime.")]
    public AudioSource audioSource;

    [Tooltip("Delay in seconds between StartGame() and first audio sample. " +
             "Gives NoteSpawner time to pre-spawn notes before music starts.")]
    [Min(0f)]
    public float startDelaySeconds = 1f;

    [Header("Total Beats")]
    [Tooltip("Total beats in the song. For a looping chart, set this high or calculate " +
             "from chart.loopLengthBeats * desired loop count. " +
             "For a finite song: (AudioClip.length / chart.SecondsPerBeat).")]
    public int totalBeats = 256;

    // ── Events ────────────────────────────────────────────────────────────

    /// <summary>Fired when the song/chart reaches completion.</summary>
    public event System.Action OnSongComplete;

    /// <summary>Fired when the game is paused.</summary>
    public event System.Action OnPaused;

    /// <summary>Fired when the game is resumed.</summary>
    public event System.Action OnResumed;

    // ── State ─────────────────────────────────────────────────────────────

    public bool IsRunning { get; private set; }
    public bool IsPaused => GameClock.Instance.IsPaused;

    // ── Private ───────────────────────────────────────────────────────────

    private NoteSpawner _noteSpawner;
    private RhythmLaneManager _laneManager;
    private HitJudge _hitJudge;
    private bool _songCompleteHandled;

    // ── Unity ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _noteSpawner = GetComponent<NoteSpawner>();
        _laneManager = GetComponent<RhythmLaneManager>();
        _hitJudge = GetComponent<HitJudge>();
    }

    void Update()
    {
        if (!IsRunning || IsPaused || _songCompleteHandled) return;

        // For non-looping songs, let TimingCoordinator decide when complete
        if (!chart.loopChart && TimingCoordinator.Instance.IsSongComplete())
            HandleSongComplete();
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Begin a game session. Safe to call from a UI button.
    /// Resets all state before starting.
    /// </summary>
    public void StartGame()
    {
        if (!ValidateSetup()) return;

        // Calculate start time internally — use this path when running standalone
        // (not driven by GameManager / ArcadeModeController).
        const double LOOKAHEAD = 0.05;
        double realDspStart = AudioSettings.dspTime + LOOKAHEAD;
        double virtualStart = GameClock.Instance.RealDspToVirtual(realDspStart);

        StartGameWithTiming(virtualStart, realDspStart);
    }

    /// <summary>
    /// Begin a game session using externally-resolved start times.
    /// Called by ArcadeModeController so all modes share the same synchronized
    /// clock anchor calculated by GameManager.
    /// </summary>
    /// <param name="virtualStartTime">GameClock virtual time of the first beat.</param>
    /// <param name="realDspStartTime">Real DSP time for AudioSource.PlayScheduled().</param>
    public void StartGameWithTiming(double virtualStartTime, double realDspStartTime)
    {
        if (!ValidateSetup()) return;

        IsRunning = false;
        _songCompleteHandled = false;

        // Clear any leftover queued notes from a previous run
        _laneManager.ClearQueues();

        // Beat 0 occurs startDelaySeconds after the base virtual time.
        // The spawner must use this delayed anchor so notes scroll in and
        // arrive at the hit zone exactly when the audio plays — not before.
        double beatZeroVirtualTime = virtualStartTime + startDelaySeconds;
        double beatZeroRealDsp = realDspStartTime + startDelaySeconds;

        // Schedule audio at the delayed real DSP time
        if (audioSource != null && chart.audioClip != null)
        {
            audioSource.clip = chart.audioClip;
            audioSource.PlayScheduled(beatZeroRealDsp);
        }

        // Scale HitJudge windows to BPM — TimingCoordinator already initialised by GameManager
        _hitJudge.SetWindowsFromBpm(chart.bpm);

        // Give the spawner the beat-0 anchor — notes will pre-scroll during
        // the startDelaySeconds window before audio begins.
        _noteSpawner.StartSpawning(beatZeroVirtualTime);

        IsRunning = true;

        Debug.Log($"[RhythmGameController] Started — " +
                  $"BaseVirtual: {virtualStartTime:F4} | " +
                  $"BeatZeroVirtual: {beatZeroVirtualTime:F4} | " +
                  $"BeatZeroRealDSP: {beatZeroRealDsp:F4} | " +
                  $"Delay: {startDelaySeconds}s");
    }

    /// <summary>Pause game and audio.</summary>
    public void PauseGame()
    {
        if (!IsRunning || IsPaused) return;

        GameClock.Instance.Pause();
        audioSource?.Pause();
        OnPaused?.Invoke();

        Debug.Log("[RhythmGameController] Paused");
    }

    /// <summary>Resume game and audio.</summary>
    public void ResumeGame()
    {
        if (!IsRunning || !IsPaused) return;

        GameClock.Instance.Resume();
        audioSource?.UnPause();
        OnResumed?.Invoke();

        Debug.Log("[RhythmGameController] Resumed");
    }

    /// <summary>Stop and reset everything — returns to pre-game state.</summary>
    public void StopGame()
    {
        if (!IsRunning) return;

        IsRunning = false;
        audioSource?.Stop();
        GameClock.Instance.Reset();
        TimingCoordinator.Instance.Reset();
        _noteSpawner.ResetSpawner();
        _laneManager.ClearQueues();

        Debug.Log("[RhythmGameController] Stopped");
    }

    // ── Private ───────────────────────────────────────────────────────────

    private void HandleSongComplete()
    {
        _songCompleteHandled = true;
        IsRunning = false;

        Debug.Log("[RhythmGameController] Song complete");
        OnSongComplete?.Invoke();
    }

    private bool ValidateSetup()
    {
        if (chart == null)
        {
            Debug.LogError("[RhythmGameController] No SongChart assigned.", this);
            return false;
        }
        if (_noteSpawner == null)
        {
            Debug.LogError("[RhythmGameController] NoteSpawner not found on this GameObject.", this);
            return false;
        }
        if (_laneManager == null)
        {
            Debug.LogError("[RhythmGameController] RhythmLaneManager not found on this GameObject.", this);
            return false;
        }
        if (_hitJudge == null)
        {
            Debug.LogError("[RhythmGameController] HitJudge not found on this GameObject.", this);
            return false;
        }
        if (TimingCoordinator.Instance == null)
        {
            Debug.LogError("[RhythmGameController] No TimingCoordinator in scene.", this);
            return false;
        }
        return true;
    }
}
