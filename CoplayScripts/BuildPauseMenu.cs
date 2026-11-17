using System.Collections.Generic;
using KrumbleHut.Drummi.Pulse;
using KrumbleHut.Drummi.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.UI;

// Rebuilds the shared pause menu (OverlayUtilCanvas prefab) as a hand-drawn paper
// sheet with Resume / Restart / Home, Music + SFX sliders and a Vibration toggle,
// routes every AudioSource through Assets/Audio/Main.mixer, and adds the overlay
// to the Pulse scene. Edit-mode script: run via Coplay execute_script.
public static class BuildPauseMenu
{
    const string OverlayPath = "Assets/Prefabs/OverlayUtilCanvas.prefab";
    const string AudioManagerPath = "Assets/Prefabs/AudioManager.prefab";
    const string MixerPath = "Assets/Audio/Main.mixer";
    const string PaperPath = "Assets/Images/Generated/BongoReportPaper.png";

    static readonly Color Ink = new Color(0.22f, 0.16f, 0.11f, 1f);
    static readonly Color FaintInk = new Color(0.22f, 0.16f, 0.11f, 0.35f);
    static readonly Color Brown = new Color(0.61f, 0.42f, 0.36f, 1f);
    static readonly Color Cream = new Color(0.98f, 0.95f, 0.88f, 1f);

    static Sprite UISprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    static Sprite BgSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
    static Sprite Knob => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
    static Sprite Check => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");

    public static void Execute()
    {
        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        if (mixer == null) { Debug.LogError("[BuildPauseMenu] mixer not found at " + MixerPath); return; }
        var music = mixer.FindMatchingGroups("Music");
        var sfx = mixer.FindMatchingGroups("SFX");
        Debug.Log($"[BuildPauseMenu] mixer groups Music={music.Length} SFX={sfx.Length} setMusic={mixer.SetFloat("MusicVolume", 0f)} setSfx={mixer.SetFloat("SFXVolume", 0f)}");
        if (music.Length != 1 || sfx.Length != 1) { Debug.LogError("[BuildPauseMenu] mixer groups missing"); return; }

        BuildOverlayPrefab(mixer);
        RouteAudioManagerPrefab(music[0], sfx[0]);
        WireScenes(music[0], sfx[0]);
        Debug.Log("[BuildPauseMenu] DONE");
    }

    // ── Overlay prefab ──────────────────────────────────────────────────────

    static void BuildOverlayPrefab(AudioMixer mixer)
    {
        var root = PrefabUtility.LoadPrefabContents(OverlayPath);
        var ctrl = root.GetComponent<OverlayUIController>();
        var menu = root.transform.Find("SettingsMenu");
        var font = TMP_Settings.defaultFontAsset;

        // Keep the dim background, rebuild everything else on a paper sheet.
        for (int i = menu.childCount - 1; i >= 0; i--)
        {
            var c = menu.GetChild(i);
            if (c.name != "Background") Object.DestroyImmediate(c.gameObject);
        }
        menu.gameObject.SetActive(true);

        var paper = Rect("Paper", menu, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(660f, 980f));
        var paperImg = paper.gameObject.AddComponent<Image>();
        paperImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PaperPath);
        paperImg.color = Color.white;
        paper.localRotation = Quaternion.Euler(0f, 0f, -2f);

        Label("Title", paper, "Paused", 96f, new Vector2(0f, 400f), new Vector2(600f, 130f), TextAlignmentOptions.Center, font);

        var resume = Button("Resume", paper, "Resume", new Vector2(0f, 255f), font);
        var restart = Button("Restart", paper, "Restart", new Vector2(0f, 125f), font);
        var home = Button("Home", paper, "Home", new Vector2(0f, -5f), font);
        UnityEventTools.AddPersistentListener(resume.onClick, ctrl.Resume);
        UnityEventTools.AddPersistentListener(restart.onClick, ctrl.RestartScene);
        UnityEventTools.AddPersistentListener(home.onClick, ctrl.GoToHomeScene);

        Label("MusicLabel", paper, "Music", 40f, new Vector2(-20f, -130f), new Vector2(500f, 50f), TextAlignmentOptions.Left, font);
        var musicSlider = Slider("MusicSlider", paper, new Vector2(0f, -185f));
        UnityEventTools.AddPersistentListener(musicSlider.onValueChanged, new UnityAction<float>(ctrl.SetMusicVolume));

        Label("SfxLabel", paper, "Sounds", 40f, new Vector2(-20f, -255f), new Vector2(500f, 50f), TextAlignmentOptions.Left, font);
        var sfxSlider = Slider("SfxSlider", paper, new Vector2(0f, -310f));
        UnityEventTools.AddPersistentListener(sfxSlider.onValueChanged, new UnityAction<float>(ctrl.SetSfxVolume));

        var toggle = Toggle("VibrationToggle", paper, new Vector2(-200f, -400f));
        UnityEventTools.AddPersistentListener(toggle.onValueChanged, new UnityAction<bool>(ctrl.OnVibrationToggleChanged));
        Label("VibrationLabel", paper, "Vibration", 40f, new Vector2(40f, -400f), new Vector2(380f, 50f), TextAlignmentOptions.Left, font);

        // Count-in: cream disc + big ink number, sibling of the menu so it survives the slide-out.
        var stale = root.transform.Find("CountIn");
        if (stale != null) Object.DestroyImmediate(stale.gameObject);
        var countIn = Rect("CountIn", root.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360f, 360f));
        countIn.SetSiblingIndex(menu.GetSiblingIndex() + 1);
        var disc = countIn.gameObject.AddComponent<Image>();
        disc.sprite = Knob; disc.color = new Color(Cream.r, Cream.g, Cream.b, 0.9f);
        var countText = Label("Number", countIn, "3", 240f, Vector2.zero, new Vector2(360f, 360f), TextAlignmentOptions.Center, font);
        countText.fontStyle = FontStyles.Bold;
        countIn.gameObject.SetActive(false);

        var so = new SerializedObject(ctrl);
        so.FindProperty("restartButton").objectReferenceValue = restart.gameObject;
        so.FindProperty("countInText").objectReferenceValue = countText;
        so.FindProperty("countInRoot").objectReferenceValue = countIn.gameObject;
        so.FindProperty("musicSlider").objectReferenceValue = musicSlider;
        so.FindProperty("sfxSlider").objectReferenceValue = sfxSlider;
        so.FindProperty("vibrationToggle").objectReferenceValue = toggle;
        so.FindProperty("audioMixer").objectReferenceValue = mixer;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, OverlayPath);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("[BuildPauseMenu] overlay prefab rebuilt");
    }

    static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static TextMeshProUGUI Label(string name, Transform parent, string text, float size, Vector2 pos, Vector2 box, TextAlignmentOptions align, TMP_FontAsset font)
    {
        var rt = Rect(name, parent, new Vector2(0.5f, 0.5f), pos, box);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text; tmp.fontSize = size; tmp.color = Ink; tmp.alignment = align;
        tmp.raycastTarget = false;
        return tmp;
    }

    static Button Button(string name, Transform parent, string text, Vector2 pos, TMP_FontAsset font)
    {
        var rt = Rect(name, parent, new Vector2(0.5f, 0.5f), pos, new Vector2(440f, 110f));
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = UISprite; img.type = Image.Type.Sliced; img.color = Brown;
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var label = Label("Text", rt, text, 56f, Vector2.zero, new Vector2(440f, 110f), TextAlignmentOptions.Center, font);
        label.color = Cream;
        return btn;
    }

    static Slider Slider(string name, Transform parent, Vector2 pos)
    {
        var res = new DefaultControls.Resources { standard = UISprite, background = BgSprite, knob = Knob };
        var go = DefaultControls.CreateSlider(res);
        go.name = name;
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(500f, 40f);
        var slider = go.GetComponent<Slider>();
        slider.minValue = 0f; slider.maxValue = 1f; slider.value = 1f;
        go.transform.Find("Background").GetComponent<Image>().color = FaintInk;
        go.transform.Find("Fill Area/Fill").GetComponent<Image>().color = Brown;
        var handle = go.transform.Find("Handle Slide Area/Handle");
        handle.GetComponent<Image>().color = Brown;
        handle.GetComponent<RectTransform>().sizeDelta = new Vector2(50f, 10f); // handle anchors stretch vertically: 40 + 10 = 50 tall
        return slider;
    }

    static Toggle Toggle(string name, Transform parent, Vector2 pos)
    {
        var res = new DefaultControls.Resources { standard = UISprite, background = BgSprite, checkmark = Check };
        var go = DefaultControls.CreateToggle(res);
        go.name = name;
        Object.DestroyImmediate(go.transform.Find("Label").gameObject);
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(60f, 60f);
        var bg = go.transform.Find("Background").GetComponent<RectTransform>();
        bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one; bg.offsetMin = bg.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = Cream;
        var check = bg.Find("Checkmark").GetComponent<RectTransform>();
        check.anchorMin = Vector2.zero; check.anchorMax = Vector2.one; check.offsetMin = new Vector2(8f, 8f); check.offsetMax = new Vector2(-8f, -8f);
        check.GetComponent<Image>().color = Ink;
        var toggle = go.GetComponent<Toggle>();
        toggle.isOn = true;
        return toggle;
    }

    // ── Audio routing ────────────────────────────────────────────────────────

    static bool IsMusicName(string n) => n.Contains("Music") || n.Contains("Ambi");

    static void RouteAudioManagerPrefab(AudioMixerGroup music, AudioMixerGroup sfx)
    {
        var root = PrefabUtility.LoadPrefabContents(AudioManagerPath);
        int m = 0, s = 0;
        foreach (var src in root.GetComponentsInChildren<AudioSource>(true))
        {
            bool isMusic = IsMusicName(src.gameObject.name);
            src.outputAudioMixerGroup = isMusic ? music : sfx;
            if (isMusic) m++; else s++;
        }
        PrefabUtility.SaveAsPrefabAsset(root, AudioManagerPath);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log($"[BuildPauseMenu] AudioManager prefab routed: music={m} sfx={s}");
    }

    // ── Scenes ───────────────────────────────────────────────────────────────

    static void WireScenes(AudioMixerGroup music, AudioMixerGroup sfx)
    {
        var active = SceneManager_ActivePath();
        if (EditorSceneManager.GetActiveScene().isDirty)
        {
            Debug.Log("[BuildPauseMenu] active scene was dirty; saving it first: " + active);
            EditorSceneManager.SaveOpenScenes();
        }

        foreach (var name in new[] { "Home", "Bongo", "Garden", "Pulse" })
        {
            string path = $"Assets/Scenes/{name}.unity";
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var overlayPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OverlayPath);

            // Route any scene-level AudioSource that has no group yet.
            int routed = 0;
            foreach (var src in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (src.outputAudioMixerGroup != null) continue;
                src.outputAudioMixerGroup = IsMusicName(src.gameObject.name) ? music : sfx;
                routed++;
            }

            // Overlay: add to Pulse (which only had a bare pause button), make sure the pause handler is wired everywhere.
            var ctrl = Object.FindFirstObjectByType<OverlayUIController>(FindObjectsInactive.Include);
            if (ctrl == null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(overlayPrefab, scene);
                ctrl = inst.GetComponent<OverlayUIController>();
                Debug.Log($"[BuildPauseMenu] {name}: overlay prefab instantiated");
            }
            var gm = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
            var ph = gm != null ? gm.GetComponent<PauseHandler>() : null;
            var so = new SerializedObject(ctrl);
            var phProp = so.FindProperty("pauseHandler");
            if (phProp.objectReferenceValue == null && ph != null)
            {
                phProp.objectReferenceValue = ph;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"[BuildPauseMenu] {name}: pauseHandler assigned");
            }

            if (name == "Pulse")
            {
                foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (b.name == "PauseButton") { Object.DestroyImmediate(b.gameObject); Debug.Log("[BuildPauseMenu] Pulse: old PauseButton removed"); }
                var pulse = Object.FindFirstObjectByType<PulseModeController>(FindObjectsInactive.Include);
                var pso = new SerializedObject(pulse);
                pso.FindProperty("stemOutput").objectReferenceValue = music;
                pso.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            Debug.Log($"[BuildPauseMenu] {name}: routed {routed} source(s), pauseHandler={(phProp.objectReferenceValue != null)}, saved={saved}");
        }

        if (!string.IsNullOrEmpty(active)) EditorSceneManager.OpenScene(active, OpenSceneMode.Single);
    }

    static string SceneManager_ActivePath() => EditorSceneManager.GetActiveScene().path;
}
