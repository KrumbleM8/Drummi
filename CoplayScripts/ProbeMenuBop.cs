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
            sb.AppendLine($"clock running={clock.IsRunning} beat={clock.BeatIndex} phase={clock.BeatPhase:F2} interval={clock.BeatInterval:F3} " +
                          $"src.isPlaying={(src != null && src.isPlaying)} src.time={(src != null ? src.time : -1f):F2} clip={(src != null && src.clip != null ? src.clip.name : "null")}");
        }

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
