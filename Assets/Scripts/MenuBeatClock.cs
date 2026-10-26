using System;
using UnityEngine;

namespace KrumbleHut.Drummi.FlashCards
{
    /// <summary>
    /// Beat source for the Home globe menu. Plays the looping menu music on its
    /// own AudioSource (scheduled on the DSP clock, so the beat grid is sample
    /// accurate) and exposes the current beat index for MenuBeatBopper and any
    /// other menu decor that wants to move to the music.
    ///
    /// Why not the gameplay Metronome / BounceOnBeat: those only run while a
    /// GameManager session is live (Metronome.enabled is the "session active"
    /// flag and CleanupSharedSystems disables it), and the menu must bop
    /// while no run is in progress. This clock is independent of GameClock,
    /// GameManager and AudioManager: the gameplay music still goes through
    /// AudioManager.speakers[0] and is untouched.
    ///
    /// LIFECYCLE: put the component on a menu-only object (listed in
    /// FlashCardsMenuController.menuObjects). OnEnable schedules the music,
    /// OnDisable stops it, so the menu→gameplay swap and scene unloads start
    /// and silence it automatically. The beat grid is re-anchored every loop
    /// (position = elapsed mod clip length) so a clip whose length is not a
    /// whole number of beats never drifts.
    ///
    /// INSPECTOR: music clip + its bpm (file names carry the tempo, e.g.
    /// Boppy_110), firstBeatOffset if beat 1 is not at sample 0.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class MenuBeatClock : MonoBehaviour
    {
        /// <summary>The active clock (set while enabled). Null when the menu is hidden.</summary>
        public static MenuBeatClock Instance { get; private set; }

        [Header("Music")]
        [Tooltip("Looping menu track. Plays on the AudioSource below from the DSP clock.")]
        [SerializeField] private AudioClip music;

        [Tooltip("Tempo of the clip in beats per minute.")]
        [SerializeField, Min(1f)] private double bpm = 110.0;

        [Tooltip("Seconds into the clip where beat 1 lands (0 when the track starts on the downbeat).")]
        [SerializeField, Min(0f)] private float firstBeatOffset = 0f;

        [Tooltip("Source the music plays on. Defaults to an AudioSource on this object.")]
        [SerializeField] private AudioSource source;

        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

        [Tooltip("Seconds of DSP lead time before the first sample so the scheduled start is never in the past.")]
        [SerializeField, Min(0.02f)] private float scheduleLead = 0.1f;

        /// <summary>Raised once per beat (index is the beat number since the clip start, loop-aware).</summary>
        public event Action<long> Beat;

        /// <summary>Seconds per beat.</summary>
        public double BeatInterval => 60.0 / bpm;

        /// <summary>True once the music has been scheduled and its first beat has passed.</summary>
        public bool IsRunning => _running && AudioSettings.dspTime >= _startDsp + firstBeatOffset;

        /// <summary>
        /// Beat number since the clip's first beat; −1 before the music starts.
        /// Continues counting across loops (loop k adds k · beatsPerLoop) so
        /// listeners that bop every N beats keep a stable phase.
        /// </summary>
        public long BeatIndex
        {
            get
            {
                if (!_running) return -1;
                double pos = ClipPosition();
                if (pos < 0.0) return -1;
                return _loopsCompleted * _beatsPerLoop + (long)Math.Floor(pos / BeatInterval);
            }
        }

        /// <summary>0..1 progress through the current beat (0 on the beat).</summary>
        public float BeatPhase
        {
            get
            {
                if (!_running) return 0f;
                double pos = ClipPosition();
                if (pos < 0.0) return 0f;
                double beats = pos / BeatInterval;
                return (float)(beats - Math.Floor(beats));
            }
        }

        private bool   _running;
        private double _startDsp;
        private double _clipLength;
        private long   _beatsPerLoop;
        private long   _loopsCompleted;
        private long   _lastBeatIndex = -1;

        private void Awake()
        {
            if (source == null) source = GetComponent<AudioSource>();
        }

        private void OnEnable()
        {
            Instance = this;
            StartMusic();
        }

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
            StopMusic();
        }

        private void Update()
        {
            if (!_running) return;

            // Track loops from the DSP clock rather than AudioSource.time
            // (which is quantised to audio buffers and jitters).
            double elapsed = AudioSettings.dspTime - _startDsp;
            if (elapsed >= 0.0 && _clipLength > 0.0)
                _loopsCompleted = (long)Math.Floor(elapsed / _clipLength);

            long index = BeatIndex;
            if (index > _lastBeatIndex)
            {
                _lastBeatIndex = index;
                Beat?.Invoke(index);
            }
        }

        /// <summary>Seconds since the clip's first beat within the current loop (negative before the first beat).</summary>
        private double ClipPosition()
        {
            double elapsed = AudioSettings.dspTime - _startDsp;
            if (elapsed < 0.0) return elapsed - firstBeatOffset;
            double inLoop = _clipLength > 0.0 ? elapsed - _loopsCompleted * _clipLength : elapsed;
            return inLoop - firstBeatOffset;
        }

        private void StartMusic()
        {
            if (music == null || source == null || bpm <= 0.0)
            {
                if (music == null) Debug.Log("[MenuBeatClock] No menu music assigned — beat clock idle.");
                return;
            }

            source.clip        = music;
            source.loop        = true;
            source.playOnAwake = false;
            source.volume      = volume;

            _clipLength     = music.length;
            _beatsPerLoop   = Math.Max(1, (long)Math.Round(_clipLength / BeatInterval));
            _loopsCompleted = 0;
            _lastBeatIndex  = -1;
            _startDsp       = AudioSettings.dspTime + scheduleLead;

            source.Stop();
            source.PlayScheduled(_startDsp);
            _running = true;
        }

        /// <summary>
        /// Fade the music to silence over <paramref name="seconds"/> (real time)
        /// while leaving the menu — the beat grid keeps running so decor bops
        /// through the cover. The next StartMusic (menu shown again) restores
        /// the volume.
        /// </summary>
        public void FadeOut(float seconds)
        {
            if (!_running || source == null) return;
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeOutRoutine(Mathf.Max(0f, seconds)));
        }

        private System.Collections.IEnumerator FadeOutRoutine(float seconds)
        {
            float startVolume = source.volume;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(startVolume, 0f, elapsed / seconds);
                yield return null;
            }
            source.volume = 0f;
            _fadeRoutine = null;
        }

        private Coroutine _fadeRoutine;

        private void StopMusic()
        {
            _running = false;
            if (_fadeRoutine != null) { StopCoroutine(_fadeRoutine); _fadeRoutine = null; }
            if (source != null && source.isPlaying) source.Stop();
        }
    }
}
