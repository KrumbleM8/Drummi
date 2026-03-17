// DrumSoundType.cs
// ----------------------------------------------------------------------------
// A simple enum that names each drum sound.
// Each row in the grid maps to one value here.
// To add a new sound: add an entry above _Count. Nothing else needs changing.
// ----------------------------------------------------------------------------

public enum DrumSoundType
{
    None = -1,
    Kick = 0,
    Snare = 1,
    HiHat = 2,
    Clap = 3,
    _Count
}