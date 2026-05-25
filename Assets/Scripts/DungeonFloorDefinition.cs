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

    [Header("Rooms")]
    [Tooltip("Ordered list of rooms. DungeonRunner steps through these in sequence.")]
    [SerializeField] private List<RoomDefinition> rooms = new List<RoomDefinition>();

    // ── Accessors ─────────────────────────────────────────────────────────────

    /// <summary>Display name for this floor.</summary>
    public string FloorName => floorName;

    /// <summary>Zero-based floor index used by DungeonRunner.</summary>
    public int FloorIndex => floorIndex;

    /// <summary>Ordered room sequence for this floor.</summary>
    public IReadOnlyList<RoomDefinition> Rooms => rooms;
}
