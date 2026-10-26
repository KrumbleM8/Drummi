using System.Text;
using UnityEngine;
using KrumbleHut.Drummi.FlashCards;

/// <summary>PLAY MODE probe: menu beat clock + bopper state snapshot.</summary>
public static class ProbeMenuBop
{
    public static string Execute()
    {
        var sb = new StringBuilder();
        var clock = MenuBeatClock.Instance;
        if (clock == null) sb.AppendLine("MenuBeatClock.Instance = null");
        else
        {
            var src = clock.GetComponent<AudioSource>();
            if (src != null && src.clip != null)
            {
                // Where the AUDIO says the beat is vs where the grid says it is.
                double audioBeats = (double)src.timeSamples / src.clip.frequency / clock.BeatInterval;
                double gridBeats  = clock.BeatPosition;
                double err = (gridBeats - audioBeats) - System.Math.Round(gridBeats - audioBeats);
                sb.AppendLine($"grid-vs-audio: audioPhase={audioBeats - System.Math.Floor(audioBeats):F3} gridPhase={clock.BeatPhase:F3} " +
                              $"err={err * clock.BeatInterval * 1000.0:+0;-0} ms  loop={src.loop} clipLen={src.clip.length:F2}s beatsPerLoop={src.clip.length / clock.BeatInterval:F3}");
            }
            sb.AppendLine($"clock running={clock.IsRunning} beat={clock.BeatIndex} phase={clock.BeatPhase:F2} interval={clock.BeatInterval:F3} " +
                          $"volumeScale={clock.VolumeScale:F2} src.volume={(src != null ? src.volume : -1f):F2} " +
                          $"src.isPlaying={(src != null && src.isPlaying)} src.time={(src != null ? src.time : -1f):F2} clip={(src != null && src.clip != null ? src.clip.name : "null")}");
        }

        var idle = Object.FindFirstObjectByType<PlanetIdleMotion>(FindObjectsInactive.Include);
        if (idle != null)
            sb.AppendLine($"PlanetIdleMotion enabled={idle.enabled} weight={idle.Weight:F2} beatWeight={idle.BeatWeight:F2} " +
                          $"pos={idle.transform.localPosition} scale={idle.transform.localScale}");

        foreach (var b in Object.FindObjectsByType<MenuBeatBopper>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var t = b.transform;
            sb.AppendLine($"{t.parent?.name}/{t.name}: active={b.isActiveAndEnabled} base={b.BaseScale} scale={t.localScale} pulse={b.Pulse:F2} mult={b.ScaleMultiplier:F3}");
        }

        string s = sb.ToString();
        Debug.Log("[ProbeMenuBop]\n" + s);
        return s;
    }
}
