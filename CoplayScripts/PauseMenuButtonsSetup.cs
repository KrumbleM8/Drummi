using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using KrumbleHut.Drummi.UI;

public static class PauseMenuButtonsSetup
{
    private const string PrefabPath = "Assets/Prefabs/OverlayUtilCanvas.prefab";

    // ── Dump hierarchy for inspection ─────────────────────────────────────────
    public static void Dump()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var sb = new StringBuilder();
            DumpRecursive(root.transform, 0, sb);
            Debug.Log("[PauseMenuButtonsSetup] Prefab hierarchy:\n" + sb);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void DumpRecursive(Transform t, int depth, StringBuilder sb)
    {
        sb.Append(new string(' ', depth * 2)).Append(t.name);
        var rt = t as RectTransform;
        if (rt != null)
            sb.Append($"  pos={rt.anchoredPosition} size={rt.rect.size}");
        foreach (var c in t.GetComponents<Component>())
            sb.Append("  [").Append(c == null ? "MISSING" : c.GetType().Name).Append("]");
        var tmp = t.GetComponent<TMP_Text>();
        if (tmp != null) sb.Append($"  text=\"{tmp.text}\"");
        sb.AppendLine();
        for (int i = 0; i < t.childCount; i++)
            DumpRecursive(t.GetChild(i), depth + 1, sb);
    }

    // ── Apply the edit ────────────────────────────────────────────────────────
    public static void Execute()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var controller = root.GetComponentInChildren<OverlayUIController>(true);
            if (controller == null) { Debug.LogError("[PauseMenuButtonsSetup] OverlayUIController not found"); return; }

            Transform mainMenu = FindDeep(root.transform, "MainMenu");
            if (mainMenu == null) { Debug.LogError("[PauseMenuButtonsSetup] MainMenu button not found"); return; }

            // 1. Relabel MainMenu → Home and rewire its onClick.
            foreach (var txt in mainMenu.GetComponentsInChildren<TMP_Text>(true))
                txt.text = "Home";
            RewireButton(mainMenu.GetComponent<Button>(), controller.GoToHomeScene);

            // 2. Duplicate as Restart button (idempotent).
            Transform existing = FindDeep(root.transform, "Restart");
            GameObject restartGO;
            if (existing != null)
            {
                restartGO = existing.gameObject;
            }
            else
            {
                restartGO = Object.Instantiate(mainMenu.gameObject, mainMenu.parent);
                restartGO.name = "Restart";
                restartGO.transform.SetSiblingIndex(mainMenu.GetSiblingIndex() + 1);
            }

            var mainRt = (RectTransform)mainMenu;
            var restRt = (RectTransform)restartGO.transform;
            restRt.anchoredPosition = new Vector2(mainRt.anchoredPosition.x,
                mainRt.anchoredPosition.y - mainRt.rect.height - 20f);
            foreach (var txt in restartGO.GetComponentsInChildren<TMP_Text>(true))
                txt.text = "Restart";
            RewireButton(restartGO.GetComponent<Button>(), controller.RestartScene);

            // 3. Refresh stale SettingsIcon EventTrigger listeners (ToggleSettings).
            Transform settingsIcon = FindDeep(root.transform, "SettingsIcon");
            var trigger = settingsIcon != null ? settingsIcon.GetComponent<UnityEngine.EventSystems.EventTrigger>() : null;
            if (trigger != null)
            {
                foreach (var entry in trigger.triggers)
                {
                    for (int i = entry.callback.GetPersistentEventCount() - 1; i >= 0; i--)
                    {
                        if (entry.callback.GetPersistentMethodName(i) == "ToggleSettings")
                        {
                            UnityEventTools.RemovePersistentListener(entry.callback, i);
                            UnityEventTools.AddVoidPersistentListener(entry.callback, controller.ToggleSettings);
                        }
                    }
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[PauseMenuButtonsSetup] Prefab updated: Home + Restart buttons wired.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void RewireButton(Button btn, UnityEngine.Events.UnityAction action)
    {
        if (btn == null) { Debug.LogError("[PauseMenuButtonsSetup] Button component missing"); return; }
        for (int i = btn.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(btn.onClick, i);
        UnityEventTools.AddPersistentListener(btn.onClick, action);
    }

    private static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var r = FindDeep(t.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }
}
