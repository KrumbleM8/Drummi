using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using KrumbleHut.Drummi.Dungeons;

public static class ResizeFlickBar
{
    public static string Execute()
    {
        var sweep = Object.FindObjectOfType<FlickTargetSweep>(true);
        if (sweep == null) return "ERROR: no FlickTargetSweep";
        var barRoot = sweep.transform.Find("EnemyHealthBar");
        if (barRoot == null) return "ERROR: no EnemyHealthBar";

        const float width = 2.4f;   // was 1.6
        const float backH = 0.4f;   // was 0.18
        const float fillH = 0.32f;  // was 0.14
        float half = width / 2f;

        barRoot.localPosition = new Vector3(0f, 1.5f, 0f);

        var back = barRoot.Find("BarBack");
        var fillPivot = barRoot.Find("FillPivot");
        var fill = fillPivot != null ? fillPivot.Find("BarFill") : null;
        if (back == null || fillPivot == null || fill == null) return "ERROR: bar children missing";

        back.localScale = new Vector3(width, backH, 1f);
        fillPivot.localPosition = new Vector3(-half, 0f, 0f);
        fillPivot.localScale = Vector3.one;
        fill.localPosition = new Vector3(half, 0f, 0f);
        fill.localScale = new Vector3(width, fillH, 1f);

        EditorSceneManager.MarkAllScenesDirty();
        bool saved = EditorSceneManager.SaveOpenScenes();
        return $"OK — bar resized to {width}x{backH} at y=1.5, saved={saved}";
    }
}
