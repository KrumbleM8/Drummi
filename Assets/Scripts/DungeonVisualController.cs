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
/// ENEMY POOL:
///   Enemy GameObjects are pooled (PoolCapacity = 8) and never Instantiated/Destroyed
///   after the initial pre-warm in Initialize().
///   Hit despawns  (Scenario A) are triggered by NotifyEnemyHit(beat).
///   Bar-end despawns (Scenario B) are triggered by ResetVisuals().
///
/// PAIRING (Bug 2 fix):
///   Each PendingMarker holds a DungeonScheduledBeat reference set by DungeonBeatManager.
///   When the marker fires, the enemy is stored in _beatToEnemy[beat] and the indicator
///   GameObject is stored on the enemy as PairedIndicator.
///   NotifyEnemyHit(beat) resolves the exact paired enemy via this dict.
///
/// SORTING (Bug 3 fix):
///   First-spawned enemy in a bar gets sortingOrder = BaseEnemySortingOrder + barEnemyTotal,
///   so it renders in front; each subsequent enemy steps one order back.
///
/// INSPECTOR SETUP:
///   barSlider           — the shared Slider
///   indicatorParent     — RectTransform child of the slider; markers are instantiated here
///   responseFill        — a separate Image (Type=Filled, Horizontal) overlaid during response phase
///   spawnIndicatorPrefab / inputIndicatorPrefab — small Image prefabs
///   enemyContainer      — optional Transform to parent pool objects; auto-created if null
///   enemySprite         — optional square sprite; a 4×4 white texture is generated if null
///   enemySpawnY         — world-space Y for enemy mid-screen position (0 = camera centre)
///   enemySpawnBandWidth — total world-unit spread on X for the enemy group
///   metronome           — needed for BPM
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

    [Header("Enemy Visuals")]
    [SerializeField] private Transform enemyContainer;
    [SerializeField] private Sprite    enemySprite;
    [SerializeField] private float     enemySpawnY         = 0f;  // world-space Y; 0 = camera centre
    [SerializeField] private float     enemySpawnBandWidth = 4f;  // total world-unit spread on X

    [Header("References")]
    [SerializeField] private Metronome metronome;

    // ── Constants ──────────────────────────────────────────────────────────

    private const int PoolCapacity         = 8;
    private const int BaseEnemySortingOrder = 10; // keeps enemies above background/indicator layers

    // ── Private State ──────────────────────────────────────────────────────

    private double phaseDuration; // 4 beats — each phase (spawn or response) lasts 4 beats
    private bool   isFrozen;

    private Sprite _defaultEnemySprite;

    // Pool
    private readonly List<DungeonEnemyVisual>                     _enemyPool     = new();
    private readonly List<DungeonEnemyVisual>                     _activeEnemies = new();
    private readonly Dictionary<DungeonScheduledBeat, DungeonEnemyVisual> _beatToEnemy = new();

    // Bug 3 — per-bar spawn counter
    private int _barEnemyTotal;
    private int _barSpawnIndex;

    private class PendingMarker
    {
        public double               scheduledTime;
        public DungeonEnemyType     enemyType;
        public float                spawnX;  // world-space X, assigned by FinalizeBarEnemyPositions
        public DungeonScheduledBeat beat;    // the exact beat instance for pairing (Bug 2)
    }
    private readonly List<PendingMarker> _pendingMarkers = new();

    // ── Public API — Lifecycle ────────────────────────────────────────────

    /// <summary>Called by DungeonModeController.StartMode() after BPM is set.</summary>
    public void Initialize()
    {
        phaseDuration = 4 * (60.0 / metronome.bpm);
        isFrozen      = false;

        if (barSlider != null) barSlider.value = 0f;
        SetResponseFillActive(false);

        PrewarmPool();

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

        bool inSpawnPhase    = now >= bar.BarStartTime     && now < bar.InputWindowStart;
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
                PlaceSpawnMarker(m.enemyType, m.spawnX, m.beat);
                triggered.Add(m);
            }
        }
        foreach (var m in triggered) _pendingMarkers.Remove(m);
    }

    // ── Public API — Scheduling ───────────────────────────────────────────

    /// <summary>
    /// Called by DungeonBeatManager for each beat in the new pattern.
    /// beat is the same DungeonScheduledBeat instance stored in scheduledBeats so the
    /// _beatToEnemy lookup works by reference equality (Bug 2).
    /// </summary>
    public void ScheduleSpawnMarker(double virtualTime, DungeonEnemyType type, DungeonScheduledBeat beat)
    {
        if (isFrozen) return;
        _pendingMarkers.Add(new PendingMarker
        {
            scheduledTime = virtualTime,
            enemyType     = type,
            beat          = beat,
        });
    }

    /// <summary>
    /// Called by DungeonBeatManager after all beats for the bar have been scheduled.
    /// Distributes enemy spawn X positions evenly across the horizontal band and
    /// captures the total count for Bug 3 sorting.
    /// </summary>
    public void FinalizeBarEnemyPositions()
    {
        int   count = _pendingMarkers.Count;
        float half  = enemySpawnBandWidth * 0.5f;
        for (int i = 0; i < count; i++)
        {
            float t = count > 1 ? (float)i / (count - 1) : 0.5f;
            _pendingMarkers[i].spawnX = Mathf.Lerp(-half, half, t);
        }

        // Bug 3 — capture total for this bar so sorting order can be assigned per-spawn
        _barEnemyTotal = count;
        _barSpawnIndex = 0;
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
                fx.wrongHit    = true;
                fx.recoilScale = 0.8f;
            }
        }
    }

    /// <summary>
    /// Scenario A — called by DungeonInputReader on a Perfect or Good hit.
    /// Resolves the exact enemy paired to this specific beat (Bug 2 fix) and triggers
    /// its hit despawn animation.
    /// </summary>
    public void NotifyEnemyHit(DungeonScheduledBeat beat)
    {
        if (beat == null) return;
        if (!_beatToEnemy.TryGetValue(beat, out var visual)) return;

        _beatToEnemy.Remove(beat);
        _activeEnemies.Remove(visual);
        visual.DespawnHit(() => ReturnToPool(visual));
    }

    // ── Public API — Reset / Cleanup ──────────────────────────────────────

    /// <summary>
    /// Called by DungeonBeatManager at the start of each new bar.
    /// Clears spawn markers and UI indicators; triggers Scenario B despawn animation
    /// on any enemies not hit this bar.
    /// </summary>
    public void ResetVisuals()
    {
        isFrozen = false;
        _pendingMarkers.Clear();
        _beatToEnemy.Clear();
        _barEnemyTotal = 0;
        _barSpawnIndex = 0;

        if (barSlider != null) barSlider.value = 0f;
        SetResponseFillActive(false);

        if (indicatorParent != null)
            for (int i = indicatorParent.childCount - 1; i >= 0; i--)
                Destroy(indicatorParent.GetChild(i).gameObject);

        // Scenario B — animate remaining enemies upward and return to pool
        foreach (var visual in _activeEnemies)
        {
            var v = visual; // capture for closure
            v.DespawnBarEnd(() => ReturnToPool(v));
        }
        _activeEnemies.Clear();
    }

    public void FreezeVisuals()
    {
        isFrozen = true;
        _pendingMarkers.Clear();
        _beatToEnemy.Clear();

        // Force-return enemies without animation
        foreach (var visual in _activeEnemies)
            ReturnToPool(visual);
        _activeEnemies.Clear();

        Debug.Log("[DungeonVisualController] Frozen");
    }

    public void CleanupAndDisable()
    {
        _beatToEnemy.Clear();

        // Force-return enemies without animation (game ending)
        foreach (var visual in _activeEnemies)
            ReturnToPool(visual);
        _activeEnemies.Clear();

        ResetVisuals();
        enabled = false;
    }

    // ── Private — Marker Placement ────────────────────────────────────────

    private void PlaceSpawnMarker(DungeonEnemyType type, float spawnX, DungeonScheduledBeat beat)
    {
        if (spawnIndicatorPrefab == null || indicatorParent == null) return;

        var   go    = Instantiate(spawnIndicatorPrefab, indicatorParent);
        Color color = GetColor(type);
        if (go.TryGetComponent(out Image img)) img.color = color;
        if (go.TryGetComponent(out RectTransform rt))
            rt.anchoredPosition = new Vector2(GetCurrentSliderX(), 0f);

        SpawnEnemyVisual(spawnX, color, type, beat, go);
    }

    private void SpawnEnemyVisual(
        float               spawnX,
        Color               color,
        DungeonEnemyType    type,
        DungeonScheduledBeat beat,
        GameObject          indicatorGO)
    {
        var visual = GetFromPool();

        // Bug 3 — first enemy spawned this bar renders in front (highest sortingOrder)
        var sr = visual.GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.sortingOrder = BaseEnemySortingOrder + (_barEnemyTotal - _barSpawnIndex);
        _barSpawnIndex++;

        visual.Init(GetOrCreateEnemySprite(), color, new Vector3(spawnX, enemySpawnY, 0f), type, indicatorGO);
        _activeEnemies.Add(visual);

        // Bug 2 — register the beat→enemy pairing for exact hit resolution
        if (beat != null)
            _beatToEnemy[beat] = visual;
    }

    // ── Private — Object Pool ─────────────────────────────────────────────

    private void PrewarmPool()
    {
        EnsureEnemyContainer();
        while (_enemyPool.Count < PoolCapacity)
        {
            var v = CreatePooledEnemy();
            v.InPool = true;
            v.gameObject.SetActive(false);
            _enemyPool.Add(v);
        }
    }

    private DungeonEnemyVisual CreatePooledEnemy()
    {
        EnsureEnemyContainer();
        var go = new GameObject("EnemyVisual");
        go.transform.SetParent(enemyContainer, false);
        go.transform.localScale = Vector3.one * 0.5f;
        go.AddComponent<SpriteRenderer>();
        return go.AddComponent<DungeonEnemyVisual>();
    }

    private DungeonEnemyVisual GetFromPool()
    {
        if (_enemyPool.Count > 0)
        {
            var v = _enemyPool[_enemyPool.Count - 1];
            _enemyPool.RemoveAt(_enemyPool.Count - 1);
            v.InPool = false;
            v.gameObject.SetActive(true);
            return v;
        }

        // Pool exhausted — create an extra (should not happen with PoolCapacity = 8)
        Debug.LogWarning("[DungeonVisualController] Enemy pool exhausted — creating extra object");
        var extra = CreatePooledEnemy();
        extra.InPool = false;
        return extra;
    }

    private void ReturnToPool(DungeonEnemyVisual visual)
    {
        if (visual == null || visual.InPool) return; // stale callback guard
        visual.InPool = true;
        visual.StopAndReset();
        visual.gameObject.SetActive(false);
        _enemyPool.Add(visual);
    }

    // ── Private — Helpers ─────────────────────────────────────────────────

    private void EnsureEnemyContainer()
    {
        if (enemyContainer != null) return;
        enemyContainer = new GameObject("EnemyContainer").transform;
    }

    private Sprite GetOrCreateEnemySprite()
    {
        if (enemySprite != null) return enemySprite;
        if (_defaultEnemySprite != null) return _defaultEnemySprite;

        var tex    = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var pixels = new Color[16];
        for (int i = 0; i < 16; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        _defaultEnemySprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        return _defaultEnemySprite;
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
