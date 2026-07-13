using System;
using System.Collections.Generic;
using UnityEngine;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>
    /// Manages pooled <see cref="CRIndicator"/> objects for Call-and-Response encounters.
    ///
    /// MECHANIC:
    ///   Call phase   — on OnCallStart, one indicator per note flies from the enemy origin up
    ///                  to a randomised hang position and settles there. The whole phrase is
    ///                  visible as a cluster once all indicators have launched.
    ///   Response phase — indicators attack in firing order, each flying from its hang position
    ///                  to the drum landing point. Each attack's start time is computed so that
    ///                  the indicator arrives exactly at hitVirtualTime — matching the evaluator's
    ///                  expected tap time to the frame.
    ///   Hit feedback  — on DungeonInputReader.OnHit, the attacking indicator closest in time
    ///                  is diffused (scale-pop). Unresolved indicators are declared misses once
    ///                  their hit window has passed by missDetectOffset.
    ///
    /// TIMING:
    ///   responseHitTime_i  = callBeat_i.scheduledTime + N × beatInterval
    ///   attackStartVirtual = responseHitTime_i − attackDuration
    ///   This mirrors DungeonInputReader.SetTimeOffset(N × beatInterval) exactly — the same
    ///   offset the evaluator uses — so indicator timing and evaluation are always in lock-step.
    ///
    /// INSPECTOR SETUP:
    ///   callAndResponse  — CallAndResponseController driving the encounter
    ///   inputReader      — DungeonInputReader (OnHit subscription)
    ///   metronome        — Metronome (bpm source for beat-interval calculation)
    ///   enemyTransform   — world-space Transform at the enemy origin (launch source)
    ///   drumLandingPoint — world-space Transform at the center drum button (attack target)
    ///   indicatorPrefab  — optional sprite prefab; auto-generates fallback if null
    /// </summary>
    public class CRIndicatorController : MonoBehaviour
    {
        #region Inspector — References
        [Header("References")]
        [Tooltip("CallAndResponseController driving the encounter. Events are subscribed on Enable.")]
        [SerializeField] private CallAndResponseController callAndResponse;

        [Tooltip("DungeonInputReader — OnHit is subscribed for echo hit/miss feedback.")]
        [SerializeField] private DungeonInputReader inputReader;

        [Tooltip("Metronome — bpm is read at OnCallStart to compute beat interval.")]
        [SerializeField] private Metronome metronome;

        [Tooltip("CallPerformer in the scene. RestWorldPosition is used as the indicator spawn anchor — " +
                 "invariant regardless of JumpIn animation state. Takes priority over EnemyTransform.")]
        [SerializeField] private CallPerformer callPerformer;

        [Tooltip("Fallback spawn anchor when CallPerformer is not assigned. Prefer assigning CallPerformer instead.")]
        [SerializeField] private Transform enemyTransform;

        [Tooltip("World-space Transform placed on the center drum button. Attacking indicators land here.")]
        [SerializeField] private Transform drumLandingPoint;

        [Tooltip("Prefab for each indicator (needs a SpriteRenderer). Leave empty to use the auto-generated white-square fallback.")]
        [SerializeField] private GameObject indicatorPrefab;

        [Tooltip("Parent for pooled objects. Auto-created as 'CRIndicatorContainer' if null.")]
        [SerializeField] private Transform indicatorContainer;
        #endregion

        #region Inspector — Launch
        [Header("Launch")]
        [Tooltip("Seconds for the indicator to fly from enemy origin to its hang position.")]
        [SerializeField] private float launchDuration = 0.4f;

        [Tooltip("Easing for the launch flight. Evaluated 0→1 over launchDuration.")]
        [SerializeField] private AnimationCurve launchCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        #endregion

        #region Inspector — Hang Placement
        [Header("Hang Placement")]
        [Tooltip("Minimum XY offset relative to enemyTransform defining the hang region.")]
        [SerializeField] private Vector2 hangBoundsMin = new Vector2(-1.5f,  0.8f);

        [Tooltip("Maximum XY offset relative to enemyTransform defining the hang region.")]
        [SerializeField] private Vector2 hangBoundsMax = new Vector2( 1.5f,  2.5f);

        [Tooltip("Minimum world-unit distance enforced between any two hang positions. Rejection sampling; relaxed after maxPlacementAttempts.")]
        [SerializeField] private float minHangSeparation = 0.4f;

        [Tooltip("Maximum rejection-sampling attempts before the separation constraint is relaxed for this indicator.")]
        [SerializeField] private int maxPlacementAttempts = 20;
        #endregion

        #region Inspector — Hang Idle
        [Header("Hang Idle (Optional)")]
        [Tooltip("When true, hanging indicators bob gently. Must read as idle flavour — NOT as a timing cue.")]
        [SerializeField] private bool  hangIdleEnabled   = false;

        [Tooltip("World-unit amplitude of the idle bob.")]
        [SerializeField] private float hangIdleAmplitude = 0.1f;

        [Tooltip("Bob cycles per second.")]
        [SerializeField] private float hangIdleSpeed     = 2.0f;
        #endregion

        #region Inspector — Attack
        [Header("Attack")]
        [Tooltip("Seconds for the indicator to fly from hang position to drum. " +
                 "At t=1 exactly at hitVirtualTime: position = drumLandingPoint, scale = endScale.")]
        [SerializeField] private float attackDuration = 1.0f;

        [Tooltip("Easing for attack position AND scale (0→1). Ease-in reads as acceleration. " +
                 "Set to a curve that starts slow and accelerates for the intended feel.")]
        [SerializeField] private AnimationCurve attackCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Tooltip("Indicator scale during hang phase and at the start of the attack (represents distance).")]
        [SerializeField] private float startScale = 0.3f;

        [Tooltip("Indicator scale at the drum landing point. Fixed and learnable — this is the player's hit-window read.")]
        [SerializeField] private float endScale = 1.0f;
        #endregion

        #region Inspector — Pre-Attack Cue
        [Header("Pre-Attack Cue (Optional)")]
        [Tooltip("When true, the next-to-attack indicator pulses gently before its attack starts.")]
        [SerializeField] private bool  preAttackCueEnabled   = false;

        [Tooltip("Beats before attack start at which the pre-attack pulse begins. " +
                 "Converted to virtual time via metronome.bpm — not a hardcoded duration.")]
        [SerializeField] private float preAttackCueLeadBeats = 1f;
        #endregion

        #region Inspector — Appearance
        [Header("Appearance")]
        [Tooltip("Color applied to all indicators in this encounter.")]
        [SerializeField] private Color indicatorColor = new Color(1f, 0.15f, 0.15f, 1f);
        #endregion

        #region Inspector — Miss Detection
        [Header("Miss Detection")]
        [Tooltip("Seconds after hitVirtualTime before an unresolved attacking indicator triggers its miss animation. " +
                 "Should exceed the evaluator's Good-window threshold to avoid false misses (~0.125 s at 120 BPM).")]
        [SerializeField] private float missDetectOffset = 0.35f;
        #endregion

        #region Inspector — Pool
        [Header("Pool")]
        [Tooltip("Number of indicators pre-warmed at Start. Should exceed the longest possible phrase length.")]
        [SerializeField] private int poolCapacity = 12;
        #endregion

        #region Events
        /// <summary>
        /// Fired at the call→response boundary each round (forwarded from OnResponseStart).
        /// Attach audio/UI turn-change cues here without modifying CallAndResponseController.
        /// Parameters: roundIndex, phraseLengthN.
        /// </summary>
        public event Action<int, int> OnResponsePhaseStart;
        #endregion

        #region Private State
        private readonly List<CRIndicator> _pool   = new();
        private readonly List<CRIndicator> _active = new();

        // Hang positions assigned this round — used for rejection-sampling separation check.
        private readonly List<Vector3> _usedHangPositions = new();

        // Per-round timing snapshot (read at OnCallStart).
        private double _currentBeatInterval;

        // Cached default sprite for the fallback pool objects.
        private Sprite _defaultSprite;
        #endregion

        #region Unity — Lifecycle
        private void Start()
        {
            EnsureContainer();
            PrewarmPool();
        }

        private void OnEnable()
        {
            if (callAndResponse != null)
            {
                callAndResponse.OnCallStart         += HandleCallStart;
                callAndResponse.OnResponseStart     += HandleResponseStart;
                callAndResponse.OnEncounterComplete += HandleEncounterComplete;
            }
            if (inputReader != null)
                inputReader.OnHit += HandleHit;
        }

        private void OnDisable()
        {
            if (callAndResponse != null)
            {
                callAndResponse.OnCallStart         -= HandleCallStart;
                callAndResponse.OnResponseStart     -= HandleResponseStart;
                callAndResponse.OnEncounterComplete -= HandleEncounterComplete;
            }
            if (inputReader != null)
                inputReader.OnHit -= HandleHit;

            ClearAllIndicators();
        }

        private void Update()
        {
            // ── 1. Return Done indicators to pool ─────────────────────────────
            // Iterate in reverse so RemoveAt is safe. Done is set by CRIndicator.MarkDone()
            // after its resolve animation completes.
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i].State == CRIndicatorState.Done)
                {
                    ReturnToPool(_active[i]);
                    _active.RemoveAt(i);
                }
            }

            // ── 2. Miss detection ─────────────────────────────────────────────
            if (GameClock.Instance == null || GameClock.Instance.IsPaused) return;
            double now = GameClock.Instance.GameTime;

            foreach (var ind in _active)
            {
                if (ind.State == CRIndicatorState.Attacking &&
                    now > ind.HitVirtualTime + missDetectOffset)
                {
                    ind.TriggerMiss();
                }
            }
        }
        #endregion

        #region Private — C&R Event Handlers
        private void HandleCallStart(int roundIndex, int phraseN)
        {
            // Clear any leftover indicators from the previous round before building the new set.
            ClearAllIndicators();
            _usedHangPositions.Clear();

            var phrase = callAndResponse?.CurrentCallPhrase;
            if (phrase == null || phrase.Count == 0) return;

            _currentBeatInterval = metronome != null ? 60.0 / metronome.bpm : 0.5;

            // Use the performer's invariant rest position so all phrases — including
            // round 0, where JumpIn() has already moved the transform above rest —
            // launch from the same anchor. Fall back to enemyTransform for scenes
            // that predate this field or use a separate anchor object.
            Vector3 spawnPos = callPerformer != null
                ? callPerformer.RestWorldPosition
                : (enemyTransform != null ? enemyTransform.position : Vector3.zero);
            Vector3 landingPos = drumLandingPoint != null ? drumLandingPoint.position : Vector3.zero;

            for (int i = 0; i < phrase.Count; i++)
            {
                var    beat            = phrase[i];
                // Same time offset as DungeonInputReader.SetTimeOffset(N * beatInterval)
                // and EvaluateBar(timeOffset = N * beatInterval) — indicator timing is
                // in lock-step with the evaluator.
                double responseHitTime = beat.scheduledTime + phraseN * _currentBeatInterval;
                double preCueTime      = responseHitTime - (double)attackDuration
                                         - preAttackCueLeadBeats * _currentBeatInterval;
                Vector3 hangPos        = PickHangPosition(spawnPos);

                var d = new CRIndicator.InitData
                {
                    launchStartVirtual  = beat.scheduledTime,
                    spawnPosition       = spawnPos,
                    hangPosition        = hangPos,
                    landingPosition     = landingPos,
                    hitVirtualTime      = responseHitTime,
                    launchDuration      = launchDuration,
                    launchCurve         = launchCurve,
                    attackDuration      = attackDuration,
                    attackCurve         = attackCurve,
                    startScale          = startScale,
                    endScale            = endScale,
                    preAttackCueEnabled = preAttackCueEnabled,
                    preCueVirtualTime   = preCueTime,
                    hangIdleEnabled     = hangIdleEnabled,
                    hangIdleAmplitude   = hangIdleAmplitude,
                    hangIdleSpeed       = hangIdleSpeed,
                    color               = indicatorColor,
                };

                var ind = GetFromPool();
                ind.Init(in d);
                _active.Add(ind);
                // No explicit BeginLaunch() call needed — CRIndicator.Update() transitions
                // from Idle → Launching automatically when now >= launchStartVirtual.
            }

            Debug.Log($"[CRIndicatorController] Round {roundIndex}: {phrase.Count} indicator(s) ready " +
                      $"(N={phraseN}, beatInterval={_currentBeatInterval:F3}s, " +
                      $"attackDuration={attackDuration:F2}s)");
        }

        private void HandleResponseStart(int roundIndex, int phraseN)
        {
            Debug.Log($"[CRIndicatorController] Response phase — round {roundIndex} N={phraseN}");
            OnResponsePhaseStart?.Invoke(roundIndex, phraseN);
        }

        private void HandleEncounterComplete()
        {
            ClearAllIndicators();
            Debug.Log("[CRIndicatorController] Encounter complete — indicators cleared");
        }
        #endregion

        #region Private — Hit Handling
        /// <summary>
        /// Subscribed to DungeonInputReader.OnHit. OnHit fires only for Perfect/Good.
        /// Identifies the attacking indicator whose hit time is closest to now —
        /// mirrors EvaluateSingleInput's closest-beat logic so visual and grading always agree.
        /// </summary>
        private void HandleHit(DungeonEnemyType type, InputMatch.MatchQuality quality)
        {
            if (GameClock.Instance == null) return;
            double now      = GameClock.Instance.GameTime;
            double bestDist = double.MaxValue;
            CRIndicator best = null;

            foreach (var ind in _active)
            {
                // Include Hanging as well as Attacking — guards against very short attackDuration configs
                // where the attack window hasn't opened yet when an early (valid) hit arrives.
                if (ind.State != CRIndicatorState.Attacking &&
                    ind.State != CRIndicatorState.Hanging) continue;

                double dist = Math.Abs(ind.HitVirtualTime - now);
                if (dist < bestDist) { bestDist = dist; best = ind; }
            }

            best?.Diffuse();
        }
        #endregion

        #region Private — Hang Placement
        /// <summary>
        /// Returns a randomised world-space hang position within hangBounds (relative to enemy).
        /// Enforces minHangSeparation from all positions placed earlier this round.
        /// Falls back without the constraint after maxPlacementAttempts — cosmetic, not gameplay.
        /// </summary>
        private Vector3 PickHangPosition(Vector3 enemyPos)
        {
            for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
            {
                float   x   = UnityEngine.Random.Range(hangBoundsMin.x, hangBoundsMax.x);
                float   y   = UnityEngine.Random.Range(hangBoundsMin.y, hangBoundsMax.y);
                Vector3 pos = enemyPos + new Vector3(x, y, 0f);

                bool tooClose = false;
                foreach (var used in _usedHangPositions)
                {
                    if (Vector3.Distance(pos, used) < minHangSeparation)
                    { tooClose = true; break; }
                }

                if (!tooClose)
                {
                    _usedHangPositions.Add(pos);
                    return pos;
                }
            }

            // Separation relaxed — place anywhere in bounds.
            float fx       = UnityEngine.Random.Range(hangBoundsMin.x, hangBoundsMax.x);
            float fy       = UnityEngine.Random.Range(hangBoundsMin.y, hangBoundsMax.y);
            Vector3 fallback = enemyPos + new Vector3(fx, fy, 0f);
            _usedHangPositions.Add(fallback);
            Debug.LogWarning($"[CRIndicatorController] Separation relaxed for indicator {_usedHangPositions.Count} " +
                             $"— consider increasing hangBounds or reducing minHangSeparation.");
            return fallback;
        }
        #endregion

        #region Private — Pool Management
        private void PrewarmPool()
        {
            while (_pool.Count < poolCapacity)
            {
                var ind = CreateIndicator();
                ind.InPool = true;
                ind.gameObject.SetActive(false);
                _pool.Add(ind);
            }
        }

        private CRIndicator GetFromPool()
        {
            if (_pool.Count > 0)
            {
                var ind = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
                ind.InPool = false;
                ind.gameObject.SetActive(true);
                return ind;
            }

            Debug.LogWarning("[CRIndicatorController] Pool exhausted — creating extra indicator. " +
                             "Increase poolCapacity to avoid this.");
            var extra = CreateIndicator();
            extra.InPool = false;
            return extra;
        }

        private void ReturnToPool(CRIndicator ind)
        {
            if (ind == null) return;
            ind.ResetForPool();
            ind.gameObject.SetActive(false);
            ind.InPool = true;
            _pool.Add(ind);
        }

        private void ClearAllIndicators()
        {
            foreach (var ind in _active)
                ReturnToPool(ind);
            _active.Clear();
        }

        private CRIndicator CreateIndicator()
        {
            EnsureContainer();

            if (indicatorPrefab != null)
            {
                var go   = Instantiate(indicatorPrefab, indicatorContainer, false);
                var comp = go.GetComponent<CRIndicator>();
                if (comp == null) comp = go.AddComponent<CRIndicator>();
                return comp;
            }

            // Fallback: plain white-square sprite — same approach as DungeonVisualController.
            var fallback = new GameObject("CRIndicator");
            fallback.transform.SetParent(indicatorContainer, false);
            var sr = fallback.AddComponent<SpriteRenderer>();
            sr.sprite = GetOrCreateDefaultSprite();
            return fallback.AddComponent<CRIndicator>();
        }

        private void EnsureContainer()
        {
            if (indicatorContainer != null) return;
            indicatorContainer = new GameObject("CRIndicatorContainer").transform;
        }

        private Sprite GetOrCreateDefaultSprite()
        {
            if (_defaultSprite != null) return _defaultSprite;
            var tex    = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color[16];
            for (int i = 0; i < 16; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            _defaultSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return _defaultSprite;
        }
        #endregion
    }
}
