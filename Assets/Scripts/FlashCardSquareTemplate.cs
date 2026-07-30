using System;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>
    /// Rhythmic content of one Flash Card square. A square is always exactly one
    /// beat; no durations cross square boundaries. Eighth positions sit at
    /// slot + 0.5 on the beat grid.
    /// </summary>
    public enum SquareTemplate
    {
        /// <summary>One quarter note on the downbeat.</summary>
        Quarter,

        /// <summary>Two beamed eighth notes: downbeat + offbeat (slot and slot + 0.5).</summary>
        EighthPair,

        /// <summary>Eighth note on the downbeat, eighth rest on the offbeat.</summary>
        DownbeatEighth,

        /// <summary>Eighth rest on the downbeat, eighth note on the offbeat (slot + 0.5).</summary>
        OffbeatEighth,

        /// <summary>Full beat rest — no notes.</summary>
        Rest,
    }

    /// <summary>
    /// Authorable template vocabulary for Flash Card generation (RoomDefinition →
    /// FlashCardController). None is the sentinel: caller falls back to the
    /// FlashCardController Inspector default (Quarter | Rest ≡ pre-eighths behavior).
    /// </summary>
    [Flags]
    public enum FlashCardVocab
    {
        None           = 0,
        Quarter        = 1 << 0,
        EighthPair     = 1 << 1,
        DownbeatEighth = 1 << 2,
        OffbeatEighth  = 1 << 3,
        Rest           = 1 << 4,
    }

    /// <summary>Shared template/vocabulary queries used by the generator and views.</summary>
    public static class SquareTemplateUtil
    {
        /// <summary>True when the template contains at least one note (i.e. not a full rest).</summary>
        public static bool IsNoteBearing(this SquareTemplate t) => t != SquareTemplate.Rest;

        /// <summary>
        /// True when the template has a note ON the downbeat — required for the
        /// slot-0 anchor rule (the card must start with a readable downbeat note).
        /// </summary>
        public static bool HasDownbeatNote(this SquareTemplate t)
            => t == SquareTemplate.Quarter
            || t == SquareTemplate.EighthPair
            || t == SquareTemplate.DownbeatEighth;

        /// <summary>True when the template contains any half-beat (eighth) content.</summary>
        public static bool HasEighths(this SquareTemplate t)
            => t == SquareTemplate.EighthPair
            || t == SquareTemplate.DownbeatEighth
            || t == SquareTemplate.OffbeatEighth;

        /// <summary>Single-char notation for pattern logs: Q / E / D / O / '.'</summary>
        public static char ToLogChar(this SquareTemplate t) => t switch
        {
            SquareTemplate.Quarter        => 'Q',
            SquareTemplate.EighthPair     => 'E',
            SquareTemplate.DownbeatEighth => 'D',
            SquareTemplate.OffbeatEighth  => 'O',
            _                             => '.',
        };
    }
}
