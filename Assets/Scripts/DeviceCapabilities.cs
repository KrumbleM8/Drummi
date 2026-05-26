/// <summary>
/// Stores device capability flags detected at startup by Bootstrap.
/// Populated once before any game scene loads; safe to read from any system thereafter.
///
/// All flags default to false so callers that run before Bootstrap completes
/// (e.g. Editor play-mode without a Bootstrap scene) get a safe conservative value.
/// </summary>
public static class DeviceCapabilities
{
    // ── Haptics ───────────────────────────────────────────────────────────────

    /// <summary>
    /// True if the device has vibration/haptic hardware.
    /// Android: checks the Vibrator service via JNI.
    /// iOS/other: uses SystemInfo.supportsVibration.
    /// PC: always false.
    /// </summary>
    public static bool HapticsSupported { get; internal set; }

    /// <summary>
    /// True if the Android vibrator supports per-effect amplitude control (API 26+).
    /// Always false on non-Android platforms.
    /// </summary>
    public static bool HapticsAmplitudeControl { get; internal set; }

    // ── Audio ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// True if a Bluetooth A2DP audio device is the active output at launch.
    /// Android only — detected via AudioManager.isBluetoothA2dpOn().
    /// iOS/PC: always false (requires native plugin to detect; see Bootstrap.cs comments).
    /// Re-check at runtime if the user may connect/disconnect headphones mid-session.
    /// </summary>
    public static bool BluetoothAudioActive { get; internal set; }

    // ── Input ─────────────────────────────────────────────────────────────────

    /// <summary>True if the device supports multi-touch input.</summary>
    public static bool MultiTouchSupported { get; internal set; }

    // ── Display ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Detected display refresh rate in Hz. Defaults to 60 if detection fails.
    /// Bootstrap applies this as Application.targetFrameRate.
    /// </summary>
    public static int MaxFrameRate { get; internal set; } = 60;
}
