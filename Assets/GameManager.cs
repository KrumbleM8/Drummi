using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    public Metronome metronome;
    public BeatGenerator beatGenerator;
    public BeatEvaluator beatEvaluator;
    public BeatVisualScheduler visualScheduler;
    public PlayerInputVisualHandler playerInputVisualHandler;

    public GameObject gameplayElementsObject;

    //Refs to Disable
    public EyeBlinker blinking;

    public double totalPausedTime = 0.0;
    public double pauseStartTime = 0.0;
    public bool isPaused = false;

    public double VirtualDspTime() => AudioSettings.dspTime - totalPausedTime;
    private void Start()
    {
        instance = this;
        Time.timeScale = 1;
    }
    public void StartGame()
    {
        StartCoroutine(StartProcess());
        blinking.enabled = false;
        blinking.transform.GetChild(0).gameObject.SetActive(false);
    }

    IEnumerator StartProcess()
    {
        yield return new WaitForEndOfFrame();
        beatGenerator.enabled = true;
        visualScheduler.enabled = true;
        playerInputVisualHandler.enabled = true;
        metronome.enabled = true;

        gameplayElementsObject.SetActive(true);

        AudioManager.instance.scheduledStartTime = metronome.GetNextBeatTime();
        AudioManager.instance.PlayMusic();

        yield return null;
    }

    public void SetDifficulty(int difficultyIndex)
    {
        beatGenerator.difficultyIndex = difficultyIndex;
        beatGenerator.SetBPM();
    }

    public void SetMusic(int index)
    {
        switch (index)
        {
            case 0:
                metronome.bpm = 105;
                AudioManager.instance.selectedSongIndex = index;
                BroadcastMessage("SetBPM");
                break;
            case 1:
                //metronome.bpm = SongItem.bpm etc
                metronome.bpm = 111;
                AudioManager.instance.selectedSongIndex = index;
                BroadcastMessage("SetBPM");
                break;
            case 2:
                break;
            default:
                metronome.bpm = 105;
                BroadcastMessage("SetBPM");
                AudioManager.instance.selectedSongIndex = index;
                break;

        }
    }

    public void TogglePause()
    {
        beatEvaluator.SaveHighScore();
        if (Time.timeScale > 0)
        {
            Time.timeScale = 0;
            pauseStartTime = AudioSettings.dspTime;
            AudioManager.instance.PauseAllAudio();

            isPaused = true;
            if (metronome.enabled) metronome.OnPause();
            if (visualScheduler.enabled) visualScheduler.OnPause();
        }
        else
        {
            Time.timeScale = 1;
            double pauseDuration = AudioSettings.dspTime - pauseStartTime;
            totalPausedTime += pauseDuration;
            AudioManager.instance.ResumeAllAudio();

            isPaused = false;
            if (metronome.enabled) metronome.OnResume();
            if (visualScheduler.enabled) visualScheduler.OnResume();
        }
    }

    public void ResetDrummi() //Maybe add a confirm check window so people dont accidentally lose all their progress
    {
        //Handle saving playerprefs, etc

        //Transition Screen here

        //PUT THIS IN A COROUTINE OR INVOKE TO ADD DELAY FOR TRANSITION TO FINISH
        SceneManager.LoadScene(0); //Does not go back to bootstrap, this may be an issue later
    }
}
