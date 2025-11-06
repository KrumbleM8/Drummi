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


        // 3. Re-enable all scripts
        if (beatGenerator != null)
        {
            beatGenerator.enabled = true;
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

        if (metronome != null)
        {
            metronome.enabled = true;
            Debug.Log("Metronome enabled");
        }

        // 4. Re-enable gameplay UI
        if (gameplayElements != null)
        {
            gameplayElements.SetActive(true);
            Debug.Log("GameplayElements enabled");
        }

        //// 7. Start pattern generation      //THIS IS BREAKING THE GAME BUT WHY!?!?!?
        //if (beatGenerator != null)
        //{
        //    beatGenerator.StartGame();
        //}

        if (beatGenerator) beatGenerator.enabled = true;
        if (visualScheduler) visualScheduler.enabled = true;
        if (playerInputVisualHandler) playerInputVisualHandler.enabled = true;
        if (metronome) metronome.enabled = true;

        if (gameplayElementsObject) gameplayElementsObject.SetActive(true);

        if (AudioManager.instance != null && metronome != null)
        {
            AudioManager.instance.scheduledStartTime = metronome.GetNextBeatTime();
            AudioManager.instance.PlayMusic();
        }

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
                metronome.bpm = 150;
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
