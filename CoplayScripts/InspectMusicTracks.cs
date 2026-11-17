using System.Text;
using UnityEngine;

public static class InspectMusicTracks
{
    public static string Execute()
    {
        var am = Object.FindFirstObjectByType<AudioManager>(FindObjectsInactive.Include);
        if (am == null) return "ERROR: AudioManager not found in scene.";

        var sb = new StringBuilder();
        sb.AppendLine($"musicTracks ({am.musicTracks.Length}):");
        for (int i = 0; i < am.musicTracks.Length; i++)
        {
            var clip = am.musicTracks[i];
            sb.AppendLine(clip == null
                ? $"  [{i}] <null>"
                : $"  [{i}] {clip.name} — {clip.length:F2}s, {clip.samples} samples @ {clip.frequency}Hz");
        }
        return sb.ToString();
    }
}
