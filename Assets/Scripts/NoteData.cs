using UnityEngine;

/// <summary>
/// A single note entry in a SongChart.
/// Beat values are 0-indexed and support fractions for subdivisions.
///
/// Examples (4/4 at beat resolution 0.25):
///   beat 0.0  = beat 1
///   beat 0.5  = beat 1 and a half (quaver offset)
///   beat 0.25 = beat 1 semiquaver offset
///   beat 1.0  = beat 2
/// </summary>
[System.Serializable]
public struct NoteData
{
    [Tooltip("Beat number (0-indexed). Fractions = subdivisions. E.g. 0.5 = half a beat after beat 1.")]
    public float beat;

    [Tooltip("Which lane this note targets.")]
    public Lane lane;
}
