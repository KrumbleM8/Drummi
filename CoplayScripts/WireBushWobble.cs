using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine.Events;
using System.Text;

public static class WireBushWobble
{
    public static string Execute()
    {
        var sb = new StringBuilder();
        foreach (string name in new[] { "BushA", "BushB", "BushC" })
        {
            var go = GameObject.Find(name);
            if (go == null) { sb.AppendLine(name + ": NOT FOUND"); continue; }

            var invoker = go.GetComponent<TapInvoker3D>();
            var effect  = go.GetComponent<TreeAnimationEffect>();
            if (invoker == null || effect == null) { sb.AppendLine(name + ": missing component"); continue; }

            if (invoker.OnTapped == null)
            {
                invoker.OnTapped = new UnityEvent();
                sb.AppendLine(name + ": OnTapped was null, created");
            }

            // Clear any previous wiring so re-runs stay idempotent.
            while (invoker.OnTapped.GetPersistentEventCount() > 0)
                UnityEventTools.RemovePersistentListener(invoker.OnTapped, 0);

            UnityEventTools.AddVoidPersistentListener(invoker.OnTapped, effect.TriggerEffect);

            // Match the Tree prefab: listener call state = RuntimeOnly.
            var so = new SerializedObject(invoker);
            var calls = so.FindProperty("OnTapped.m_PersistentCalls.m_Calls");
            if (calls == null || calls.arraySize == 0)
            {
                sb.AppendLine(name + ": WARNING serialized calls not found (calls=" +
                              (calls == null ? "null" : calls.arraySize.ToString()) + ")");
            }
            else
            {
                calls.GetArrayElementAtIndex(0).FindPropertyRelative("m_CallState").intValue =
                    (int)UnityEventCallState.RuntimeOnly;
                so.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(invoker);
            sb.AppendLine(name + ": wired OnTapped -> TreeAnimationEffect.TriggerEffect " +
                          "(listeners=" + invoker.OnTapped.GetPersistentEventCount() + ")");
        }
        return sb.ToString();
    }
}
