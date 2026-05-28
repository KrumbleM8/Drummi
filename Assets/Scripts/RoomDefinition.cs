using UnityEngine;

/// <summary>
/// Defines the properties of a single dungeon room encounter.
/// Assign to a RoomRunner (or equivalent) to configure what enemies,
/// music, and pattern difficulty the room uses.
/// </summary>
[CreateAssetMenu(fileName = "NewRoom", menuName = "Drummi Dungeons/Room Definition")]
public class RoomDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Display name for this room.")]
    [SerializeField] private string roomName;

    [Tooltip("Determines enemy type spawned in this room.")]
    [SerializeField] private EnemyTier tier;

    [Header("Gameplay")]
    [Tooltip("Pattern durations passed to DungeonBeatManager — overrides the default { 1f, 0.5f }. Leave empty to use defaults.")]
    [SerializeField] private float[] patternDurations;

    [Tooltip("Index into AudioManager.musicTracks to play for this room.")]
    [SerializeField] private int bgmTrackIndex;

    [Header("Visuals")]
    [Tooltip("Background sprite swapped in when this room is entered.")]
    [SerializeField] private Sprite backgroundSprite;

    [Tooltip("Key used to look up visual theme overrides. Unused until theming system is implemented.")]
    [SerializeField] private string artOverrideKey;

    [Header("Room Type")]
    [Tooltip("CombatEncounter — normal enemy fight. DirectionChoice — player picks a door, no combat.")]
    [SerializeField] private RoomType roomType = RoomType.CombatEncounter;

    [Header("BGM Override")]
    [Tooltip("If true, this room switches to BgmTrackIndex when entered. If false, the current music continues uninterrupted.")]
    [SerializeField] private bool overrideBgm;

    [Tooltip("Room length in bars. Used when music is continuous so the room ends at the right time rather than relying on clip length. 0 = derive from clip length (first room or override rooms).")]
    [SerializeField] private int roomLengthBars;

    // ── Accessors ─────────────────────────────────────────────────────────────

    /// <summary>Display name for this room.</summary>
    public string RoomName => roomName;

    /// <summary>Enemy tier that determines which enemy type is spawned.</summary>
    public EnemyTier Tier => tier;

    /// <summary>
    /// Pattern durations to pass to DungeonBeatManager.Initialize().
    /// Returns null when the array is empty, signalling "use defaults".
    /// </summary>
    public float[] PatternDurations => (patternDurations != null && patternDurations.Length > 0)
        ? patternDurations
        : null;

    /// <summary>AudioManager.musicTracks index for this room's BGM.</summary>
    public int BgmTrackIndex => bgmTrackIndex;

    /// <summary>Background sprite to display while this room is active. May be null.</summary>
    public Sprite BackgroundSprite => backgroundSprite;

    /// <summary>Visual theme key — reserved for future use.</summary>
    public string ArtOverrideKey => artOverrideKey;

    /// <summary>Room archetype — determines whether combat or direction-choice flow is used.</summary>
    public RoomType RoomType => roomType;

    /// <summary>
    /// When true, this room overrides the current BGM track with <see cref="BgmTrackIndex"/>
    /// at the next bar boundary. When false, the current music plays through uninterrupted.
    /// </summary>
    public bool OverrideBgm => overrideBgm;

    /// <summary>
    /// Room length in bars. When non-zero, used instead of clip length to determine
    /// how many beats the combat encounter lasts. Required for rooms that do not
    /// restart the music (OverrideBgm = false) so the room ends at the right time.
    /// </summary>
    public int RoomLengthBars => roomLengthBars;
}

/// <summary>Enemy difficulty tier for a dungeon room.</summary>
public enum EnemyTier
{
    Standard,
    Elite,
    Boss,
}

/// <summary>Room archetype — determines the gameplay flow inside the room.</summary>
public enum RoomType
{
    /// <summary>Standard combat encounter: enemy pattern + evaluation cycle.</summary>
    CombatEncounter,

    /// <summary>No combat — player chooses a door (left/right) to advance.</summary>
    DirectionChoice,
}
