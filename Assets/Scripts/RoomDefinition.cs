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

    [Header("Visuals")]
    [Tooltip("Background sprite swapped in when this room is entered.")]
    [SerializeField] private Sprite backgroundSprite;

    [Tooltip("Key used to look up visual theme overrides. Unused until theming system is implemented.")]
    [SerializeField] private string artOverrideKey;

    [Header("Room Type")]
    [Tooltip("CombatEncounter — normal enemy fight. DirectionChoice — player picks a door, no combat. CallAndResponse — enemy presents a phrase, player echoes it.")]
    [SerializeField] private RoomType roomType = RoomType.CombatEncounter;

    [Header("Call-and-Response Config")]
    [Tooltip("Ordered phrase lengths in beats for each C&R round (e.g. {2,4,6} = three escalating rounds). " +
             "Only meaningful when RoomType == CallAndResponse. " +
             "Leave empty to use CallAndResponseController Inspector defaults.")]
    [SerializeField] private int[] callResponsePhraseLengths = { 2, 4, 6 };

    [Tooltip("Directional: player echoes Left/Center/Right — both rhythm and direction must match. " +
             "SingleDrum: any pad counts, grading is timing-only.")]
    [SerializeField] private CallResponseInputMode callResponseInputMode = CallResponseInputMode.Directional;

    [Header("Flash Card Config")]
    [Tooltip("Note-count range per card: x = min, y = max (beat 1 is always a note). " +
             "(0,0) = use FlashCardController Inspector defaults. " +
             "Only meaningful when RoomType == FlashCard. " +
             "Card count comes from RoomLengthBars — each card spans 2 bars, so author an even value. " +
             "PatternDurations is INERT for FlashCard rooms (DungeonBeatManager is suppressed).")]
    [SerializeField] private Vector2Int flashCardNoteRange = Vector2Int.zero;

    [Header("Input Layout")]
    [Tooltip("Pad rig shown during this room. ThreePad = standard combat (L/C/R buttons). SingleDrum = one wide drum (C&R encounters).")]
    [SerializeField] private InputLayoutType inputLayout = InputLayoutType.ThreePad;

    [Header("BGM Override")]
    [Tooltip("Index into AudioManager.musicTracks. -1 = no override (the floor's BGM plays). >= 0 swaps to this track when the room is entered.")]
    [SerializeField] private int bgmTrackOverride = -1;

    [Tooltip("Tempo of the override track. -1 = no override (the floor's BPM applies). Set alongside BgmTrackOverride when the override track's tempo differs from the floor's, otherwise the beat grid desyncs from the music.")]
    [SerializeField] private int bpmOverride = -1;

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

    /// <summary>Background sprite to display while this room is active. May be null.</summary>
    public Sprite BackgroundSprite => backgroundSprite;

    /// <summary>Visual theme key — reserved for future use.</summary>
    public string ArtOverrideKey => artOverrideKey;

    /// <summary>Room archetype — determines whether combat or direction-choice flow is used.</summary>
    public RoomType RoomType => roomType;

    /// <summary>
    /// Ordered phrase lengths (in beats) for each Call-and-Response round.
    /// Returns null when the array is empty — caller falls back to CallAndResponseController Inspector defaults.
    /// Only meaningful when <see cref="RoomType"/> is <see cref="RoomType.CallAndResponse"/>.
    /// </summary>
    public int[] PhraseLengths => (callResponsePhraseLengths != null && callResponsePhraseLengths.Length > 0)
        ? callResponsePhraseLengths
        : null;

    /// <summary>
    /// Whether the player must echo direction (Left/Center/Right) or any pad counts (timing-only).
    /// Only meaningful when <see cref="RoomType"/> is <see cref="RoomType.CallAndResponse"/>.
    /// </summary>
    public CallResponseInputMode CallResponseMode => callResponseInputMode;

    /// <summary>
    /// Authored note-count range per Flash Card (x = min, y = max).
    /// Only meaningful when <see cref="RoomType"/> is <see cref="RoomType.FlashCard"/>;
    /// check <see cref="HasFlashCardNoteRange"/> before use.
    /// </summary>
    public Vector2Int FlashCardNoteRange => flashCardNoteRange;

    /// <summary>
    /// True when this room authors a Flash Card note-count range.
    /// False = (0,0) sentinel — caller falls back to FlashCardController Inspector defaults.
    /// </summary>
    public bool HasFlashCardNoteRange => flashCardNoteRange.x > 0 && flashCardNoteRange.y > 0;

    /// <summary>Track index this room overrides the floor BGM with, or -1 for no override.</summary>
    public int BgmTrackOverride => bgmTrackOverride;

    /// <summary>
    /// True when this room overrides the floor BGM with <see cref="BgmTrackOverride"/>
    /// at the next bar boundary. False = the floor's BGM plays.
    /// </summary>
    public bool HasBgmOverride => bgmTrackOverride >= 0;

    /// <summary>Tempo the beat grid runs at while this room's override track plays, or -1 for no override.</summary>
    public int BpmOverride => bpmOverride;

    /// <summary>True when this room overrides the floor BPM with <see cref="BpmOverride"/>.</summary>
    public bool HasBpmOverride => bpmOverride > 0;

    /// <summary>
    /// Room length in bars. When non-zero, used instead of clip length to determine
    /// how many beats the combat encounter lasts. Required for rooms that do not
    /// restart the music (no BGM override) so the room ends at the right time.
    /// </summary>
    public int RoomLengthBars => roomLengthBars;

    /// <summary>
    /// Which input rig is displayed during this room.
    /// Passed to DungeonInputLayoutController.SwitchLayout() when the room starts.
    /// </summary>
    public InputLayoutType InputLayout => inputLayout;
}

/// <summary>Enemy difficulty tier for a dungeon room.</summary>
public enum EnemyTier
{
    Standard,
    Elite,
    Boss,
}

/// <summary>
/// Input mode for Call-and-Response encounters.
/// </summary>
public enum CallResponseInputMode
{
    /// <summary>Player must echo both rhythm and direction (Left/Center/Right). Default.</summary>
    Directional,

    /// <summary>Any pad counts — grading is timing-only, direction is ignored.</summary>
    SingleDrum,
}

/// <summary>Which pad rig is displayed during a dungeon room.</summary>
public enum InputLayoutType
{
    /// <summary>Three separate pad buttons (Left / Center / Right). Default for combat rooms.</summary>
    ThreePad,

    /// <summary>One wide single drum. Default for C&R rooms — direction-collapsed via SingleDrumEchoConstant.</summary>
    SingleDrum,

    /// <summary>Bongo-style two-pad rig (Left / Right only). Used for CombatTwoPad rooms.</summary>
    TwoPad,
}

/// <summary>Room archetype — determines the gameplay flow inside the room.</summary>
public enum RoomType
{
    /// <summary>Standard combat encounter: enemy pattern + evaluation cycle.</summary>
    CombatEncounter,

    /// <summary>No combat — player chooses a door (left/right) to advance.</summary>
    DirectionChoice,

    /// <summary>Rhythm call-and-response encounter: enemy presents a beat phrase, player echoes it.</summary>
    CallAndResponse,

    /// <summary>
    /// Two-pad combat encounter: identical flow to CombatEncounter but patterns contain only
    /// Left and Right — no Center. Uses the Bongo-style two-pad input rig.
    /// </summary>
    CombatTwoPad,

    /// <summary>
    /// Flash Card sight-reading encounter: a card of 8 squares reveals a rhythm
    /// silently over one bar, then the player performs it over the next bar.
    /// Driven by FlashCardController; DungeonBeatManager is suppressed.
    /// </summary>
    FlashCard,
}
