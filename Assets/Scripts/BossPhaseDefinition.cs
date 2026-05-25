using UnityEngine;

/// <summary>
/// Defines a single phase of a Boss encounter.
/// BossController steps through a list of these in order as boss HP drops.
/// </summary>
[CreateAssetMenu(fileName = "NewBossPhase", menuName = "Drummi Dungeons/Boss Phase Definition")]
public class BossPhaseDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Display name for this phase (used in logs and debug UI).")]
    [SerializeField] private string phaseName;

    [Header("Rhythm")]
    [Tooltip("Pattern duration weights passed to DungeonBeatManager. Null/empty = use room defaults.")]
    [SerializeField] private float[] patternDurations;

    [Tooltip("Multiplier applied to the metronome BPM when this phase begins. 1.0 = no change.")]
    [SerializeField] private float bpmMultiplier = 1f;

    [Header("Mechanics")]
    [Tooltip("Phase 5d: hides the bar slider indicator, forcing the player to play from memory.")]
    [SerializeField] private bool removeVisualIndicator = false;

    [Tooltip("Boss transitions into this phase when boss HP drops below this value. " +
             "Set to 0 for the final phase (no further transition).")]
    [SerializeField] private int bossHPThreshold;

    // ── Accessors ─────────────────────────────────────────────────────────────

    /// <summary>Display name for this phase.</summary>
    public string PhaseName => phaseName;

    /// <summary>
    /// Pattern duration weights for this phase.
    /// Returns null when the array is empty — callers should fall back to room defaults.
    /// </summary>
    public float[] PatternDurations => (patternDurations != null && patternDurations.Length > 0)
        ? patternDurations
        : null;

    /// <summary>BPM multiplier applied when this phase starts. 1.0 = no change.</summary>
    public float BpmMultiplier => bpmMultiplier;

    /// <summary>When true, the bar slider indicator is hidden for this phase (Phase 5d mechanic).</summary>
    public bool RemoveVisualIndicator => removeVisualIndicator;

    /// <summary>
    /// Boss HP threshold that triggers entry into this phase.
    /// 0 indicates the final phase with no further transition.
    /// </summary>
    public int BossHPThreshold => bossHPThreshold;
}
