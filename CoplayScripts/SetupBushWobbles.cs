using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine.Events;
using System.Text;

public static class SetupBushWobbles
{
    public static string Execute()
    {
        var renames = new (string from, string to)[]
        {
            ("TreeA", "BushD"), ("TreeB", "BushE"),
            ("HillPropA", "BushF"), ("HillPropB", "BushG"), ("HillPropC", "BushH"),
        };
        var sb = new StringBuilder();

        var pivot = GameObject.Find("PlanetPivot");
        if (pivot == null) return "PlanetPivot NOT FOUND";

        foreach (var (from, to) in renames)
        {
            var t = pivot.transform.Find(from);
            if (t == null) { sb.AppendLine(from + ": NOT FOUND"); continue; }
            t.name = to;
            sb.AppendLine(from + " -> " + to);
        }

        foreach (string name in new[] { "BushA", "BushB", "BushC", "BushD", "BushE", "BushF", "BushG", "BushH" })
        {
            var t = pivot.transform.Find(name);
            if (t == null) { sb.AppendLine(name + ": NOT FOUND"); continue; }
            var go = t.gameObject;

            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null) { sb.AppendLine(name + ": no sprite, skipped"); continue; }

            // Old round collider no longer matches the wide bush sprite.
            var sphere = go.GetComponent<SphereCollider>();
            if (sphere != null) Object.DestroyImmediate(sphere);

            var box = go.GetComponent<BoxCollider>();
            if (box == null) box = go.AddComponent<BoxCollider>();
            box.center = sr.sprite.bounds.center;
            box.size   = sr.sprite.bounds.size;

            var effect = go.GetComponent<TreeAnimationEffect>();
            if (effect == null) effect = go.AddComponent<TreeAnimationEffect>();
            effect.shakeStrength = 0.07f;

            var invoker = go.GetComponent<TapInvoker3D>();
            if (invoker == null) invoker = go.AddComponent<TapInvoker3D>();
            if (invoker.OnTapped == null) invoker.OnTapped = new UnityEvent();

            while (invoker.OnTapped.GetPersistentEventCount() > 0)
                UnityEventTools.RemovePersistentListener(invoker.OnTapped, 0);
            UnityEventTools.AddVoidPersistentListener(invoker.OnTapped, effect.TriggerEffect);

            var so = new SerializedObject(invoker);
            var calls = so.FindProperty("OnTapped.m_PersistentCalls.m_Calls");
            if (calls != null && calls.arraySize > 0)
            {
                calls.GetArrayElementAtIndex(0).FindPropertyRelative("m_CallState").intValue =
                    (int)UnityEventCallState.RuntimeOnly;
                so.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(go);
            sb.AppendLine($"{name}: box={box.size} listeners={invoker.OnTapped.GetPersistentEventCount()}");
        }
        return sb.ToString();
    }
}
