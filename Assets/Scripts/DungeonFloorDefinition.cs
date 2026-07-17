using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines a single dungeon floor as an ordered sequence of rooms.
/// DungeonRunner steps through <see cref="Rooms"/> in index order.
/// By convention the final room should be a Boss tier, but this is not enforced in code.
/// </summary>
[CreateAssetMenu(fileName = "NewFloor", menuName = "Drummi Dungeons/Floor Definition")]
public class DungeonFloorDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Display name for this floor.")]
    [SerializeField] private string floorName;

    [Tooltip("Zero-based index used by DungeonRunner to order floors.")]
    [SerializeField] private int floorIndex;

    [Header("Music")]
    [Tooltip("Index into AudioManager.musicTracks played for this floor. Rooms may override via RoomDefinition.BgmTrackOverride.")]
    [SerializeField] private int bgmTrackIndex;

    [Tooltip("Tempo of this floor's BGM track. The dungeon beat grid runs at this BPM while the floor's track plays. 0 = keep the metronome's current BPM.")]
    [SerializeField] private int bpm;

    [Header("Visuals")]
    [Tooltip("Default background sprite used for all rooms on this floor. Overridden per-room by RoomDefinition.BackgroundSprite when non-null.")]
    [SerializeField] private Sprite backgroundSprite;

    [Header("Rooms")]
    [Tooltip("Ordered list of rooms. DungeonRunner steps through these in sequence.")]
    [SerializeField] private List<RoomDefinition> rooms = new List<RoomDefinition>();

    // ── Accessors ─────────────────────────────────────────────────────────────

    /// <summary>Display name for this floor.</summary>
    public string FloorName => floorName;

    /// <summary>Zero-based floor index used by DungeonRunner.</summary>
    public int FloorIndex => floorIndex;

    /// <summary>AudioManager.musicTracks index played for this floor unless a room overrides it.</summary>
    public int BgmTrackIndex => bgmTrackIndex;

    /// <summary>Tempo of this floor's BGM track. 0 = keep the metronome's current BPM.</summary>
    public int Bpm => bpm;

    /// <summary>Default background sprite for this floor. Individual rooms may override this.</summary>
    public Sprite BackgroundSprite => backgroundSprite;

    /// <summary>Ordered room sequence for this floor.</summary>
    public IReadOnlyList<RoomDefinition> Rooms => rooms;
}
