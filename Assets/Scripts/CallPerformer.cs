using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>
    /// Single dedicated enemy performer for Call-and-Response encounters.
    ///
    /// Holds one scene-placed Transform (NOT from the standard combat pool).
    /// Strike(DungeonEnemyType) is the sole entry point for per-beat call visuals:
    ///   - Directional bounce (Left/Center/Right) on the performer Transform, via DOTween.
    ///   - Audio fires via PlayScheduled from PrepareCallPhrase — sample-accurate,
    ///     independent of frame timing. This mirrors DungeonBeatManager.ScheduleSpawnAudio
    ///     exactly and is why sounds are pre-scheduled rather than triggered inline.
    ///
    /// PLACEHOLDER AUDIO: assign bongoSounds[2/3/4] (pad L/C/R) to strikeClips[0/1/2].
    /// Swap final clips later — callers never change.
    ///
    /// INSPECTOR SETUP:
    ///   performerTransform — scene-placed Transform (not pooled)
    ///   performerRenderer  — SpriteRenderer on performer (drives fade/color)
    ///   strikeClips[3]     — per-type audio clips: index 0=Left, 1=Center, 2=Right
    ///   handoffCueClip     — audio cue at response window open
    ///   bounceDistance     — world-unit punch offset (tune in Inspector)
    ///   bounceDuration     — punch-return duration in seconds
    /// </summary>
    public class CallPerformer : MonoBehaviour
    {
        #region Inspector
        [Header("Performer Object")]
        [Tooltip("Dedicated scene-placed Transform. NOT from the standard combat enemy pool.")]
        [SerializeField] private Transform performerTransform;

        [Tooltip("SpriteRenderer on the performer — drives color/fade for handoff cue.")]
        [SerializeField] private SpriteRenderer performerRenderer;

        [Header("Strike Audio (index 0=Left, 1=Center, 2=Right)")]
        [Tooltip("Per-type strike clips. Assign pad L/C/R sounds as placeholders; swap final clips later.")]
        [SerializeField] private AudioClip[] strikeClips;

        [Tooltip("Distinct audio cue played when the call ends and the response window opens.")]
        [SerializeField] private AudioClip handoffCueClip;

        [Header("Sprite Frames")]
        [Tooltip("Sprite shown briefly on each strike. Leave empty to skip the frame swap.")]
        [SerializeField] private Sprite attackSprite;

        [Header("Bounce")]
        [Tooltip("World-unit distance of the directional punch.")]
        [SerializeField] private float bounceDistance = 0.5f;

        [Tooltip("Duration of the full punch-return cycle in seconds.")]
        [SerializeField] private float bounceDuration = 0.15f;

        [Header("Jump-In")]
        [Tooltip("World units above rest position where the performer starts its entrance.")]
        [SerializeField] private float jumpInDistance = 3f;

        [Tooltip("Duration of the entrance slide-and-fade in seconds.")]
        [SerializeField] private float jumpInDuration = 0.4f;
        #endregion

        #region Private State
        private Vector3 _restPosition;
        private Color   _restColor;
        private Sprite  _restSprite;
        private Tween   _activeTween;
        private Tween   _spriteTween;

        // Tracked for cleanup on interrupt/pause — mirrors DungeonBeatManager._scheduledAudio.
        private readonly List<ScheduledStrikeVoice> _scheduledVoices = new();

        private class ScheduledStrikeVoice
        {
            public AudioSource source;
        }
        #endregion

        #region Unity
        private void Awake()
        {
            if (performerTransform != null)
                _restPosition = performerTransform.localPosition;

            _restColor  = performerRenderer != null ? performerRenderer.color  : Color.white;
            _restSprite = performerRenderer != null ? performerRenderer.sprite : null;

            // Start hidden so the performer is invisible before its encounter begins.
            HideImmediate();
        }

        private void OnDisable() => StopAndReset();
        private void OnDestroy() => StopAndReset();
        #endregion

        #region Public API
        /// <summary>
        /// Pre-schedules beat-accurate audio for every strike in this round and for
        /// the handoff cue at the response boundary. Call once at OnCallStart, before
        /// the call window opens, so each PlayScheduled has sufficient DSP lookahead.
        ///
        /// Audio fires automatically via the Unity audio thread — independent of frame
        /// timing. This is why sounds are not triggered inside Strike(); Strike() owns
        /// only the visual bounce.
        /// </summary>
        /// <param name="phrase">The call phrase for this round (from CallAndResponseController.CurrentCallPhrase).</param>
        /// <param name="responseStartVirtual">
        /// Virtual time of the response boundary — used to schedule the handoff cue.
        /// </param>
        public void PrepareCallPhrase(IReadOnlyList<DungeonScheduledBeat> phrase, double responseStartVirtual)
        {
            CancelScheduledVoices();

            if (phrase == null || GameClock.Instance == null) return;

            foreach (var beat in phrase)
            {
                AudioClip clip = GetStrikeClip(beat.enemyType);
                if (clip == null) continue;

                double realDsp = GameClock.Instance.VirtualToRealDsp(beat.scheduledTime);
                var src = CreateVoice(clip, realDsp);
                if (src != null)
                    _scheduledVoices.Add(new ScheduledStrikeVoice { source = src });
            }

            // Handoff cue — scheduled at the response boundary
            if (handoffCueClip != null)
            {
                double realDsp = GameClock.Instance.VirtualToRealDsp(responseStartVirtual);
                var src = CreateVoice(handoffCueClip, realDsp);
                if (src != null)
                    _scheduledVoices.Add(new ScheduledStrikeVoice { source = src });
            }

            Debug.Log($"[CallPerformer] Audio scheduled — {phrase.Count} strike(s) + handoff cue");
        }

        /// <summary>
        /// Triggers the directional bounce for the given enemy type.
        /// Audio for this beat was pre-scheduled by PrepareCallPhrase.
        /// Kills any in-flight bounce before starting the new one so rapid half-beat
        /// strikes each read as a distinct hit.
        /// </summary>
        public void Strike(DungeonEnemyType type)
        {
            if (performerTransform == null) return;

            // Kill and snap to rest so each strike starts from a clean position.
            _activeTween?.Kill(complete: true);
            performerTransform.localPosition = _restPosition;

            Vector3 dir = type switch
            {
                DungeonEnemyType.Left   => Vector3.left,
                DungeonEnemyType.Center => Vector3.up,
                DungeonEnemyType.Right  => Vector3.right,
                _                       => Vector3.up,
            };

            _activeTween = performerTransform
                .DOPunchPosition(dir * bounceDistance, bounceDuration, vibrato: 0, elasticity: 0.5f)
                .SetLink(gameObject);

            // Swap to attack frame and restore after the bounce completes.
            if (attackSprite != null && performerRenderer != null)
            {
                _spriteTween?.Kill(complete: false);
                performerRenderer.sprite = attackSprite;
                _spriteTween = DOVirtual.DelayedCall(bounceDuration, RestoreSprite)
                    .SetLink(gameObject);
            }

            Debug.Log($"[CallPerformer] Strike {type} | dir={dir} dist={bounceDistance}");
        }

        /// <summary>
        /// Visual signal that the call window has closed and the response window is open.
        /// Dims the performer to a passive state. The handoff cue audio was pre-scheduled
        /// by PrepareCallPhrase and fires at the exact response boundary.
        /// </summary>
        public void ShowHandoffCue()
        {
            _activeTween?.Kill(complete: true);
            _spriteTween?.Kill(complete: false);
            RestoreSprite();

            if (performerTransform != null)
                performerTransform.localPosition = _restPosition;

            if (performerRenderer != null)
            {
                Color dim = new Color(_restColor.r, _restColor.g, _restColor.b, 0.25f);
                _activeTween = performerRenderer
                    .DOColor(dim, 0.08f)
                    .SetLink(gameObject);
            }

            Debug.Log("[CallPerformer] Handoff cue — performer dimmed, response window open");
        }

        /// <summary>
        /// Per-tap reaction during the response/echo phase.
        /// Matches the signature of DungeonInputReader.OnHit so it can be subscribed directly.
        /// Perfect/Good: small confirming bounce in the struck direction.
        /// Any miss quality: brief red flash, returning to the current passive (dimmed) colour.
        /// </summary>
        public void OnEchoHit(DungeonEnemyType type, InputMatch.MatchQuality quality)
        {
            bool isHit = quality == InputMatch.MatchQuality.Perfect
                      || quality == InputMatch.MatchQuality.Good;

            if (isHit)
            {
                if (performerTransform == null) return;

                Vector3 dir = type switch
                {
                    DungeonEnemyType.Left   => Vector3.left,
                    DungeonEnemyType.Center => Vector3.up,
                    DungeonEnemyType.Right  => Vector3.right,
                    _                       => Vector3.up,
                };

                // Smaller + faster than the call bounce so it reads as a confirmation, not a strike.
                _activeTween?.Kill(complete: true);
                performerTransform.localPosition = _restPosition;
                _activeTween = performerTransform
                    .DOPunchPosition(dir * bounceDistance * 0.35f, bounceDuration * 0.5f, vibrato: 0, elasticity: 0.5f)
                    .SetLink(gameObject);
            }
            else if (performerRenderer != null)
            {
                // Flash red briefly, returning to whatever colour the performer currently has
                // (typically the dimmed passive colour set by ShowHandoffCue).
                Color currentColor = performerRenderer.color;
                Color flashColor   = new Color(1f, 0.15f, 0.15f, currentColor.a);

                _activeTween?.Kill(complete: true);
                _activeTween = DOTween.Sequence()
                    .Append(performerRenderer.DOColor(flashColor,   0.04f))
                    .Append(performerRenderer.DOColor(currentColor, 0.08f))
                    .SetLink(gameObject);
            }

            Debug.Log($"[CallPerformer] Echo reaction | type={type} quality={quality}");
        }

        /// <summary>
        /// Restores the performer to its full active state (full opacity, rest position).
        /// Call at the start of each new round before presenting the call.
        /// </summary>
        public void ResetToActiveState()
        {
            _activeTween?.Kill(complete: true);
            _activeTween = null;
            _spriteTween?.Kill(complete: false);
            _spriteTween = null;

            if (performerTransform != null)
                performerTransform.localPosition = _restPosition;

            if (performerRenderer != null)
            {
                performerRenderer.color  = _restColor;
                RestoreSprite();
            }
        }

        /// <summary>
        /// Entrance animation: performer slides down from above the rest position and fades in.
        /// Call once when the first call phrase fires to introduce the performer.
        /// For subsequent rounds use ResetToActiveState() instead.
        /// </summary>
        public void JumpIn()
        {
            _activeTween?.Kill(complete: false);
            _activeTween = null;
            _spriteTween?.Kill(complete: false);
            _spriteTween = null;

            if (performerTransform == null) return;

            // Place above rest position, fully transparent.
            performerTransform.localPosition = _restPosition + Vector3.up * jumpInDistance;
            if (performerRenderer != null)
            {
                Color hidden = _restColor;
                hidden.a = 0f;
                performerRenderer.color = hidden;
                RestoreSprite();
            }

            // Slide to rest with overshoot; fade completes at 60% of the slide duration.
            var seq = DOTween.Sequence().SetLink(gameObject);
            seq.Append(performerTransform.DOLocalMove(_restPosition, jumpInDuration).SetEase(Ease.OutBack));
            if (performerRenderer != null)
                seq.Join(performerRenderer.DOColor(_restColor, jumpInDuration * 0.6f));
            _activeTween = seq;
        }

        /// <summary>
        /// Kills all tweens, stops all scheduled audio voices, and hides the performer.
        /// Safe to call from OnDisable/OnDestroy and on encounter interrupt.
        /// </summary>
        public void StopAndReset()
        {
            _activeTween?.Kill(complete: false);
            _activeTween = null;
            _spriteTween?.Kill(complete: false);
            _spriteTween = null;

            CancelScheduledVoices();
            HideImmediate();
        }
        #endregion

        #region Private
        private void HideImmediate()
        {
            if (performerTransform != null)
                performerTransform.localPosition = _restPosition + Vector3.up * jumpInDistance;
            if (performerRenderer != null)
            {
                Color c = _restColor;
                c.a = 0f;
                performerRenderer.color = c;
            }
            RestoreSprite();
        }

        private void RestoreSprite()
        {
            if (performerRenderer != null && _restSprite != null)
                performerRenderer.sprite = _restSprite;
        }

        private void CancelScheduledVoices()
        {
            foreach (var v in _scheduledVoices)
            {
                if (v.source != null)
                {
                    v.source.Stop();
                    Destroy(v.source.gameObject);
                }
            }
            _scheduledVoices.Clear();
        }

        private AudioClip GetStrikeClip(DungeonEnemyType type)
        {
            if (strikeClips == null) return null;
            int idx = (int)type;
            return idx >= 0 && idx < strikeClips.Length ? strikeClips[idx] : null;
        }

        /// <summary>
        /// Mirrors DungeonBeatManager.ScheduleSpawnAudio exactly: one-shot voice parented
        /// here, PlayScheduled with real DSP time, auto-destroyed when clip finishes.
        /// </summary>
        private AudioSource CreateVoice(AudioClip clip, double realDspTime)
        {
            var go  = new GameObject("StrikeVoice");
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.clip        = clip;
            src.playOnAwake = false;
            src.PlayScheduled(realDspTime);
            Destroy(go, clip.length + 3f);
            return src;
        }
        #endregion
    }
}
