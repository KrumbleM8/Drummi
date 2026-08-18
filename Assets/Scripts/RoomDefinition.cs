using UnityEngine;
using KrumbleHut.Drummi.Dungeons;

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
    [Tooltip("Pattern durations passed to DungeonBeatManager — overrides the default { 1f, 0.5f }. Leave empty to use defaults. " +
             "INERT for FlashCard rooms (DungeonBeatManager is suppressed; card content is driven by FlashCardVocabulary/FlashCardNoteRange).")]
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

    [Header("Flash Card Config")]
    [Tooltip("Note-count range per card: x = min, y = max. Counts NOTE-BEARING SQUARES (not taps); " +
             "square 1 always carries a downbeat note. " +
             "(0,0) = use FlashCardController Inspector defaults. " +
             "Only meaningful when RoomType == FlashCard. " +
             "Card count comes from RoomLengthBars — each card spans 2 bars, so author an even value. " +
             "PatternDurations is INERT for FlashCard rooms (DungeonBeatManager is suppressed).")]
    [SerializeField] private Vector2Int flashCardNoteRange = Vector2Int.zero;

    [Tooltip("Square-template vocabulary the card generator draws from. " +
             "None = use FlashCardController Inspector default (Quarter | Rest ≡ quarters-and-rests-only behavior). " +
             "Must include at least one note-bearing template (generator warns and falls back to Quarter otherwise). " +
             "Without Rest, every square is note-bearing. Only meaningful when RoomType == FlashCard.")]
    [SerializeField] private FlashCardVocab flashCardVocabulary = FlashCardVocab.None;

    [Header("Flick Config")]
    [Tooltip("Required release slots within the 8-beat bar (0-based; slot 0 = musical beat 1). " +
             "Empty = use FlickBeatManager scene defaults ({0,2,4,6} — half-time). " +
             "Only meaningful when RoomType == Flick.")]
    [SerializeField] private float[] flickRequiredBeatSlots;

    [Tooltip("Optional off-beat bonus slots within the 8-beat bar. " +
             "Empty = use FlickBeatManager scene defaults ({1,3,5,7}). " +
             "A single NEGATIVE value (e.g. {-1}) = explicitly NO off-beats — needed because the " +
             "core treats an empty runtime array as 'disabled', so empty here must mean 'default'. " +
             "Only meaningful when RoomType == Flick.")]
    [SerializeField] private float[] flickOffBeatSlots;

    [Tooltip("Beats per full sweep crossing (left→right). 0 = use FlickTargetSweep scene default (4). " +
             "Only meaningful when RoomType == Flick.")]
    [SerializeField, Min(0)] private float flickBeatsPerCrossing;

    [Tooltip("Generous hitbox radius around the flick target, in world units. " +
             "0 = use FlickTargetSweep scene default (1.2). Only meaningful when RoomType == Flick.")]
    [SerializeField, Min(0)] private float flickHitboxRadiusWorld;

    [Tooltip("Hit points per flick enemy. 0 = use FlickEnemy scene default (60). " +
             "Only meaningful when RoomType == Flick.")]
    [SerializeField, Min(0)] private int flickEnemyMaxHP;

    [Tooltip("Flat attack strength (in damagePerMiss units) a SURVIVING flick enemy always lands " +
             "at the seam, on top of per-fizzle scaling. -1 = use FlickBeatManager scene default (1). " +
             "0 is meaningful: whiff-on-clean-bar. Only meaningful when RoomType == Flick.")]
    [SerializeField, Min(-1)] private int flickSurvivorAttackUnits = -1;

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

    /// <summary>
    /// Authored square-template vocabulary for Flash Card generation.
    /// <see cref="FlashCardVocab.None"/> sentinel = caller falls back to the
    /// FlashCardController Inspector default (Quarter | Rest).
    /// Only meaningful when <see cref="RoomType"/> is <see cref="RoomType.FlashCard"/>.
    /// </summary>
    public FlashCardVocab FlashCardVocabulary => flashCardVocabulary;

    /// <summary>
    /// Authored required-beat slots for a Flick room, or null (empty sentinel) to use
    /// FlickBeatManager scene defaults. Only meaningful when <see cref="RoomType"/> is
    /// <see cref="RoomType.Flick"/>.
    /// </summary>
    public float[] FlickRequiredBeatSlots => (flickRequiredBeatSlots != null && flickRequiredBeatSlots.Length > 0)
        ? flickRequiredBeatSlots
        : null;

    /// <summary>
    /// Authored off-beat bonus slots for a Flick room, or null (empty sentinel) to use
    /// FlickBeatManager scene defaults. A single negative value means "explicitly no
    /// off-beats" — consumers map it to an empty runtime array. Only meaningful when
    /// <see cref="RoomType"/> is <see cref="RoomType.Flick"/>.
    /// </summary>
    public float[] FlickOffBeatSlots => (flickOffBeatSlots != null && flickOffBeatSlots.Length > 0)
        ? flickOffBeatSlots
        : null;

    /// <summary>Beats per sweep crossing for a Flick room. 0 = use FlickTargetSweep scene default.</summary>
    public float FlickBeatsPerCrossing => flickBeatsPerCrossing;

    /// <summary>Flick hitbox radius in world units. 0 = use FlickTargetSweep scene default.</summary>
    public float FlickHitboxRadiusWorld => flickHitboxRadiusWorld;

    /// <summary>Flick enemy hit points. 0 = use FlickEnemy scene default.</summary>
    public int FlickEnemyMaxHP => flickEnemyMaxHP;

    /// <summary>
    /// Survivor seam-attack units for a Flick room. -1 = use FlickBeatManager scene
    /// default; 0 is meaningful (whiff-on-clean-bar).
    /// </summary>
    public int FlickSurvivorAttackUnits => flickSurvivorAttackUnits;

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

/// <summary>Which pad rig is displayed during a dungeon room.</summary>
public enum InputLayoutType
{
    /// <summary>Three separate pad buttons (Left / Center / Right). Default for combat rooms.</summary>
    ThreePad,

    /// <summary>One wide single drum. Default for C&R rooms — direction-collapsed via SingleDrumEchoConstant.</summary>
    SingleDrum,

    /// <summary>Bongo-style two-pad rig (Left / Right only). Used for CombatTwoPad rooms.</summary>
    TwoPad,

    /// <summary>
    /// No pad rig at all — pads collapse and DrumPadTouch is disabled so the flick
    /// gesture reader owns the whole screen. Used for Flick rooms.
    /// </summary>
    Flick,
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

    /// <summary>
    /// Flick encounter: the shared flick core (FlickBeatManager + FlickEncounterDirector)
    /// hosts a per-bar enemy the player flicks a ball at on required beats.
    /// DungeonBeatManager is suppressed; room length comes from RoomLengthBars.
    /// </summary>
    Flick,
}
