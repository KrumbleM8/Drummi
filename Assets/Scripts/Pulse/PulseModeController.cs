using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace KrumbleHut.Drummi.Pulse
{
    /// <summary>
    /// "Keep a steady pulse" mode. The player taps one centre pad on every
    /// quarter-note of a fixed beat grid; each tap plays a shaker clip that is
    /// exactly one beat of four semiquavers, so steady tapping fills the bar.
    /// There is no count-in: the base layer starts on the grid origin and the
    /// player finds the pulse by ear.
    ///
    /// Stages: a streak of judged taps climbs through <see cref="stages"/>, each
    /// of which sets a volume per looping stem (layers can also drop out, which
    /// is how the pulse is hidden later). Mistakes (missed beat or stray tap):
    ///   • one mistake only loses the progress toward the next stage;
    ///   • 2+ mistakes in one bar (4 beats) drop one stage;
    ///   • 2+ mistakes in each of two consecutive bars revert to stage 0.
    /// Endless — there is no game over; the player leaves via Home.
    ///
    /// Timing: the grid is one integer beat index from the GameManager's shared
    /// virtual start, so judging and missed-beat detection cannot drift apart.
    /// Stems are StemPlayer-style: one looping AudioSource each, all
    /// PlayScheduled on the same DSP tick, paused/unpaused by this controller
    /// (AudioManager.PauseAllAudio only covers its own speakers).
    /// </summary>
    public class PulseModeController : ModeController
    {
        [System.Serializable]
        public class PulseStage
        {
            [Tooltip("Cumulative good-tap streak needed to enter this stage (ascending; stage 0 = 0). " +
                     "After a drop the streak is floored at the new stage's value, so the gap to the next " +
                     "entry is what must be re-earned.")]
            public int streak;
            [Tooltip("Target volume per stem (same order as stems). Lowering a layer is allowed.")]
            public float[] layerVolumes;
        }

        [Header("Systems")]
        [SerializeField] private DrumPadTouch drumPad;

        [Header("Audio")]
        [Tooltip("One beat of four semiquaver shakes at the scene's BPM. Played on every tap.")]
        [SerializeField] private AudioClip shakerClip;
        [Tooltip("Equal-length loop stems, all started on the shared DSP tick.")]
        [SerializeField] private AudioClip[] stems;
        [SerializeField] private PulseStage[] stages;
        [SerializeField] private float layerFadeSeconds = 0.5f;

        [Header("Rules")]
        [SerializeField] private double perfectFraction = 0.08;
        [SerializeField] private double goodFraction = 0.25;

        [Header("HUD")]
        [SerializeField] private TMP_Text streakText;
        [SerializeField] private TMP_Text stageText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private RectTransform shakerVisual;

        private const string HsKey = "PulseHS";
        private const int PerfectReward = 200, GoodReward = 100;
        private const int BeatsPerBar = GameConstants.BEATS_PER_BAR;
        private const float DriftLogSeconds = 60f;

        private AudioSource[] _stemSources;
        private bool   _running;
        private double _origin, _interval;
        private long   _nextBeat;          // first beat index not yet judged or missed
        private int    _score, _perfectHits, _streak, _stage = -1, _startHighScore;
        private long   _mistakeBar = -1;   // bar of the most recent mistake
        private int    _barMistakes, _prevBarMistakes;
        private float  _driftLogTimer;

        public override string ModeId => "Pulse";
        public override int CalculateTotalBeats(int bpm) => 100_000;
        public override int BarsBeforeEndForFinalBar => 1;
        public override int Score => _score;
        public override int TotalPerfectHits => _perfectHits;
        public override bool IsNewHighScore => _score > _startHighScore;
        public override string HighScoreKey => HsKey;

        private double PerfectWindow => _interval * perfectFraction;
        private double GoodWindow    => _interval * goodFraction;
        private double BeatTime(long k) => _origin + k * _interval;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _stemSources = new AudioSource[stems?.Length ?? 0];
            for (int i = 0; i < _stemSources.Length; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop        = true;
                src.clip        = stems[i];
                src.volume      = 0f;
                _stemSources[i] = src;
            }
        }

        private IEnumerator Start()
        {
            // The scene has no menu: auto-start once GameManager (order -30) has registered us.
            // Requires the scene's ScreenTransition.autoRevealOnStart = false, or the
            // cover wait in GameManager.StartGameSequence races the auto-reveal.
            yield return null;
            GameManager.instance.StartGame();
        }

        private void Update()
        {
            if (!_running || GameClock.Instance == null || GameClock.Instance.IsPaused) return;

            double now = GameClock.Instance.GameTime;
            while (now > BeatTime(_nextBeat) + GoodWindow)
            {
                Debug.Log($"[Pulse] beat {_nextBeat} MISSED (no tap)");
                RegisterMistake(_nextBeat);
                _nextBeat++;
            }

            _driftLogTimer -= Time.unscaledDeltaTime;
            if (_driftLogTimer <= 0f) { _driftLogTimer = DriftLogSeconds; LogStemAlignment("PERIODIC"); }
        }

        // ── ModeController ────────────────────────────────────────────────────

        public override void StartMode(int bpm, double virtualStartTime, double realDspStartTime)
        {
            if (stems == null || stages == null || stages.Length == 0 || stages[0].layerVolumes.Length != stems.Length)
            {
                Debug.LogError("[Pulse] stems/stages not wired: need stages[0].layerVolumes.Length == stems.Length.");
                return;
            }

            _interval = 60.0 / bpm;
            _origin   = virtualStartTime;
            _nextBeat = 0;
            _startHighScore = PlayerPrefs.GetInt(HsKey, 0);
            _stage = -1;
            _mistakeBar = -1;
            _barMistakes = _prevBarMistakes = 0;
            _driftLogTimer = DriftLogSeconds;

            foreach (var src in _stemSources) src.PlayScheduled(realDspStartTime);

            if (drumPad != null) drumPad.OnCenterHit += HandleTap;
            _running = true;
            ApplyStage();
            UpdateHud();
            Debug.Log($"[Pulse] Started: bpm={bpm} interval={_interval:F4}s perfect=±{PerfectWindow * 1000:F0}ms good=±{GoodWindow * 1000:F0}ms stems={stems.Length}");
        }

        public override void Cleanup()
        {
            if (_score > _startHighScore) { PlayerPrefs.SetInt(HsKey, _score); PlayerPrefs.Save(); }
            StopRun();
        }

        public override void ResetToInitialState()
        {
            StopRun();
            _score = _perfectHits = _streak = 0;
            _stage = -1;
            UpdateHud();
        }

        public override void OnPause()
        {
            foreach (var src in _stemSources) src.Pause();
        }

        public override void OnResume()
        {
            foreach (var src in _stemSources) src.UnPause();
            LogStemAlignment("POST-RESUME");
        }

        // ── Gameplay ──────────────────────────────────────────────────────────

        private void HandleTap()
        {
            if (!_running || GameClock.Instance.IsPaused) return;

            PlayShaker();
            double t = GameClock.Instance.GameTime;
            long k = (long)System.Math.Round((t - _origin) / _interval);
            if (k < 0) return;

            // Beats between the pending one and the tapped one were skipped.
            while (k > _nextBeat)
            {
                Debug.Log($"[Pulse] beat {_nextBeat} MISSED (skipped)");
                RegisterMistake(_nextBeat);
                _nextBeat++;
            }

            double err = t - BeatTime(k);
            double abs = System.Math.Abs(err);
            if (k < _nextBeat || abs > GoodWindow)
            {
                Debug.Log($"[Pulse] beat {k} STRAY tap err={err * 1000:+0;-0}ms");
                RegisterMistake(k);
                return;
            }

            bool perfect = abs <= PerfectWindow;
            _score += perfect ? PerfectReward : GoodReward;
            if (perfect) _perfectHits++;
            _streak++;
            _nextBeat = k + 1;
            Debug.Log($"[Pulse] beat {k} {(perfect ? "PERFECT" : "GOOD")} err={err * 1000:+0;-0}ms streak={_streak}");
            ApplyStage();
            UpdateHud();
        }

        /// <summary>
        /// A mistake floors the streak at the current stage's entry value (progress
        /// toward the next stage is lost). The second mistake in a bar drops one
        /// stage; if the previous bar also had two or more, revert to stage 0.
        /// </summary>
        private void RegisterMistake(long beatIndex)
        {
            long bar = beatIndex / BeatsPerBar;
            if (bar != _mistakeBar)
            {
                _prevBarMistakes = bar == _mistakeBar + 1 ? _barMistakes : 0;
                _barMistakes = 0;
                _mistakeBar = bar;
            }
            _barMistakes++;

            int target = _stage;
            if (_barMistakes == 2)
            {
                target = _prevBarMistakes >= 2 ? 0 : Mathf.Max(0, _stage - 1);
                Debug.Log($"[Pulse] bar {bar}: 2 mistakes{(_prevBarMistakes >= 2 ? " after a 2-mistake bar — REVERT to stage 0" : " — drop one stage")}");
            }
            _streak = stages[Mathf.Max(0, target)].streak;
            ApplyStage();
            UpdateHud();
        }

        /// <summary>Highest stage whose streak threshold is met; fades stems to its volumes.</summary>
        private void ApplyStage()
        {
            int target = 0;
            for (int i = 0; i < stages.Length; i++)
                if (stages[i].streak <= _streak) target = i;
            if (target == _stage) return;
            _stage = target;

            var vols = stages[target].layerVolumes;
            for (int i = 0; i < _stemSources.Length; i++)
                _stemSources[i].DOFade(i < vols.Length ? vols[i] : 0f, layerFadeSeconds).SetUpdate(true);
            Debug.Log($"[Pulse] stage {target} (streak {_streak}) volumes=[{string.Join(",", vols)}]");
        }

        private void PlayShaker()
        {
            var audio = AudioManager.instance;
            if (shakerClip != null && audio != null && audio.speakers != null && audio.speakers.Length > 1 && audio.speakers[1] != null)
                audio.speakers[1].PlayOneShot(shakerClip);

            if (shakerVisual != null)
            {
                shakerVisual.DOKill();
                shakerVisual.localScale = Vector3.one;
                shakerVisual.DOPunchScale(Vector3.one * 0.15f, 0.15f, 1, 0f);
            }
        }

        private void StopRun()
        {
            _running = false;
            if (drumPad != null) drumPad.OnCenterHit -= HandleTap;
            foreach (var src in _stemSources) { src.DOKill(); src.Stop(); src.volume = 0f; }
        }

        private void UpdateHud()
        {
            int stage = Mathf.Max(0, _stage);
            if (streakText != null) streakText.text = $"Streak: {_streak - (stages != null && stages.Length > stage ? stages[stage].streak : 0)}";
            if (stageText  != null) stageText.text  = $"Stage {stage + 1}";
            if (scoreText  != null) scoreText.text  = _score.ToString();
        }

        // Sample-lock diagnostic (StemPlayer style): identical timeSamples = locked.
        private void LogStemAlignment(string label)
        {
            var sb = new System.Text.StringBuilder($"[Pulse:{label}] dsp={AudioSettings.dspTime:F3} ");
            for (int i = 0; i < _stemSources.Length; i++)
                sb.Append($"[{i}]={_stemSources[i].timeSamples} ");
            Debug.Log(sb.ToString());
        }
    }
}
