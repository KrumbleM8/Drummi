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
        yield return new WaitForEndOfFrame();

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
                metronome.bpm = 105;
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
}
