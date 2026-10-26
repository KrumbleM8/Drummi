using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

/// <summary>PLAY MODE: jump the menu music to 2 s before the clip end to exercise the loop + grid re-anchor.</summary>
public static class SeekMenuMusicNearEnd
{
    public static string Execute()
    {
        var clock = MenuBeatClock.Instance;
        if (clock == null) return "no clock";
        var src = clock.GetComponent<AudioSource>();
        if (src == null || src.clip == null) return "no source/clip";
        src.timeSamples = Mathf.Max(0, src.clip.samples - 2 * src.clip.frequency);
        return $"seeked to {src.time:F2}s of {src.clip.length:F2}s (loop={src.loop})";
    }
}
