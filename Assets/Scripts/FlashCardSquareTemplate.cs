using System;

namespace KrumbleHut.Drummi.Dungeons
{
    /// <summary>
    /// Rhythmic content of one Flash Card square. Every square is exactly one
    /// beat EXCEPT HalfRest (half a beat); no durations cross square boundaries.
    /// Subdivided positions sit at fractional slots on the beat grid (eighths
    /// +0.5, sixteenths +0.25 steps, triplets +1/3 steps).
    ///
    /// DownbeatEighth was removed in Phase 6 — a lone eighth on the downbeat is
    /// performance-identical to a Quarter (one tap at slot+0).
    /// </summary>
    public enum SquareTemplate
    {
        /// <summary>One quarter note on the downbeat.</summary>
        Quarter,

        /// <summary>Two beamed eighth notes: downbeat + offbeat (slot and slot + 0.5).</summary>
        EighthPair,

        /// <summary>Eighth rest on the downbeat, eighth note on the offbeat (slot + 0.5).</summary>
        OffbeatEighth,

        /// <summary>Full beat rest — no notes.</summary>
        Rest,

        /// <summary>Four beamed sixteenth notes: slot, +0.25, +0.5, +0.75.</summary>
        SixteenthQuad,

        /// <summary>Eighth-note triplet: slot, +1/3, +2/3.</summary>
        Triplet,

        /// <summary>
        /// Eighth rest occupying only HALF a beat — no notes. Every slot after it
        /// shifts 0.5 beat earlier, flipping play's phase against the metronome/
        /// BGM. ALWAYS the same gesture: the episode's opener skips play onto the
        /// off-beats, its resolver skips play back on — and the resolver card
        /// carries an appended plain-Rest balancing slot so the episode is
        /// net-zero in time (bar alignment never drifts).
        /// </summary>
        HalfRest,
    }

    /// <summary>
    /// Authorable template vocabulary for Flash Card generation (RoomDefinition /
    /// escalation ladder → FlashCardController). None is the sentinel: caller falls
    /// back to the FlashCardController Inspector default (Quarter | Rest).
    /// Bit 4 (1 &lt;&lt; 2) was DownbeatEighth — RESERVED, never reuse: stale
    /// serialized vocab ints (e.g. old ladder steps, "Everything" = -1 assets) may
    /// still carry it; the generator simply never reads it.
    /// </summary>
    [Flags]
    public enum FlashCardVocab
    {
        None           = 0,
        Quarter        = 1 << 0,
        EighthPair     = 1 << 1,
        // 1 << 2 reserved (was DownbeatEighth — removed Phase 6)
        OffbeatEighth  = 1 << 3,
        Rest           = 1 << 4,
        SixteenthQuad  = 1 << 5,
        Triplet        = 1 << 6,
        HalfRest       = 1 << 7,
    }

    /// <summary>Shared template/vocabulary queries used by the generator and views.</summary>
    public static class SquareTemplateUtil
    {
        /// <summary>True when the template contains at least one note (i.e. not a rest).</summary>
        public static bool IsNoteBearing(this SquareTemplate t)
            => t != SquareTemplate.Rest && t != SquareTemplate.HalfRest;

        /// <summary>
        /// True for the phase-flip rest (HalfRest). Everything that reacts to the
        /// off-beat phase keys on this.
        /// </summary>
        public static bool IsPhaseFlip(this SquareTemplate t)
            => t == SquareTemplate.HalfRest;

        /// <summary>
        /// True when the template has a note ON the downbeat — required for the
        /// slot-0 anchor rule (the card must start with a readable downbeat note).
        /// </summary>
        public static bool HasDownbeatNote(this SquareTemplate t)
            => t == SquareTemplate.Quarter
            || t == SquareTemplate.EighthPair
            || t == SquareTemplate.SixteenthQuad
            || t == SquareTemplate.Triplet;

        /// <summary>
        /// True when the template contains any sub-beat rhythm (eighths, sixteenths,
        /// or triplets). Drives the rest-enforcement tuning guard.
        /// </summary>
        public static bool HasSubdivision(this SquareTemplate t)
            => t == SquareTemplate.EighthPair
            || t == SquareTemplate.OffbeatEighth
            || t == SquareTemplate.SixteenthQuad
            || t == SquareTemplate.Triplet
            || t == SquareTemplate.HalfRest; // half-beat windows need the same guard

        /// <summary>Number of tappable notes in the template (expected taps per square).</summary>
        public static int NoteCount(this SquareTemplate t) => t switch
        {
            SquareTemplate.Quarter       => 1,
            SquareTemplate.EighthPair    => 2,
            SquareTemplate.OffbeatEighth => 1,
            SquareTemplate.SixteenthQuad => 4,
            SquareTemplate.Triplet       => 3,
            _                            => 0,
        };

        /// <summary>Single-char notation for pattern logs: Q / E / O / S / T / h / '.'</summary>
        public static char ToLogChar(this SquareTemplate t) => t switch
        {
            SquareTemplate.Quarter       => 'Q',
            SquareTemplate.EighthPair    => 'E',
            SquareTemplate.OffbeatEighth => 'O',
            SquareTemplate.SixteenthQuad => 'S',
            SquareTemplate.Triplet       => 'T',
            SquareTemplate.HalfRest      => 'h',
            _                            => '.',
        };

        /// <summary>Duration of the square in beats: 0.5 for HalfRest, 1 otherwise.</summary>
        public static float BeatLength(this SquareTemplate t)
            => t == SquareTemplate.HalfRest ? 0.5f : 1f;

        /// <summary>
        /// Cumulative slot start positions in beats: length N+1, entry i is where
        /// slot i begins, the final entry is the card's total length in beats.
        /// With no HalfRest this is simply 0,1,2,…,N.
        /// </summary>
        public static double[] BuildSlotStartBeats(SquareTemplate[] templates)
        {
            var starts = new double[templates.Length + 1];
            double cursor = 0.0;
            for (int i = 0; i < templates.Length; i++)
            {
                starts[i] = cursor;
                cursor   += templates[i].BeatLength();
            }
            starts[templates.Length] = cursor;
            return starts;
        }
    }
}
