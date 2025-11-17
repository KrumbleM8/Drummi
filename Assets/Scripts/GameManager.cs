using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("Core Gameplay")]
    public Metronome metronome;
    public BeatGenerator beatGenerator;
    public BeatEvaluator beatEvaluator;
    public BeatVisualScheduler visualScheduler;
    public PlayerInputVisualHandler playerInputVisualHandler;

    [Header("Gameplay Root")]
    public GameObject gameplayElementsObject;

    [Header("Misc")]
    public EyeBlinker blinking;

    [Header("Delegates")]
    [SerializeField] private PauseHandler pauseHandler;
    [SerializeField] private SceneLoadManager sceneLoader;
    [SerializeField] private ScreenTransition screenTransition;
    [SerializeField] private GameObject pageToCloseOnStart;
    [SerializeField] private GameObject gameplayElements;
    [SerializeField] private BeatVisualScheduler beatVisualScheduler;
    [SerializeField] private PlayerInputVisualHandler playerInputVisual;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        instance = this;
    }

    private void Start()
    {
        Time.timeScale = 1f;

        pauseHandler = GetComponent<PauseHandler>();
        sceneLoader = SceneLoadManager.instance;

        beatGenerator.OnSongComplete += HandleSongComplete;
    }

    private void HandleSongComplete()
    {
        // Your transition logic here
        Debug.Log("Song complete! Show results screen");

        // Example options:
        // 1. Show results panel
        // resultsPanel.SetActive(true);
        // resultsPanel.DisplayScore(beatEvaluator.score);

        // 2. Or trigger animation
        // transitionAnimator.SetTrigger("FadeToResults");

        // 3. Or simply enable a results UI object
        // resultsScreen.gameObject.SetActive(true);
        // resultsScreen.Initialize(beatEvaluator.score, beatEvaluator.perfectHits);
    }

    public void StartGame()
    {
        StartCoroutine(StartProcess());
        if (blinking != null)
        {
            blinking.enabled = false;
            if (blinking.transform.childCount > 0)
                blinking.transform.GetChild(0).gameObject.SetActive(false);
        }
    }

    private IEnumerator StartProcess()
    {
        screenTransition.StartCover();

        while (!screenTransition.IsScreenCovered)
        {
            yield return null;
        }

        pageToCloseOnStart.SetActive(false);
        screenTransition.StartReveal();

        Debug.Log("=== STARTING GAME ===");

        bool isReplay = metronome != null && metronome.hasEverStarted;

 

        // Enable non-metronome components
        if (beatGenerator != null)
        {
            beatGenerator.enabled = true;
            beatGenerator.Initialize();
            Debug.Log("BeatGenerator enabled");
        }

        if (beatVisualScheduler != null)
        {
            beatVisualScheduler.enabled = true;
            Debug.Log("BeatVisualScheduler enabled");
        }

        if (playerInputVisual != null)
        {
            playerInputVisual.enabled = true;
            Debug.Log("PlayerInputVisualHandler enabled");
        }

        // === DO NOT ENABLE METRONOME YET ===
        // On first playthrough, we need to enable it to trigger Start()
        // On second playthrough, we keep it disabled until after scheduling

        if (!isReplay && metronome != null)
        {
            // First playthrough: enable metronome so Start() runs
            metronome.enabled = true;
            Debug.Log("Metronome enabled (first playthrough - Start will run)");
        }

        // Initialize BeatGenerator
        if (isReplay && beatGenerator != null)
        {
            beatGenerator.ResetToInitialState();
            Debug.Log("BeatGenerator prepared");
        }

        // Enable UI
        if (gameplayElements != null)
        {
            gameplayElements.SetActive(true);
        }

        if (gameplayElementsObject)
            gameplayElementsObject.SetActive(true);

        // Get next beat time (metronome is disabled on replay, so just read the value)
        double nextBeatTime = 0;
        if (metronome != null)
        {
            nextBeatTime = metronome.nextTick; // Read directly, don't call method
            Debug.Log($"Next beat time: {nextBeatTime:F4} at DSP: {AudioSettings.dspTime:F4}");
        }

        // Prepare metronome timing while DISABLED
        if (isReplay && metronome != null)
        {
            metronome.RefreshTimingForGameStart();
            Debug.Log("Metronome timing refreshed (while DISABLED)");
        }

        // Schedule music
        if (AudioManager.instance != null)
        {
            AudioManager.instance.scheduledStartTime = nextBeatTime;
            AudioManager.instance.PlayMusic();
            Debug.Log($"Music scheduled at: {nextBeatTime:F4}");
        }

        // === NOW enable metronome for second playthrough ===
        if (isReplay && metronome != null)
        {
            metronome.enabled = true;
            Debug.Log($"Metronome enabled AFTER music scheduled");
            Debug.Log($"Beat count: {metronome.beatCount}, Loop beat count: {metronome.loopBeatCount}");
        }

        // Sync visual schedulers
        if (beatVisualScheduler != null)
        {
            beatVisualScheduler.SyncWithMetronome(nextBeatTime);
        }

        if (playerInputVisual != null)
        {
            playerInputVisual.SyncWithMetronome(nextBeatTime);
        }

        beatGenerator.StartGameplay(nextBeatTime);
        yield return null;
    }

    public void SetDifficulty(int difficultyIndex)
    {
        if (beatGenerator) beatGenerator.difficultyIndex = difficultyIndex;
    }

    public void SetMusic(int index)
    {
        if (metronome == null) return;

        switch (index)
        {
            case 0:
                metronome.bpm = 111;
                break;
            case 1:
                metronome.bpm = 111;
                break;
            case 2:
                metronome.bpm = 79;
                break;
            default:
                metronome.bpm = 105;
                break;
        }

        if (AudioManager.instance != null) AudioManager.instance.selectedSongIndex = index;
        BroadcastMessage("SetBPM", SendMessageOptions.DontRequireReceiver);
    }

    // Preserved public API. Logic delegated.
    public void TogglePause()
    {
        if (pauseHandler != null) pauseHandler.TogglePause();
    }

    // Preserved public API. Logic delegated.
    public void ResetDrummi()
    {
        if (sceneLoader != null) sceneLoader.ResetDrummi();
    }
    public void ResetGameValues()
    {
        if (beatGenerator != null)
        {
            beatGenerator.ResetToInitialState();
        }

        if (beatVisualScheduler != null)
        {
            beatVisualScheduler.ResetToInitialState();
        }

        if (playerInputVisual != null)
        {
            playerInputVisual.ResetToInitialState();
        }

        if (metronome != null)
        {
            metronome.ResetToInitialState();
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks
        if (beatGenerator != null)
            beatGenerator.OnSongComplete -= HandleSongComplete;
    }
}
