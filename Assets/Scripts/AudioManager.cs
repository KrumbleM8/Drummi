using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;
    public int selectedSongIndex;
    // 0-Music, 1-Bongos, 2-Misc
    public AudioSource[] speakers;

    public AudioClip[] musicTracks;
    public AudioClip[] bongoSounds;
    public AudioClip[] otherSounds;

    [Header("Game Over")]
    [SerializeField] private int gameOverSoundIndex = 6;

    [Header("Drum Machine")]
    [Tooltip("Clips in DrumSoundType order: Kick, Snare, HiHat, Clap.")]
    public AudioClip[] drumMachineSounds;

    // The DSP time at which the music should start playing.
    // This is usually set from elsewhere (e.g., by the rhythm system).
    public double scheduledStartTime;

    // Fired when all audio is paused/resumed — StemPlayer subscribes to stay in sync
    // without being added to speakers[].
    public static event Action OnAudioPaused;
    public static event Action OnAudioResumed;

    // Pause tracking variables.
    private bool isPaused = false;
    private double pauseStartDspTime = 0.0;
    // Total pause duration that we add to scheduled times if needed.
    private double accumulatedPauseDuration = 0.0;

    // Active music fade (dungeon floor transitions). Cancelled whenever a new
    // track is started/scheduled so the volume is never left mid-fade.
    private Coroutine musicFadeRoutine;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this); // was Destroy(gameObject) � only destroy the duplicate component
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void PlayDrumSound(DrumSoundType sound)
    {
        int index = (int)sound;
        if (drumMachineSounds == null || index < 0 || index >= drumMachineSounds.Length) return;
        if (drumMachineSounds[index] == null) return;
        speakers[1].PlayOneShot(drumMachineSounds[index]);
    }

    // Play bongo sounds immediately using PlayOneShot.
    public void PlayBongoLeft()
    {
        if (bongoSounds == null || bongoSounds.Length < 1 || bongoSounds[0] == null) return;
        speakers[1].PlayOneShot(bongoSounds[0]);
    }

    public void PlayBongoRight()
    {
        if (bongoSounds == null || bongoSounds.Length < 2 || bongoSounds[1] == null) return;
        speakers[1].PlayOneShot(bongoSounds[1]);
    }

    public void PlayPadLeft()
    {
        if (bongoSounds == null || bongoSounds.Length < 3 || bongoSounds[2] == null) return;
        speakers[1].PlayOneShot(bongoSounds[2]);
    }

    public void PlayPadRight()
    {
        if (bongoSounds == null || bongoSounds.Length < 4 || bongoSounds[3] == null) return;
        speakers[1].PlayOneShot(bongoSounds[3]);
    }

    public void PlayPadCenter()
    {
        if (bongoSounds == null || bongoSounds.Length < 5 || bongoSounds[4] == null) return;
        speakers[1].PlayOneShot(bongoSounds[4]);
    }

    // Schedule the music to start playing at the (adjusted) scheduled time.
    // loop=true is used by dungeon floor BGM, which relies on Unity's sample-accurate
    // looping with bar-exact clips; song modes (Bongo) keep the default one-shot behavior.
    public void PlayMusic(bool loop = false)
    {
        RestoreMusicVolume();
        speakers[0].clip = musicTracks[selectedSongIndex];
        speakers[0].loop = loop;
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
        if (otherSounds == null || otherSounds.Length < 1 || otherSounds[0] == null) return;
        speakers[2].PlayOneShot(otherSounds[0]);
    }

    public void PlayPassable()
    {
        if (otherSounds == null || otherSounds.Length < 5 || otherSounds[4] == null) return;
        speakers[2].PlayOneShot(otherSounds[4]);
    }

    public void PlayCorrect()
    {
        if (otherSounds == null || otherSounds.Length < 2 || otherSounds[1] == null) return;
        speakers[2].PlayOneShot(otherSounds[1]);
    }

    public void PlayAllPerfect()
    {
        if (otherSounds == null || otherSounds.Length < 4 || otherSounds[3] == null) return;
        speakers[2].PlayOneShot(otherSounds[3]);
    }

    public void PlayTotalFail()
    {
        if (otherSounds == null || otherSounds.Length < 3 || otherSounds[2] == null) return;
        speakers[2].PlayOneShot(otherSounds[2]);
    }
    //public void PlayTurnSignal(double time)
    //{
    //    speakers[3].PlayScheduled(time);
    //}

    /// <summary>Plays the game-over audio sting via otherSounds[gameOverSoundIndex].</summary>
    public void PlayGameOver()
    {
        if (otherSounds == null || gameOverSoundIndex < 0 || gameOverSoundIndex >= otherSounds.Length) return;
        if (otherSounds[gameOverSoundIndex] == null) return;
        speakers[2].PlayOneShot(otherSounds[gameOverSoundIndex]);
    }

    public void PlayTurnSignal(double scheduledDspTime)
    {
        if (otherSounds == null || otherSounds.Length < 6 || otherSounds[5] == null) return;
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
            OnAudioPaused?.Invoke();
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
            OnAudioResumed?.Invoke();
        }
    }

    public void StopMusic()
    {
        speakers[0].Stop();
    }

    /// <summary>
    /// Stops the current music source and schedules a new track to begin at
    /// <paramref name="realDspSeamTime"/>. Call this for special rooms that
    /// override the BGM mid-run.
    /// <paramref name="realDspSeamTime"/> must be a fully pause-corrected real DSP
    /// timestamp (i.e. from GameClock.VirtualToRealDsp) — no pause offset is added
    /// here, or the swap would land late by the total pause time and desync the
    /// music from the beat grid.
    /// </summary>
    public void ScheduleTrackSwap(int trackIndex, double realDspSeamTime)
    {
        RestoreMusicVolume();
        speakers[0].Stop();
        selectedSongIndex = trackIndex;
        speakers[0].clip = musicTracks[trackIndex];
        // Dungeon-only path — floor/override BGM loops until the next seam swap.
        speakers[0].loop = true;
        speakers[0].PlayScheduled(realDspSeamTime);
        Debug.Log($"[AudioManager] Track swap — index {trackIndex} scheduled at DSP {realDspSeamTime:F4}");
    }

    /// <summary>
    /// Fades the music source (speakers[0]) to silence over <paramref name="seconds"/>
    /// of unscaled real time. Used by the dungeon floor transition while the title
    /// card is up. Volume is restored automatically the next time a track starts
    /// (PlayMusic / ScheduleTrackSwap / ResetState).
    ///
    /// STEM-REDUCTION HOOK: when stem-authored floor tracks and a stem mixer exist,
    /// replace this whole-track volume fade with a filter down to the bass stem.
    /// Call site: DungeonRunner.FloorTransitionRoutine().
    /// </summary>
    public void FadeOutMusic(float seconds)
    {
        if (musicFadeRoutine != null) StopCoroutine(musicFadeRoutine);
        musicFadeRoutine = StartCoroutine(FadeOutMusicRoutine(seconds));
    }

    private System.Collections.IEnumerator FadeOutMusicRoutine(float seconds)
    {
        AudioSource music = speakers[0];
        float startVolume = music.volume;
        float elapsed = 0f;

        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            music.volume = Mathf.Lerp(startVolume, 0f, elapsed / seconds);
            yield return null;
        }

        music.volume = 0f;
        musicFadeRoutine = null;
    }

    /// <summary>Cancels any running music fade and restores full source volume.</summary>
    private void RestoreMusicVolume()
    {
        if (musicFadeRoutine != null)
        {
            StopCoroutine(musicFadeRoutine);
            musicFadeRoutine = null;
        }
        speakers[0].volume = 1f;
    }

    public void ResetState()
    {
        // Stop every speaker cleanly before the new session starts
        foreach (AudioSource src in speakers)
        {
            if (src != null) src.Stop();
        }
        RestoreMusicVolume();

        // Clear all pause tracking so the new session starts from zero
        isPaused = false;
        pauseStartDspTime = 0.0;
        accumulatedPauseDuration = 0.0;
        scheduledStartTime = 0.0;

        Debug.Log("[AudioManager] State reset for new session");
    }
}
