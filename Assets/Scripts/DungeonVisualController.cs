using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages all Dungeon mode visuals on the single shared bar slider.
///
/// SLIDER PHASES (both share one Slider component):
///   Spawn phase  (beats 1-4): slider 0→1 as enemies spawn; a coloured marker is
///                             placed at the current handle X for each spawned enemy.
///   Response phase (beats 5-8): slider resets to 0→1; a second fill overlay tracks
///                             response progress.  Player input markers are placed
///                             at the current handle X on each hit.
///   Spawn markers persist into the response phase so the player can see the
///   reference pattern underneath the response fill.
///
/// INSPECTOR SETUP:
///   barSlider          — the shared Slider (fill direction Left→Right, handle present)
///   indicatorParent    — RectTransform child of the slider; markers are instantiated here
///   responseFill       — a separate Image (Type=Filled, Horizontal) that overlays the
///                        slider fill area during the response phase
///   spawnIndicatorPrefab / inputIndicatorPrefab — small Image prefabs
///   metronome          — needed for BPM
/// </summary>
public class DungeonVisualController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Slider         barSlider;
    [SerializeField] private RectTransform  indicatorParent;
    [SerializeField] private Image          responseFill;

    [Header("Prefabs")]
    [SerializeField] private GameObject spawnIndicatorPrefab;
    [SerializeField] private GameObject inputIndicatorPrefab;

    [Header("Enemy Colors")]
    [SerializeField] private Color leftColor   = new Color(0.3f, 0.5f, 1f);    // blue-ish
    [SerializeField] private Color centerColor = new Color(1f,   0.85f, 0.1f); // gold
    [SerializeField] private Color rightColor  = new Color(1f,   0.3f,  0.3f); // red-ish
    [SerializeField] private Color missedColor = Color.gray;

    [Header("References")]
    [SerializeField] private Metronome metronome;

    // ── Private State ──────────────────────────────────────────────────────

    private double phaseDuration; // 4 beats — each phase (spawn or response) lasts 4 beats
    private bool   isFrozen;

    private class PendingMarker
    {
        public double          scheduledTime;
        public DungeonEnemyType enemyType;
    }
    private readonly List<PendingMarker> _pendingMarkers = new();

    // ── Public API — Lifecycle ────────────────────────────────────────────

    /// <summary>Called by DungeonModeController.StartMode() after BPM is set.</summary>
    public void Initialize()
    {
        phaseDuration = 4 * (60.0 / metronome.bpm);
        isFrozen      = false;

        if (barSlider   != null) barSlider.value = 0f;
        SetResponseFillActive(false);

        Debug.Log($"[DungeonVisualController] Initialized — phase duration: {phaseDuration:F3}s");
    }

    // ── Unity ─────────────────────────────────────────────────────────────

    private void Update()
    {
        if (GameClock.Instance == null || GameClock.Instance.IsPaused || isFrozen) return;

        var coordinator = TimingCoordinator.Instance;
        if (coordinator == null || barSlider == null) return;

        var    bar = coordinator.CurrentBar;
        double now = GameClock.Instance.GameTime;

        bool inSpawnPhase    = now >= bar.BarStartTime    && now < bar.InputWindowStart;
        bool inResponsePhase = now >= bar.InputWindowStart && now < bar.EvaluationTime;

        if (inSpawnPhase)
        {
            double elapsed = now - bar.BarStartTime;
            barSlider.value = Mathf.Clamp01((float)(elapsed / phaseDuration));
            SetResponseFillActive(false);
        }
        else if (inResponsePhase)
        {
            double elapsed = now - bar.InputWindowStart;
            barSlider.value = Mathf.Clamp01((float)(elapsed / phaseDuration));
            SetResponseFillActive(true);
            if (responseFill != null)
                responseFill.fillAmount = barSlider.value;
        }

        // Trigger any pending spawn markers whose time has arrived
        var triggered = new List<PendingMarker>();
        foreach (var m in _pendingMarkers)
        {
            if (now >= m.scheduledTime)
            {
                PlaceSpawnMarker(m.enemyType);
                triggered.Add(m);
            }
        }
        foreach (var m in triggered) _pendingMarkers.Remove(m);
    }

    // ── Public API — Scheduling ───────────────────────────────────────────

    /// <summary>
    /// Called by DungeonBeatManager for each beat in the new pattern.
    /// The marker is placed when Update() detects the virtual time has passed.
    /// </summary>
    public void ScheduleSpawnMarker(double virtualTime, DungeonEnemyType type)
    {
        if (isFrozen) return;
        _pendingMarkers.Add(new PendingMarker { scheduledTime = virtualTime, enemyType = type });
    }

    /// <summary>
    /// Called immediately on player input by DungeonInputReader.
    /// Marker is placed at the current slider X position.
    /// </summary>
    public void SpawnInputMarker(DungeonEnemyType type, InputMatch.MatchQuality quality)
    {
        if (inputIndicatorPrefab == null || indicatorParent == null) return;

        var go = Instantiate(inputIndicatorPrefab, indicatorParent);

        // Colour: full enemy colour on hit, grey on miss
        if (go.TryGetComponent(out Image img))
        {
            bool isHit = quality == InputMatch.MatchQuality.Perfect || quality == InputMatch.MatchQuality.Good;
            img.color  = isHit ? GetColor(type) : missedColor;
        }

        if (go.TryGetComponent(out RectTransform rt))
            rt.anchoredPosition = new Vector2(GetCurrentSliderX(), 0f);

        // Tilt missed indicators the same way PlayerInputVisualHandler does
        if (quality != InputMatch.MatchQuality.Perfect && quality != InputMatch.MatchQuality.Good)
        {
            if (go.TryGetComponent(out UIQuickSpawnEffect fx))
            {
                fx.rotateAngle = quality == InputMatch.MatchQuality.TooEarly ? 45f : -45f;
                if (quality == InputMatch.MatchQuality.Miss || quality == InputMatch.MatchQuality.WrongSide)
                    fx.rotateAngle = 90f;
                fx.wrongHit   = true;
                fx.recoilScale = 0.8f;
            }
        }
    }

    // ── Public API — Reset / Cleanup ──────────────────────────────────────

    /// <summary>
    /// Called by DungeonBeatManager at the start of each new bar.
    /// Clears spawn markers from the previous bar so the slider is clean.
    /// </summary>
    public void ResetVisuals()
    {
        isFrozen = false;
        _pendingMarkers.Clear();

        if (barSlider != null) barSlider.value = 0f;
        SetResponseFillActive(false);

        if (indicatorParent != null)
            for (int i = indicatorParent.childCount - 1; i >= 0; i--)
                Destroy(indicatorParent.GetChild(i).gameObject);
    }

    public void FreezeVisuals()
    {
        isFrozen = true;
        _pendingMarkers.Clear();
        Debug.Log("[DungeonVisualController] Frozen");
    }

    public void CleanupAndDisable()
    {
        ResetVisuals();
        enabled = false;
    }

    // ── Private Helpers ───────────────────────────────────────────────────

    private void PlaceSpawnMarker(DungeonEnemyType type)
    {
        if (spawnIndicatorPrefab == null || indicatorParent == null) return;

        var go = Instantiate(spawnIndicatorPrefab, indicatorParent);
        if (go.TryGetComponent(out Image img)) img.color = GetColor(type);
        if (go.TryGetComponent(out RectTransform rt))
            rt.anchoredPosition = new Vector2(GetCurrentSliderX(), 0f);
    }

    private float GetCurrentSliderX()
    {
        if (barSlider == null || indicatorParent == null) return 0f;
        float w = indicatorParent.rect.width;
        return (barSlider.value * w) - (w / 2f);
    }

    private Color GetColor(DungeonEnemyType type) => type switch
    {
        DungeonEnemyType.Left   => leftColor,
        DungeonEnemyType.Center => centerColor,
        DungeonEnemyType.Right  => rightColor,
        _                       => Color.white
    };

    private void SetResponseFillActive(bool active)
    {
        if (responseFill == null) return;
        responseFill.gameObject.SetActive(active);
        if (!active) responseFill.fillAmount = 0f;
    }
}
