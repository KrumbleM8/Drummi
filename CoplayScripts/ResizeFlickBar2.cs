using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using KrumbleHut.Drummi.Dungeons;

public static class ResizeFlickBar2
{
    public static string Execute()
    {
        var sweep = Object.FindObjectOfType<FlickTargetSweep>(true);
        if (sweep == null) return "ERROR: no FlickTargetSweep";
        var barRoot = sweep.transform.Find("EnemyHealthBar");
        if (barRoot == null) return "ERROR: no EnemyHealthBar";

        var back = barRoot.Find("BarBack");
        var fillPivot = barRoot.Find("FillPivot");
        var fill = fillPivot != null ? fillPivot.Find("BarFill") : null;
        if (back == null || fillPivot == null || fill == null) return "ERROR: bar children missing";

        var backSr = back.GetComponent<SpriteRenderer>();
        var fillSr = fill.GetComponent<SpriteRenderer>();
        if (backSr == null || fillSr == null || backSr.sprite == null)
            return "ERROR: missing SpriteRenderer/sprite";

        // Desired WORLD size — enemy sprite is 1.6 wide, bar slightly wider and chunky.
        const float width = 2.4f;
        const float backH = 0.45f;
        const float fillH = 0.36f;
        float half = width / 2f;

        Vector2 backUnit = backSr.sprite.bounds.size;   // world units at scale 1
        Vector2 fillUnit = fillSr.sprite.bounds.size;

        barRoot.localPosition = new Vector3(0f, 1.5f, 0f);
        back.localScale = new Vector3(width / backUnit.x, backH / backUnit.y, 1f);
        fillPivot.localPosition = new Vector3(-half, 0f, 0f);
        fillPivot.localScale = Vector3.one;
        fill.localPosition = new Vector3(half, 0f, 0f);
        fill.localScale = new Vector3(width / fillUnit.x, fillH / fillUnit.y, 1f);

        EditorSceneManager.MarkAllScenesDirty();
        bool saved = EditorSceneManager.SaveOpenScenes();
        return $"OK — sprite '{backSr.sprite.name}' unit size {backUnit}, world size now {width}x{backH}, saved={saved}";
    }
}
