using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public Metronome metronome;
    public BeatGenerator beatGenerator;
    public BeatEvaluator beatEvaluator;
    public BeatVisualScheduler visualScheduler;
    public PlayerInputVisualHandler playerInputVisualHandler;

    public GameObject gameplayElementsObject;

    private void Start()
    {
        Time.timeScale = 1;
    }
    public void StartGame()
    {
        StartCoroutine(StartProcess());
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
            AudioManager.instance.PauseAllAudio();

            if (metronome.enabled) metronome.OnPause();
            if (beatGenerator.enabled) beatGenerator.OnPause();
            if (playerInputVisualHandler.enabled) playerInputVisualHandler.OnPause();
            if (visualScheduler.enabled) visualScheduler.OnPause();
        }
        else
        {
            Time.timeScale = 1;
            AudioManager.instance.ResumeAllAudio();

            if (metronome.enabled) metronome.OnResume();
            if (beatGenerator.enabled) beatGenerator.OnResume();
            if (playerInputVisualHandler.enabled) playerInputVisualHandler.OnResume();
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
