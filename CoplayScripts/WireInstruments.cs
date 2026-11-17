using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using KrumbleHut.Drummi.FlashCards;

/// <summary>
/// Instrument skins wiring for FlashCardsMenu:
///  - Creates 3 FlashCardInstrument assets (Bongo stock, Pop, Woodblock placeholders).
///  - Clones the inactive OverlayCanvas/BackButton into an active InstrumentButton
///    (persistent onClick -> FlashCardsModeController.CycleInstrument).
///  - Wires instruments array, input reader, Drum image, and label on the mode controller.
/// Saves the scene.
/// </summary>
public static class WireInstruments
{
    public static void Execute()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "FlashCardsMenu") { Debug.Log($"[Instr] ABORT: active scene '{scene.name}'."); return; }

        var mode = Object.FindFirstObjectByType<FlashCardsModeController>(FindObjectsInactive.Include);
        if (mode == null) { Debug.Log("[Instr] ABORT: FlashCardsModeController not found."); return; }

        // ── Assets ──
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects/FlashCards/Instruments"))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects/FlashCards", "Instruments");

        var pop   = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/CheekPop.mp3");
        var click = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/click.wav");
        if (pop == null || click == null) { Debug.Log("[Instr] ABORT: placeholder clips not found."); return; }

        var bongoInst = GetOrCreate("Bongo",     null,  new Color(0.85f, 0.68f, 0.42f));
        var popInst   = GetOrCreate("Pop",       pop,   new Color(0.95f, 0.55f, 0.65f));
        var woodInst  = GetOrCreate("Woodblock", click, new Color(0.72f, 0.52f, 0.33f));

        // ── Menu button (clone of the retired BackButton for matching style) ──
        var instBtnGo = GameObject.Find("InstrumentButton");
        TMP_Text label;
        if (instBtnGo == null)
        {
            var back = FindInactive("OverlayCanvas/BackButton");
            if (back == null) { Debug.Log("[Instr] ABORT: OverlayCanvas/BackButton not found."); return; }

            instBtnGo = Object.Instantiate(back, back.transform.parent);
            instBtnGo.name = "InstrumentButton";
            instBtnGo.SetActive(true);
            var rt     = (RectTransform)instBtnGo.transform;
            var backRt = (RectTransform)back.transform;
            rt.anchoredPosition = backRt.anchoredPosition - new Vector2(0f, backRt.sizeDelta.y + 24f);

            var btn = instBtnGo.GetComponent<Button>();
            if (btn == null) { Debug.Log("[Instr] ABORT: clone has no Button."); return; }
            var so = new SerializedObject(btn);
            so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls").arraySize = 0; // strip inherited calls
            so.ApplyModifiedProperties();
            UnityEventTools.AddVoidPersistentListener(btn.onClick, mode.CycleInstrument);
        }
        label = instBtnGo.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = "Bongo";

        // ── Mode controller fields ──
        var reader = Object.FindFirstObjectByType<DungeonInputReader>(FindObjectsInactive.Include);
        var drumGo = FindInactive("FlashCardsCanvas/GameplayUI/Drum");
        var drumImg = drumGo != null ? drumGo.GetComponent<Image>() : null;

        var mso = new SerializedObject(mode);
        var arr = mso.FindProperty("instruments");
        arr.arraySize = 3;
        arr.GetArrayElementAtIndex(0).objectReferenceValue = bongoInst;
        arr.GetArrayElementAtIndex(1).objectReferenceValue = popInst;
        arr.GetArrayElementAtIndex(2).objectReferenceValue = woodInst;
        mso.FindProperty("instrumentInputReader").objectReferenceValue = reader;
        mso.FindProperty("drumButtonImage").objectReferenceValue       = drumImg;
        mso.FindProperty("instrumentLabelText").objectReferenceValue   = label;
        mso.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[Instr] Wired: 3 instruments, reader={(reader ? reader.name : "NULL")}, " +
                  $"drumImage={(drumImg ? "ok" : "NULL")}, label={(label ? "ok" : "NULL")}, " +
                  $"button at {((RectTransform)instBtnGo.transform).anchoredPosition}. Saved={saved}.");
    }

    private static FlashCardInstrument GetOrCreate(string name, AudioClip clip, Color tint)
    {
        string path = $"Assets/ScriptableObjects/FlashCards/Instruments/Instrument_{name}.asset";
        var inst = AssetDatabase.LoadAssetAtPath<FlashCardInstrument>(path);
        if (inst == null)
        {
            inst = ScriptableObject.CreateInstance<FlashCardInstrument>();
            AssetDatabase.CreateAsset(inst, path);
        }
        inst.displayName = name;
        inst.tapClip     = clip;
        inst.buttonTint  = tint;
        EditorUtility.SetDirty(inst);
        AssetDatabase.SaveAssets();
        return inst;
    }

    /// <summary>GameObject.Find skips inactive objects — walk the path explicitly instead.</summary>
    private static GameObject FindInactive(string path)
    {
        string[] parts = path.Split('/');
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name != parts[0]) continue;
            Transform t = root.transform;
            for (int i = 1; i < parts.Length && t != null; i++) t = t.Find(parts[i]);
            if (t != null) return t.gameObject;
        }
        return null;
    }
}
