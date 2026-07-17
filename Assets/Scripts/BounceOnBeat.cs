using System;
using UnityEngine;

public class BounceOnBeat : MonoBehaviour
{
    [Header("Metronome Reference")]
    public Metronome metronome;

    [Header("Bounce Settings")]
    public float baseBounceScaleY = 1.3f;
    public float bounceIntensity = 1.0f;

    [Header("Damping Settings")]
    public float smoothTime = 0.15f; // How quickly it returns to original
    public bool useCurve = false;
    public AnimationCurve bounceCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    private Vector3 originalScale;
    private float currentVelocity = 0f;
    private float currentYScale;
    private float bounceTimer = 0f;
    private float bounceDuration = 0.2f;
    private bool isBouncing = false;

    // Beat-grid polling state.
    //
    // _activeStartTick : the startTick currently driving beat detection.
    // _pendingStartTick: a new startTick staged when InitializeWithStartTime()
    //                    fires at room start.  The switch is deferred until the
    //                    seam DSP time actually arrives so the old beat grid
    //                    keeps firing continuously right through the
    //                    cover/reveal transition — zero gap between rooms.
    //
    // Why deferred? InitializeWithStartTime() is called ~0.35 s before the seam
    // while the screen is uncovering. Switching immediately would silence the
    // bounce for that 0.35 s window, which the player perceives as a stop at
    // the end of the outgoing room.  Staying on the old grid until the seam
    // means the bounce fires at regular beat intervals all the way to the bar
    // boundary, then re-aligns to the new grid exactly at beat 0.
    private int    _lastBeatIndex    = -1;
    private double _activeStartTick  = double.MinValue;
    private double _pendingStartTick = 0.0;

    private void Start()
    {
        if (metronome == null)
            metronome = GameManager.instance.metronome;

        originalScale = transform.localScale;
        currentYScale = originalScale.y;
    }

    private void Update()
    {
        DetectBeat();

        // Skip the damping/scale step while time is frozen (paused, timeScale = 0).
        // Unity's Mathf.SmoothDamp divides by deltaTime in its overshoot clamp,
        // which produces a NaN velocity when deltaTime is 0 and current == target,
        // corrupting the scale with NaN on subsequent frames.
        if (Time.deltaTime <= 0f) return;

        bounceTimer += Time.deltaTime;

        if (useCurve)
        {
            float t = bounceTimer / bounceDuration;
            if (t >= 1f)
            {
                currentYScale = originalScale.y;
                isBouncing = false;
            }
            else
            {
                float curveValue = bounceCurve.Evaluate(t);
                float targetY = Mathf.Lerp(baseBounceScaleY * bounceIntensity, originalScale.y, curveValue);
                currentYScale = targetY;
            }
        }
        else
        {
            currentYScale = Mathf.SmoothDamp(currentYScale, originalScale.y, ref currentVelocity, smoothTime);
        }

        transform.localScale = new Vector3(originalScale.x, currentYScale, originalScale.z);
    }

    private void DetectBeat()
    {
        if (metronome == null || GameClock.Instance == null || GameClock.Instance.IsPaused)
            return;

        // Only bounce while a session is live. Both GameManager (Bongo) and
        // DungeonModeController enable the metronome at session start, and
        // CleanupSharedSystems() disables it when the song/run ends. We can't
        // rely on startTick alone: Metronome.ResetToInitialState() sets
        // startTick to the current dspTime, which looks like a valid grid and
        // would keep the bounce running forever after the song finishes.
        // The metronome stays enabled across dungeon room transitions, so the
        // deferred beat-grid handoff below is unaffected.
        if (!metronome.enabled)
        {
            // Drop the stale grid so the next session starts like a first init.
            _activeStartTick  = double.MinValue;
            _pendingStartTick = 0.0;
            _lastBeatIndex    = -1;
            return;
        }

        if (metronome.startTick <= 0 || metronome.bpm <= 0)
            return;

        double beatInterval = 60.0 / metronome.bpm;

        // Stage a new startTick whenever InitializeWithStartTime() assigns one.
        // Two cases:
        //   a) First-ever init — no old grid exists, activate immediately.
        //   b) Re-init (new room) — defer; keep the old grid alive until the seam.
        if (metronome.startTick != _activeStartTick && metronome.startTick != _pendingStartTick)
        {
            _pendingStartTick = metronome.startTick;

            if (_activeStartTick == double.MinValue)
            {
                // First init: activate now so beat detection can begin.
                _activeStartTick  = _pendingStartTick;
                _lastBeatIndex    = -1;
                _pendingStartTick = 0.0;
            }
        }

        // Switch to the new grid the moment its seam time is reached.
        // Doing this BEFORE the beat check below means the seam beat fires
        // on the new grid (index 0) rather than the old grid, so we never
        // double-fire the boundary beat.
        if (_pendingStartTick > 0.0 && AudioSettings.dspTime >= _pendingStartTick)
        {
            _activeStartTick  = _pendingStartTick;
            _pendingStartTick = 0.0;
            _lastBeatIndex    = -1;
        }

        if (_activeStartTick <= 0.0) return;

        double elapsed = AudioSettings.dspTime - _activeStartTick;
        if (elapsed < 0.0) return;

        int beatIndex = (int)Math.Floor(elapsed / beatInterval);
        if (beatIndex > _lastBeatIndex)
        {
            _lastBeatIndex = beatIndex;
            TriggerBounce();
        }
    }

    private void TriggerBounce()
    {
        currentYScale = baseBounceScaleY * bounceIntensity;
        isBouncing    = true;
        bounceTimer   = 0f;
    }
}
