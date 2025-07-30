using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;
    public int selectedSongIndex;
    // 0-Music, 1-Bongos, 2-Misc
    public AudioSource[] speakers;

    public AudioClip[] musicTracks;
    public AudioClip[] bongoSounds;
    public AudioClip[] otherSounds;

    // The DSP time at which the music should start playing.
    // This is usually set from elsewhere (e.g., by the rhythm system).
    public double scheduledStartTime;

    // Pause tracking variables.
    private bool isPaused = false;
    private double pauseStartDspTime = 0.0;
    // Total pause duration that we add to scheduled times if needed.
    private double accumulatedPauseDuration = 0.0;

    private void Awake()
    {
        instance = this;
    }

    // Play bongo sounds immediately using PlayOneShot.
    public void PlayBongoLeft()
    {
        speakers[1].PlayOneShot(bongoSounds[0]);
    }

    public void PlayBongoRight()
    {
        speakers[1].PlayOneShot(bongoSounds[1]);
    }

    // Schedule the music to start playing at the (adjusted) scheduled time.
    public void PlayMusic()
    {
        speakers[0].clip = musicTracks[selectedSongIndex];
        speakers[0].loop = true;
        // Adjust the scheduled start time by any accumulated pause duration.
        double adjustedStartTime = scheduledStartTime + accumulatedPauseDuration;
        speakers[0].PlayScheduled(adjustedStartTime);
    }

    // Pause the music. (If music is already playing, this stops it while keeping its state.)
    public void PauseMusic()
    {
        speakers[0].Pause();
    }

    // Resume the music using UnPause(), which continues playback from the paused position.
    public void ResumeMusic()
    {
        speakers[0].UnPause();
    }

    public void PlayIncorrect()
    {
        speakers[2].PlayOneShot(otherSounds[0]);
    }

    public void PlayPassable()
    {
        speakers[2].PlayOneShot(otherSounds[4]);
    }
    public void PlayCorrect()
    {
        speakers[2].PlayOneShot(otherSounds[1]);
    }

    public void PlayAllPerfect()
    {
        Debug.Log("AudioManager - Perfect");
        speakers[2].PlayOneShot(otherSounds[3]);
    }
    public void PlayTotalFail()
    {
        speakers[2].PlayOneShot(otherSounds[2]);
    }
    //public void PlayTurnSignal(double time)
    //{
    //    speakers[3].PlayScheduled(time);
    //}

    public void PlayTurnSignal(double scheduledDspTime)
    {
        var go = new GameObject("TurnSignalVoice");
        go.transform.SetParent(transform);
        var src = go.AddComponent<AudioSource>();
        src.clip = otherSounds[5];
        src.playOnAwake = false;
        src.PlayScheduled(scheduledDspTime);

        // destroy when done
        Destroy(go, otherSounds[5].length + 3f);
    }


    // Call this method when you want to pause all audio.
    public void PauseAllAudio()
    {
        if (!isPaused)
        {
            isPaused = true;
            pauseStartDspTime = AudioSettings.dspTime;
            // Pause each speaker.
            foreach (AudioSource src in speakers)
            {
                src.Pause();
            }
        }
    }

    // Call this method when you want to resume all audio.
    public void ResumeAllAudio()
    {
        if (isPaused)
        {
            // Calculate how long the pause lasted.
            double pauseDuration = AudioSettings.dspTime - pauseStartDspTime;
            accumulatedPauseDuration += pauseDuration;
            isPaused = false;
            // Resume each speaker.
            foreach (AudioSource src in speakers)
            {
                src.UnPause();
            }
        }
    }
}
