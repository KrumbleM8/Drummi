using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Switches the Game view to a fixed resolution via UnityEditor internals (reflection).
///   Execute16x9  — 1080x1920 (short phone)
///   ExecuteTall  — 1080x2340 (19.5:9 phone)
/// Adds the size to the current group if missing, then selects it.
/// </summary>
public static class SetGameViewSize
{
    public static string Execute16x9() => Apply(1080, 1920, "Coplay 1080x1920");
    public static string ExecuteTall() => Apply(1080, 2340, "Coplay 1080x2340");

    private static string Apply(int width, int height, string label)
    {
        try
        {
            var asm = typeof(Editor).Assembly;
            var sizesType = asm.GetType("UnityEditor.GameViewSizes");
            var singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var instance = singletonType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            var currentGroup = sizesType.GetProperty("currentGroup", BindingFlags.Public | BindingFlags.Instance).GetValue(instance);
            var groupType = currentGroup.GetType();

            int total = (int)groupType.GetMethod("GetTotalCount").Invoke(currentGroup, null);
            int foundIndex = -1;
            for (int i = 0; i < total; i++)
            {
                var size = groupType.GetMethod("GetGameViewSize").Invoke(currentGroup, new object[] { i });
                int w = (int)size.GetType().GetProperty("width").GetValue(size);
                int h = (int)size.GetType().GetProperty("height").GetValue(size);
                if (w == width && h == height) { foundIndex = i; break; }
            }

            if (foundIndex < 0)
            {
                var sizeType = asm.GetType("UnityEditor.GameViewSize");
                var gvSizeTypeEnum = asm.GetType("UnityEditor.GameViewSizeType");
                var ctor = sizeType.GetConstructor(new[] { gvSizeTypeEnum, typeof(int), typeof(int), typeof(string) });
                var newSize = ctor.Invoke(new object[] { Enum.ToObject(gvSizeTypeEnum, 1 /*FixedResolution*/), width, height, label });
                groupType.GetMethod("AddCustomSize").Invoke(currentGroup, new[] { newSize });
                foundIndex = (int)groupType.GetMethod("GetTotalCount").Invoke(currentGroup, null) - 1;
            }

            var gameViewType = asm.GetType("UnityEditor.GameView");
            var window = EditorWindow.GetWindow(gameViewType);
            var callback = gameViewType.GetMethod("SizeSelectionCallback",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            callback.Invoke(window, new object[] { foundIndex, null });
            window.Repaint();
            return $"OK: Game view set to {width}x{height} (index {foundIndex})";
        }
        catch (Exception e)
        {
            return $"FAIL: {e.GetType().Name}: {e.Message}";
        }
    }
}
