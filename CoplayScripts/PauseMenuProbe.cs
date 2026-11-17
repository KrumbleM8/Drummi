using DG.Tweening;
using KrumbleHut.Drummi.UI;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

// Play-mode probes for the pause menu.
//   Execute()        - open the pause menu, log state
//   ExecuteResume()  - press Resume, screenshot mid count-in, log state after it
//   ExecuteStatus()  - log pause/panel state
//   ExecuteMixer()   - set/get the exposed mixer params, drive the sliders
//   ExecuteShot()    - screenshot now
public static class PauseMenuProbe
{
    const string ShotDir = "C:/Users/karlr/.claude/jobs/e8477a2b/tmp/";

    static OverlayUIController Ctrl => Object.FindFirstObjectByType<OverlayUIController>(FindObjectsInactive.Include);

    public static void Execute()
    {
        var c = Ctrl;
        if (c == null) { Debug.Log("[PauseProbe] no OverlayUIController"); return; }
        c.OpenSettings();
        DOVirtual.DelayedCall(0.5f, () => { Status("after open"); Shot("pause_open"); }).SetUpdate(true);
    }

    public static void ExecuteResume()
    {
        var c = Ctrl;
        if (c == null) { Debug.Log("[PauseProbe] no OverlayUIController"); return; }
        c.Resume();
        DOVirtual.DelayedCall(0.35f, () => { Status("mid count-in"); Shot("pause_countin"); }).SetUpdate(true);
        DOVirtual.DelayedCall(3.5f, () => Status("after count-in")).SetUpdate(true);
    }

    public static void ExecuteStatus() => Status("status");

    // Real device-level input through the Input System event queue (what a mouse click / key press does).
    public static void ExecuteMouseTap()
    {
        var c = Ctrl;
        var rt = c.transform.Find("SettingsIcon").GetComponent<RectTransform>();
        var corners = new Vector3[4]; rt.GetWorldCorners(corners);
        Vector2 centre = (corners[0] + corners[2]) * 0.5f;
        var mouse = UnityEngine.InputSystem.Mouse.current;
        Debug.Log($"[PauseProbe] mouse={(mouse != null ? mouse.name : "<none>")} touchscreen={(UnityEngine.InputSystem.Touchscreen.current != null)} tap at {centre}");
        Status("before mouse tap");
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = centre, buttons = 1 });
        DOVirtual.DelayedCall(0.15f, () =>
        {
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = centre, buttons = 0 });
        }).SetUpdate(true);
        DOVirtual.DelayedCall(0.6f, () => Status("after mouse tap")).SetUpdate(true);
    }

    public static void ExecuteEscape()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        Debug.Log($"[PauseProbe] keyboard={(kb != null ? kb.name : "<none>")}");
        Status("before escape");
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Escape));
        DOVirtual.DelayedCall(0.15f, () => UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState())).SetUpdate(true);
        DOVirtual.DelayedCall(0.6f, () => Status("after escape")).SetUpdate(true);
    }

    // Simulates a tap on the X icon: raycast at its screen centre (what is on top?) then invoke its Button.
    public static void ExecuteClickIcon()
    {
        var c = Ctrl;
        if (c == null) { Debug.Log("[PauseProbe] no OverlayUIController"); return; }
        var icon = c.transform.Find("SettingsIcon");
        var rt = icon.GetComponent<RectTransform>();
        var corners = new Vector3[4]; rt.GetWorldCorners(corners);
        Vector2 centre = (corners[0] + corners[2]) * 0.5f;
        var es = UnityEngine.EventSystems.EventSystem.current;
        var ped = new UnityEngine.EventSystems.PointerEventData(es) { position = centre };
        var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        es.RaycastAll(ped, hits);
        Debug.Log($"[PauseProbe] icon centre={centre} eventSystem={(es != null ? es.name : "<none>")} module={(es != null && es.currentInputModule != null ? es.currentInputModule.GetType().Name : "<none>")} hits={hits.Count}");
        foreach (var h in hits) Debug.Log($"[PauseProbe]   hit {h.gameObject.name} (canvas {h.module.name}, depth {h.depth}, sort {h.sortingOrder})");
        // The icon is an EventTrigger (PointerDown -> ToggleSettings), so dispatch a real pointer-down through the event system.
        var trig = icon.GetComponent<UnityEngine.EventSystems.EventTrigger>();
        foreach (var d in trig.triggers)
            Debug.Log($"[PauseProbe] trigger {d.eventID}: calls={d.callback.GetPersistentEventCount()} target={d.callback.GetPersistentTarget(0)} method={d.callback.GetPersistentMethodName(0)}");
        Status("before pointer-down");
        UnityEngine.EventSystems.ExecuteEvents.Execute(icon.gameObject, ped, UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);
        DOVirtual.DelayedCall(0.5f, () => Status("after pointer-down")).SetUpdate(true);
    }

    public static void ExecuteShot() => Shot("pause_shot");

    public static void ExecuteMixer()
    {
        var c = Ctrl;
        var mixer = Resources.FindObjectsOfTypeAll<AudioMixer>();
        foreach (var m in mixer)
        {
            bool set = m.SetFloat("MusicVolume", -12f);
            bool got = m.GetFloat("MusicVolume", out float v);
            bool set2 = m.SetFloat("SFXVolume", -6f);
            m.GetFloat("SFXVolume", out float v2);
            Debug.Log($"[PauseProbe] mixer {m.name}: setMusic={set} getMusic={got} music={v} setSfx={set2} sfx={v2}");
            m.SetFloat("MusicVolume", 0f); m.SetFloat("SFXVolume", 0f);
        }
        if (c != null)
        {
            var sliders = c.GetComponentsInChildren<Slider>(true);
            foreach (var s in sliders)
            {
                s.value = 0.5f;
                Debug.Log($"[PauseProbe] slider {s.name} -> 0.5, listeners={s.onValueChanged.GetPersistentEventCount()} music={c.GetMusicVolume()} sfx={c.GetSfxVolume()}");
                s.value = 1f;
            }
            foreach (var t in c.GetComponentsInChildren<Toggle>(true))
            {
                t.isOn = false;
                Debug.Log($"[PauseProbe] toggle {t.name} -> off, vibration={c.GetVibration()}");
                t.isOn = true;
            }
        }
        int ungrouped = 0, total = 0;
        foreach (var src in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            total++;
            if (src.outputAudioMixerGroup == null) { ungrouped++; Debug.Log("[PauseProbe] ungrouped AudioSource: " + src.gameObject.name); }
        }
        Debug.Log($"[PauseProbe] audio sources total={total} ungrouped={ungrouped}");
    }

    static void Status(string tag)
    {
        var c = Ctrl;
        bool paused = GameClock.Instance != null && GameClock.Instance.IsPaused;
        bool playing = GameManager.instance != null && GameManager.instance.IsPlaying;
        var panel = c != null ? c.transform.Find("SettingsMenu") : null;
        var countIn = c != null ? c.transform.Find("CountIn") : null;
        var restart = panel != null ? panel.Find("Paper/Restart") : null;
        var gm = GameManager.instance;
        Debug.Log($"[PauseProbe] {tag}: scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} gm={(gm != null ? gm.gameObject.scene.name : "<null>")} mode={(gm != null && gm.ActiveMode != null ? gm.ActiveMode.ModeId : "<none>")} paused={paused} playing={playing} timeScale={Time.timeScale} panel={(panel != null && panel.gameObject.activeSelf)} " +
                  $"countIn={(countIn != null && countIn.gameObject.activeSelf)} restartShown={(restart != null && restart.gameObject.activeSelf)} gameTime={(GameClock.Instance != null ? GameClock.Instance.GameTime : -1):F2}");
    }

    static void Shot(string name)
    {
        string path = ShotDir + name + ".png";
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log("[PauseProbe] screenshot " + path);
    }
}
