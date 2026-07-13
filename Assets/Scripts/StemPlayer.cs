using System.Text;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>
    /// Phase-0 stem sync test harness — throwaway proof of concept, not final architecture.
    ///
    /// KEYS (New Input System — works in any scene):
    ///   S — phase-lock all stems and fire PlayScheduled on the same DSP tick.
    ///        baseLayerIndex stem starts audible (vol=1); all others start silent (vol=0).
    ///   R — ramp the next muted stem 0→1 over rampDuration (DOFade, click-free because
    ///        the source has been playing silently in phase with the base since the start).
    ///
    /// PAUSE: subscribes to AudioManager.OnAudioPaused / OnAudioResumed so stems are
    ///        Pause()/UnPause()'d synchronously with speakers[] in the same C# call stack.
    ///        No re-scheduling needed — Pause/UnPause preserves position exactly.
    ///
    /// LOOP SETTING (stemLoop): must match AudioManager.PlayMusic() (currently loop=false).
    ///        If stems are loop clips, set stemLoop=true AND ensure the music track also loops
    ///        — a mismatch causes a permanent desync at the loop boundary.
    ///
    /// DIAGNOSTICS: timeSamples for every stem is logged at start, periodically, pre/post-pause,
    ///              and pre-ramp. Identical values = sample-locked. One sample of jitter is
    ///              normal (audio thread vs. game thread boundary); any growing drift is not.
    /// </summary>
    public class StemPlayer : MonoBehaviour
    {
        [Header("Stems")]
        [Tooltip("Sample-aligned clips — identical sample count, identical sample rate, all starting at sample 0.")]
        [SerializeField] private AudioClip[] testStems;
        [Tooltip("Index of the always-audible base layer (vol=1 from the start).")]
        [SerializeField] private int baseLayerIndex = 0;

        [Header("Loop")]
        [Tooltip("Must match AudioManager.PlayMusic() loop setting (currently false). " +
                 "Mismatch between stems and base track causes permanent desync at the loop boundary.")]
        [SerializeField] private bool stemLoop = false;

        [Header("Ramp")]
        [Tooltip("Volume ramp duration in seconds when pressing R to blend in a stem.")]
        [SerializeField] private float rampDuration = 1f;

        [Header("Timing")]
        [Tooltip("Seconds added to AudioSettings.dspTime for the shared PlayScheduled call. " +
                 "Must be long enough for Unity's audio thread to receive the schedule before the tick fires.")]
        [SerializeField] private float lookahead = 0.1f;

        [Header("Diagnostics")]
        [Tooltip("Unscaled seconds between periodic alignment log entries.")]
        [SerializeField] private float logIntervalSeconds = 10f;

        // ── Private state ──────────────────────────────────────────────────────
        private AudioSource[] _stemSources;  // one per testStems[i], never in speakers[]
        private bool[]        _stemRamped;   // true once a stem has been scheduled to ramp in
        private double        _dspStart;     // the exact DSP tick passed to every PlayScheduled
        private bool          _isPlaying;
        private float         _logTimer;

        // ── Unity lifecycle ────────────────────────────────────────────────────
        private void Awake()
        {
            CreateStemSources();
        }

        private void OnEnable()
        {
            AudioManager.OnAudioPaused  += HandlePaused;
            AudioManager.OnAudioResumed += HandleResumed;
        }

        private void OnDisable()
        {
            AudioManager.OnAudioPaused  -= HandlePaused;
            AudioManager.OnAudioResumed -= HandleResumed;
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            // S — start (only once per scene load)
            if (!_isPlaying && Keyboard.current.sKey.wasPressedThisFrame)
                StartStems();

            // R — ramp in next muted stem
            if (_isPlaying && Keyboard.current.rKey.wasPressedThisFrame)
                RampInNext();

            // Periodic alignment log (unscaled so it fires even if timeScale != 1)
            if (_isPlaying)
            {
                _logTimer -= Time.unscaledDeltaTime;
                if (_logTimer <= 0f)
                {
                    _logTimer = logIntervalSeconds;
                    LogAlignment("PERIODIC");
                }
            }
        }

        // ── Private — setup ────────────────────────────────────────────────────
        private void CreateStemSources()
        {
            if (testStems == null || testStems.Length == 0)
            {
                Debug.LogWarning("[StemPlayer] testStems is empty — assign sample-aligned clips in the Inspector.");
                return;
            }

            _stemSources = new AudioSource[testStems.Length];
            _stemRamped  = new bool[testStems.Length];

            for (int i = 0; i < testStems.Length; i++)
            {
                var src         = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop        = stemLoop;
                src.clip        = testStems[i];
                src.volume      = (i == baseLayerIndex) ? 1f : 0f;
                _stemSources[i] = src;
            }

            // Base layer counts as already ramped
            if (baseLayerIndex >= 0 && baseLayerIndex < _stemRamped.Length)
                _stemRamped[baseLayerIndex] = true;

            Debug.Log($"[StemPlayer] Created {_stemSources.Length} stem AudioSources " +
                      $"(base={baseLayerIndex}, loop={stemLoop}). Press S to start.");
        }

        // ── Private — playback ─────────────────────────────────────────────────
        private void StartStems()
        {
            if (_stemSources == null || _stemSources.Length == 0)
            {
                Debug.LogError("[StemPlayer] No stem sources ready — assign testStems in Inspector.");
                return;
            }

            _dspStart = AudioSettings.dspTime + lookahead;

            for (int i = 0; i < _stemSources.Length; i++)
                _stemSources[i].PlayScheduled(_dspStart);

            _isPlaying = true;
            _logTimer  = logIntervalSeconds;

            Debug.Log($"[StemPlayer] START — {_stemSources.Length} stems PlayScheduled." +
                      $"  dspStart={_dspStart:F6}  lookahead={lookahead:F3}s  loop={stemLoop}");

            // Read timeSamples after the audio thread has actually started the clip.
            Invoke(nameof(LogAfterStart), (float)lookahead + 0.05f);
        }

        // Called ~150 ms after PlayScheduled fires so timeSamples are non-zero
        private void LogAfterStart() => LogAlignment("AFTER-START");

        private void RampInNext()
        {
            int idx = FindNextUnramped();
            if (idx < 0)
            {
                Debug.Log("[StemPlayer] All stems are already ramped in.");
                return;
            }

            _stemRamped[idx] = true;

            Debug.Log($"[StemPlayer] RAMP-IN stem[{idx}] '{testStems[idx]?.name}'" +
                      $" → vol 1 over {rampDuration:F2}s");
            LogAlignment("PRE-RAMP");

            _stemSources[idx].DOFade(1f, rampDuration);
        }

        private int FindNextUnramped()
        {
            for (int i = 0; i < _stemSources.Length; i++)
                if (!_stemRamped[i]) return i;
            return -1;
        }

        // ── Private — pause coordination ───────────────────────────────────────
        private void HandlePaused()
        {
            if (!_isPlaying) return;
            LogAlignment("PRE-PAUSE");
            foreach (var src in _stemSources)
                if (src != null) src.Pause();
            Debug.Log("[StemPlayer] Stems PAUSED (Pause() called synchronously with speakers[]).");
        }

        private void HandleResumed()
        {
            if (!_isPlaying) return;
            foreach (var src in _stemSources)
                if (src != null) src.UnPause();
            // Small delay to let the audio thread resume before sampling timeSamples
            Invoke(nameof(LogAfterResume), 0.05f);
        }

        private void LogAfterResume() => LogAlignment("POST-RESUME");

        // ── Private — diagnostics ──────────────────────────────────────────────
        private void LogAlignment(string label)
        {
            if (_stemSources == null) return;
            var sb = new StringBuilder();
            sb.Append($"[StemPlayer:{label}] dsp={AudioSettings.dspTime:F4} | ");
            for (int i = 0; i < _stemSources.Length; i++)
            {
                var src = _stemSources[i];
                if (src == null) continue;
                string name = testStems[i] != null ? testStems[i].name : "null";
                sb.Append($"[{i}]'{name}' samples={src.timeSamples} vol={src.volume:F2}  ");
            }
            Debug.Log(sb.ToString());
        }
    }
}
